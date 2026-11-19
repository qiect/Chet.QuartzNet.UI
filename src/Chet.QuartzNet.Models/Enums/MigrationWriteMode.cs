using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 迁移写入模式
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationWriteMode
{
    /// <summary>
    /// 不存在则插入，存在则跳过（设置为按键 Upsert），幂等
    /// </summary>
    Merge = 0,

    /// <summary>
    /// 不存在则插入，存在则更新
    /// </summary>
    Overwrite = 1,

    /// <summary>
    /// 先清空所选实体的目标数据，再写入
    /// </summary>
    Replace = 2,
}
