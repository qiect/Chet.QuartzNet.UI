<script setup lang="ts">
import { withBase } from 'vitepress'

const logo = withBase('/logo.svg')
const link = (path: string) => withBase(path)
</script>

<template>
  <div class="ca-landing">
    <!-- Hero -->
    <section class="ca-hero">
      <div class="ca-hero-glow" aria-hidden="true" />
      <div class="ca-container ca-hero-inner">
        <span class="ca-hero-badge">
          <span class="ca-badge-dot" />
          .NET 8 / 10 · Quartz.Net · 开源免费
        </span>
        <img class="ca-hero-mark" :src="logo" alt="Chet.QuartzNet.UI" width="64" height="64" />
        <h1 class="ca-hero-title">Chet.QuartzNet.UI</h1>
        <p class="ca-hero-slogan">让 Quartz.Net 调度，<span>从未如此简单</span></p>
        <p class="ca-hero-desc">
          一个 NuGet 包，即可为 .NET 项目接入可视化任务调度：作业管理、Cron 表达式、
          执行日志、失败重试、作业依赖与多渠道通知，文件或数据库存储随需切换。
        </p>
        <div class="ca-hero-actions">
          <a class="ca-btn ca-btn-primary" :href="link('/guide/quick-start')">
            快速开始<span class="ca-btn-arrow">→</span>
          </a>
          <a class="ca-btn ca-btn-ghost" :href="link('/guide/introduction')">项目简介</a>
        </div>
        <div class="ca-hero-meta">
          <span>.NET 8 / .NET 10</span>
          <i />
          <span>Quartz.Net 3.15</span>
          <i />
          <a href="https://github.com/qiect/Chet.QuartzNet.UI" target="_blank" rel="noreferrer">
            GitHub 开源 →
          </a>
        </div>
      </div>

      <div class="ca-container ca-hero-preview">
        <span class="ca-float ca-float-a">Cron 可视化</span>
        <span class="ca-float ca-float-b">多渠道通知</span>
        <div class="ca-window">
          <div class="ca-window-bar">
            <div class="ca-window-dots"><span /><span /><span /></div>
            <div class="ca-window-title">Chet.QuartzNet.UI · 可视化作业调度</div>
            <div class="ca-window-mode"><span class="ca-mode-dot" />在线运行</div>
          </div>
          <div class="ca-window-body">
            <div class="ca-source-pane">
              <div class="ca-pane-label">集成代码</div>
              <div class="ca-src">
                <div class="ca-src-line"><span class="ca-src-num">1</span><span class="ca-src-comment">// Program.cs</span></div>
                <div class="ca-src-line"><span class="ca-src-num">2</span><span class="ca-src-fn">builder</span>.Services.<span class="ca-src-fn">AddQuartzUI</span>(</div>
                <div class="ca-src-line"><span class="ca-src-num">3</span>  builder.Configuration);</div>
                <div class="ca-src-line"><span class="ca-src-num">4</span><span class="ca-src-fn">builder</span>.Services.<span class="ca-src-fn">AddQuartzClassJobs</span>();</div>
                <div class="ca-src-line"><span class="ca-src-num">5</span> </div>
                <div class="ca-src-line"><span class="ca-src-num">6</span><span class="ca-src-key">var</span> app = builder.<span class="ca-src-fn">Build</span>();</div>
                <div class="ca-src-line"><span class="ca-src-num">7</span>app.<span class="ca-src-fn">UseQuartz</span>();</div>
                <div class="ca-src-line"><span class="ca-src-num">8</span>app.<span class="ca-src-fn">Run</span>();</div>
                <div class="ca-src-line"><span class="ca-src-num">9</span> </div>
                <div class="ca-src-line"><span class="ca-src-num">10</span><span class="ca-src-comment">// dotnet run</span></div>
                <div class="ca-src-line"><span class="ca-src-num">11</span><span class="ca-src-ok">Now listening on: localhost:5000</span></div>
                <div class="ca-src-line"><span class="ca-src-num">12</span><span class="ca-src-ok">Quartz UI: /quartz-ui</span></div>
              </div>
            </div>
            <div class="ca-render-pane">
              <div class="ca-pane-label">管理后台</div>
              <div class="ca-page">
                <div class="ca-page-crumb">工作台 / 分析页</div>
                <h3>分析页</h3>
                <div class="ca-page-meta">Chet.QuartzNet.UI · 今日概览</div>
                <div class="ca-mini-stats">
                  <div><strong>42</strong><span>作业总数</span></div>
                  <div><strong>1,208</strong><span>今日执行</span></div>
                  <div><strong>3</strong><span>失败次数</span></div>
                </div>
                <div class="ca-mini-table">
                  <div class="ca-mini-row ca-mini-head"><span>作业名称</span><span>分组</span><span>状态</span></div>
                  <div class="ca-mini-row"><span>data-sync</span><span>DEFAULT</span><span class="ca-tag">运行中</span></div>
                  <div class="ca-mini-row"><span>report-job</span><span>REPORT</span><span class="ca-tag">正常</span></div>
                  <div class="ca-mini-row"><span>clean-temp</span><span>SYSTEM</span><span class="ca-tag ca-tag-muted">暂停</span></div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Stats -->
    <section class="ca-stats">
      <div class="ca-container ca-stats-grid">
        <div class="ca-stat"><strong>5<span>种</span></strong><span>存储类型自由切换</span></div>
        <div class="ca-stat"><strong>6<span>大</span></strong><span>可视化管理模块</span></div>
        <div class="ca-stat"><strong>15<span>+</span></strong><span>版本持续迭代</span></div>
        <div class="ca-stat"><strong>100<span>%</span></strong><span>MIT 开源可商用</span></div>
      </div>
    </section>

    <!-- 集成 -->
    <section class="ca-efficiency">
      <div class="ca-container">
        <div class="ca-section-head ca-section-head--dark">
          <span class="ca-eyebrow">为效率而生</span>
          <h2>几行代码，从安装到运行</h2>
          <p>一个 NuGet 包完成集成，文件存储零依赖；需要数据库时再按需切换，不写额外脚手架。</p>
        </div>
        <div class="ca-key-grid">
          <div class="ca-key-card">
            <div class="ca-keys"><kbd>dotnet add package</kbd></div>
            <h3>一键安装</h3>
            <p>安装 Chet.QuartzNet.UI 主包，文件存储模式无需任何数据库依赖。</p>
          </div>
          <div class="ca-key-card">
            <div class="ca-keys"><kbd>AddQuartzUI()</kbd></div>
            <h3>注入服务</h3>
            <p>一行代码完成调度器、存储、认证与管理界面的服务注册。</p>
          </div>
          <div class="ca-key-card">
            <div class="ca-keys"><kbd>UseQuartz()</kbd></div>
            <h3>启用中间件</h3>
            <p>挂载 UI 路由与静态资源，JWT 认证保护管理界面。</p>
          </div>
          <div class="ca-key-card">
            <div class="ca-keys"><kbd>/quartz-ui</kbd></div>
            <h3>访问管理台</h3>
            <p>启动应用后直接访问，作业、日志、通知一目了然。</p>
          </div>
          <div class="ca-key-card">
            <div class="ca-keys"><kbd>[QuartzJob]</kbd></div>
            <h3>ClassJob 自动注册</h3>
            <p>标记特性的作业类自动扫描注册，代码与 UI 管理无缝结合。</p>
          </div>
          <div class="ca-key-card">
            <div class="ca-keys"><kbd>StorageType</kbd></div>
            <h3>存储随需切换</h3>
            <p>文件 / MySQL / PostgreSQL / SQLite / SQL Server，五种存储任意互转。</p>
          </div>
        </div>
      </div>
    </section>

    <!-- 特性 -->
    <section class="ca-section ca-features">
      <div class="ca-container">
        <div class="ca-section-head">
          <span class="ca-eyebrow">轻量但不简陋</span>
          <h2>一整套任务调度工作流</h2>
          <p>从作业创建、执行监控到失败通知与数据迁移，高频运维场景都在同一套界面里完成。</p>
        </div>
        <div class="ca-feature-grid">
          <a class="ca-feature" :href="link('/modules/job')">
            <div class="ca-feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                <rect x="3" y="4" width="18" height="16" rx="2" />
                <path d="M8 9h8M8 13h5" />
                <path d="m16 17 2 2 3-3" />
              </svg>
            </div>
            <h3>可视化管理<span>→</span></h3>
            <p>新增、编辑、暂停、恢复、立即执行、批量操作，无需编写配置代码。</p>
          </a>
          <a class="ca-feature" :href="link('/modules/dashboard')">
            <div class="ca-feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                <path d="M3 12h4l2-6 4 12 2-6h6" />
                <path d="M3 21h18" />
              </svg>
            </div>
            <h3>实时监控分析<span>→</span></h3>
            <p>KPI 卡片、健康象限、时段热力图、耗时排行，执行状态一目了然。</p>
          </a>
          <a class="ca-feature" :href="link('/modules/log')">
            <div class="ca-feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                <path d="M5 3h9l5 5v13H5z" />
                <path d="M14 3v5h5M9 13h6M9 17h4" />
              </svg>
            </div>
            <h3>执行日志<span>→</span></h3>
            <p>完整的执行历史、耗时、错误信息与「已跳过」状态，支持自动清理。</p>
          </a>
          <a class="ca-feature" :href="link('/modules/notification')">
            <div class="ca-feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                <path d="M18 8a6 6 0 1 0-12 0c0 7-3 8-3 8h18s-3-1-3-8" />
                <path d="M10 21a2 2 0 0 0 4 0" />
              </svg>
            </div>
            <h3>多渠道通知<span>→</span></h3>
            <p>基于 PushPlus，微信、企业微信、钉钉、邮件多渠道触达，支持三种模板。</p>
          </a>
          <a class="ca-feature" :href="link('/guide/dependency')">
            <div class="ca-feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="6" cy="6" r="3" />
                <circle cx="18" cy="18" r="3" />
                <path d="M9 6h6a3 3 0 0 1 3 3v6" />
              </svg>
            </div>
            <h3>作业依赖<span>→</span></h3>
            <p>前置作业全部成功才执行，否则跳过并记录日志，轻量版工作流。</p>
          </a>
          <a class="ca-feature" :href="link('/guide/migration')">
            <div class="ca-feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
                <path d="M4 7h11l-3-3M4 7l3 3" />
                <path d="M20 17H9l3-3M20 17l-3 3" />
              </svg>
            </div>
            <h3>数据迁移<span>→</span></h3>
            <p>五种存储任意互转，预检、备份、进度与迁移后校验一步到位。</p>
          </a>
        </div>
      </div>
    </section>

    <!-- CTA -->
    <section class="ca-cta">
      <div class="ca-container">
        <div class="ca-cta-card">
          <div class="ca-cta-copy">
            <span class="ca-eyebrow ca-eyebrow--light">5 分钟上手</span>
            <h2>把可视化调度跑起来</h2>
            <p>安装 NuGet 包、注入两个服务、访问 <code>/quartz-ui</code>，全部流程写在一篇文档里。MIT 协议，可自由商用。</p>
          </div>
          <div class="ca-cta-actions">
            <a class="ca-btn ca-btn-light" :href="link('/guide/quick-start')">快速开始</a>
            <a class="ca-btn ca-btn-outline-light" href="https://github.com/qiect/Chet.QuartzNet.UI" target="_blank" rel="noreferrer">GitHub</a>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>
