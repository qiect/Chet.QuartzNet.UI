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
    public static class DateTimeSerializationConfig
    {
        /// <summary>
        /// 创建包含DateTime格式化配置的JsonSerializerOptions
        /// </summary>
        public static JsonSerializerOptions JsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                // 使用UTC时间序列化，避免时区偏移
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
