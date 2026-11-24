/** 通知内容格式（与后端消息模板一一对应） */
export type NotificationContentFormat = 'html' | 'json' | 'markdown' | 'txt';

/** HTML 标签特征：通知内容以富文本卡片形式存储 */
const HTML_TAG_RE =
  /<(?:a|b|body|br|div|h[1-6]|html|img|li|ol|p|pre|span|strong|table|tbody|td|th|thead|tr|ul)\b[^>]*>/i;

/** Markdown 标题特征 */
const MD_HEADING_RE = /^\s{0,3}#{1,6}\s+\S/m;

/** Markdown 表格特征：至少一行表头 + 一行分隔线 */
const MD_TABLE_RE = /^\s*\|.+\|\s*$/m;

/** Markdown 代码块特征 */
const MD_FENCE_RE = /^\s*```/m;

/**
 * 推断通知内容格式。
 *
 * 历史通知记录没有保存模板类型，这里按内容特征判定：
 * JSON / HTML 特征明确，其余按 Markdown 语法特征区分，最后兜底为纯文本。
 *
 * @param content 通知内容
 */
export function detectContentFormat(content?: null | string): NotificationContentFormat {
  const text = (content ?? '').trim();
  if (!text) {
    return 'txt';
  }

  // JSON：能被解析才认作 JSON，避免误判以 { 开头的普通文本
  if (/^[[{]/.test(text) && /[\]}]$/.test(text)) {
    try {
      JSON.parse(text);
      return 'json';
    } catch {
      // 解析失败继续按其他格式判断
    }
  }

  if (HTML_TAG_RE.test(text)) {
    return 'html';
  }

  if (MD_HEADING_RE.test(text) || MD_TABLE_RE.test(text) || MD_FENCE_RE.test(text)) {
    return 'markdown';
  }

  return 'txt';
}

/**
 * JSON 内容格式化：解析成功时缩进输出，失败时原样返回
 *
 * @param content JSON 文本
 */
export function formatJsonContent(content?: null | string): string {
  const text = content ?? '';
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}

/** 转义 HTML 特殊字符，避免内容注入 */
function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

/** 行内语法：代码、加粗、斜体、删除线、链接 */
function renderInline(raw: string): string {
  const codes: string[] = [];

  // 行内代码先占位，避免内部字符被后续规则替换
  let output = escapeHtml(raw).replace(/`([^`\n]+)`/g, (_match, code: string) => {
    codes.push(`<code>${code}</code>`);
    return `\u0000${codes.length - 1}\u0000`;
  });

  output = output
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/(^|[^*])\*([^*\n]+)\*/g, '$1<em>$2</em>')
    .replace(/~~([^~]+)~~/g, '<del>$1</del>')
    .replace(
      /\[([^\]]+)\]\(([^)\s]+)\)/g,
      '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>',
    );

  return output.replace(
    /\u0000(\d+)\u0000/g,
    (_match, index: string) => codes[Number(index)] ?? '',
  );
}

/** 是否为表格行 */
const isTableRow = (line: string): boolean => /^\s*\|.*\|\s*$/.test(line);

/** 是否为表格分隔行（| --- | --- |） */
const isTableDivider = (line: string): boolean => /^\s*\|[\s:|-]*-[\s:|-]*\|\s*$/.test(line);

/** 拆分表格行单元格 */
function splitTableRow(line: string): string[] {
  return line
    .trim()
    .replace(/^\||\|$/g, '')
    .split('|')
    .map((cell) => cell.trim());
}

/** 解析列对齐方式 */
function parseAlign(cell: string): 'center' | 'left' | 'right' | undefined {
  const left = cell.startsWith(':');
  const right = cell.endsWith(':');
  if (left && right) {
    return 'center';
  }
  if (right) {
    return 'right';
  }
  return left ? 'left' : undefined;
}

/** 渲染表格 */
function renderTable(
  header: string[],
  aligns: ('center' | 'left' | 'right' | undefined)[],
  rows: string[][],
): string {
  const styleOf = (index: number) =>
    aligns[index] ? ` style="text-align: ${aligns[index]}"` : '';

  const head = header
    .map((cell, index) => `<th${styleOf(index)}>${renderInline(cell)}</th>`)
    .join('');
  const body = rows
    .map(
      (row) =>
        `<tr>${header
          .map(
            (_unused, index) =>
              `<td${styleOf(index)}>${renderInline(row[index] ?? '')}</td>`,
          )
          .join('')}</tr>`,
    )
    .join('');

  return `<table><thead><tr>${head}</tr></thead><tbody>${body}</tbody></table>`;
}

