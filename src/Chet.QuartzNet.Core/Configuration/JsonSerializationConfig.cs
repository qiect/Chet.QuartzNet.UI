using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Chet.QuartzNet.Core.Configuration
{

    /// <summary>
    /// DateTime序列化配置辅助类
    /// </summary>
    public static class JsonSerializationConfig
    {
        /// <summary>
        /// 创建包含DateTime格式化配置的JsonSerializerOptions
        /// </summary>
        public static JsonSerializerOptions JsonOptions()
        {
            return new JsonSerializerOptions
            {
                // 格式化JSON输出，增加可读性，便于调试和查看
                WriteIndented = true,
                
                // 使用不安全的宽松JSON转义编码器，允许输出更多字符而不转义
                // 适合内部系统之间的数据交换，提高JSON可读性
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                
                // 序列化时忽略值为null的属性，减少JSON大小
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                
                // 反序列化时忽略属性名大小写，提高兼容性
                // 允许处理不同命名约定的JSON数据（如驼峰式、 PascalCase等）
                PropertyNameCaseInsensitive = true,
                
                // 注册自定义UTC时间转换器，确保DateTime值以统一格式序列化
                // 避免时区偏移问题，提高跨系统数据交换的可靠性
                Converters = { new UtcDateTimeConverter() }
            };
        }

        /// <summary>
        /// UTC DateTime转换器，将DateTime序列化为不带时区偏移的格式
        /// </summary>
        private class UtcDateTimeConverter : System.Text.Json.Serialization.JsonConverter<DateTime>
        {
            /// <summary>
            /// DateTime格式
            /// </summary>
            private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffff";

            /// <summary>
            /// 读取JSON中的DateTime值
            /// </summary>
            public override DateTime Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            {
                return DateTime.Parse(reader.GetString()!);
            }

            /// <summary>
            /// 将DateTime值写入JSON
            /// </summary>
            public override void Write(System.Text.Json.Utf8JsonWriter writer, DateTime value, System.Text.Json.JsonSerializerOptions options)
            {
                // 序列化时使用UTC时间，避免时区偏移
                writer.WriteStringValue(value.ToString(DateTimeFormat));
            }
        }
    }
}
