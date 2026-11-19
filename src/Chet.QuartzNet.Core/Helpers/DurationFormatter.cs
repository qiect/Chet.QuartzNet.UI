namespace Chet.QuartzNet.Core.Helpers;

/// <summary>
/// 耗时格式化工具：把毫秒数转换为更易读的文本
/// </summary>
public static class DurationFormatter
{
    /// <summary>
    /// 格式化耗时（自动选择毫秒 / 秒 / 分钟 / 小时）
    /// </summary>
    /// <param name="milliseconds">耗时（毫秒）</param>
    /// <returns>可读文本，例如 850 毫秒、1.28 秒、5.2 分钟、2.56 小时</returns>
    public static string Format(long milliseconds)
    {
        if (milliseconds <= 0)
        {
            return "0 毫秒";
        }

        if (milliseconds < 1000)
        {
            return $"{milliseconds} 毫秒";
        }

        if (milliseconds < 60_000)
        {
            return $"{milliseconds / 1000d:0.##} 秒";
        }

        if (milliseconds < 3_600_000)
        {
            return $"{milliseconds / 60_000d:0.##} 分钟";
        }

        return $"{milliseconds / 3_600_000d:0.##} 小时";
    }
}