/** 该行是否开启新的 Markdown 块（用于段落结束判定） */
function startsNewBlock(line: string): boolean {
  return (
    /^\s{0,3}#{1,6}\s+/.test(line) ||
    /^\s*```/.test(line) ||
    /^\s{0,3}>\s?/.test(line) ||
    /^\s{0,3}(?:[-*+]|\d+\.)\s+/.test(line) ||
    /^\s{0,3}(?:-{3,}|\*{3,}|_{3,})\s*$/.test(line) ||
    isTableRow(line)
  );
}

/**
 * 轻量 Markdown 渲染。
 *
 * 仅覆盖后端消息模板会产出的语法（标题、表格、分隔线、代码块、列表、引用、行内样式），
 * 避免为了通知详情额外引入完整的 Markdown 依赖。
 *
 * @param markdown Markdown 文本
 */
export function renderMarkdown(markdown?: null | string): string {
  const lines = (markdown ?? '').replace(/\r\n?/g, '\n').split('\n');
  const html: string[] = [];
  let index = 0;

  while (index < lines.length) {
    const line = lines[index] ?? '';

    // 空行
    if (!line.trim()) {
      index++;
      continue;
    }

    // 代码块
    if (/^\s*```/.test(line)) {
      const code: string[] = [];
      index++;
      while (index < lines.length && !/^\s*```\s*$/.test(lines[index] ?? '')) {
        code.push(lines[index] ?? '');
        index++;
      }
      // 跳过结束围栏
      index++;
      html.push(`<pre class="md-code"><code>${escapeHtml(code.join('\n'))}</code></pre>`);
      continue;
    }

    // 标题
    const heading = /^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$/.exec(line);
    if (heading) {
      const level = (heading[1] ?? '#').length;
      html.push(`<h${level}>${renderInline(heading[2] ?? '')}</h${level}>`);
      index++;
      continue;
    }

    // 分隔线
    if (/^\s{0,3}(?:-{3,}|\*{3,}|_{3,})\s*$/.test(line)) {
      html.push('<hr />');
      index++;
      continue;
    }

    // 表格
    if (isTableRow(line) && isTableDivider(lines[index + 1] ?? '')) {
      const header = splitTableRow(line);
      const aligns = splitTableRow(lines[index + 1] ?? '').map(parseAlign);
      index += 2;

      const rows: string[][] = [];
      while (index < lines.length && isTableRow(lines[index] ?? '')) {
        rows.push(splitTableRow(lines[index] ?? ''));
        index++;
      }
      html.push(renderTable(header, aligns, rows));
      continue;
    }

    // 引用
    if (/^\s{0,3}>\s?/.test(line)) {
      const quote: string[] = [];
      while (index < lines.length && /^\s{0,3}>\s?/.test(lines[index] ?? '')) {
        quote.push((lines[index] ?? '').replace(/^\s{0,3}>\s?/, ''));
        index++;
      }
      html.push(`<blockquote>${renderInline(quote.join(' '))}</blockquote>`);
      continue;
    }

    // 列表
    const listItem = /^\s{0,3}([-*+]|\d+\.)\s+(.*)$/.exec(line);
    if (listItem) {
      const ordered = /^\d+\.$/.test(listItem[1] ?? '');
      const items: string[] = [];

      while (index < lines.length) {
        const item = /^\s{0,3}([-*+]|\d+\.)\s+(.*)$/.exec(lines[index] ?? '');
        if (!item) {
          break;
        }
        items.push(item[2] ?? '');
        index++;
      }

      const tag = ordered ? 'ol' : 'ul';
      html.push(
        `<${tag}>${items.map((item) => `<li>${renderInline(item)}</li>`).join('')}</${tag}>`,
      );
      continue;
    }

    // 段落：遇到空行或新的块级语法时结束
    const paragraph: string[] = [];
    while (index < lines.length && (lines[index] ?? '').trim()) {
      if (paragraph.length > 0 && startsNewBlock(lines[index] ?? '')) {
        break;
      }
      paragraph.push(lines[index] ?? '');
      index++;
    }
    html.push(`<p>${renderInline(paragraph.join(' '))}</p>`);
  }

  return html.join('\n');
}
