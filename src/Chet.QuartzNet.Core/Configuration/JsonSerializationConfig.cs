using System.Text.Json;

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
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = System
                    .Text
                    .Json
                    .Serialization
                    .JsonIgnoreCondition
                    .WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                Converters = { new UtcDateTimeConverter(), new DateTimeOffsetConverter() },
            };
        }

        /// <summary>
        /// 配置ASP.NET Core MVC的JSON选项，确保DateTime/DateTimeOffset带时区偏移序列化
        /// </summary>
        public static void ConfigureMvcJsonOptions(
            System.Text.Json.JsonSerializerOptions options
        )
        {
            options.Converters.Add(new UtcDateTimeConverter());
            options.Converters.Add(new DateTimeOffsetConverter());
        }

        /// <summary>
        /// DateTime转换器，序列化为ISO 8601格式（带时区偏移）
        /// </summary>
        private class UtcDateTimeConverter : System.Text.Json.Serialization.JsonConverter<DateTime>
        {
            private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffK";

            public override DateTime Read(
                ref System.Text.Json.Utf8JsonReader reader,
                Type typeToConvert,
                System.Text.Json.JsonSerializerOptions options
            )
            {
                return DateTime.Parse(reader.GetString()!);
            }

            public override void Write(
                System.Text.Json.Utf8JsonWriter writer,
                DateTime value,
                System.Text.Json.JsonSerializerOptions options
            )
            {
                writer.WriteStringValue(
                    value.Kind == DateTimeKind.Unspecified
                        ? value.ToString("yyyy-MM-ddTHH:mm:ss.fffffff")
                        : value.ToString(DateTimeFormat)
                );
            }
        }

        /// <summary>
        /// DateTimeOffset转换器，始终带时区偏移序列化，确保前端能正确转换时区
        /// </summary>
        private class DateTimeOffsetConverter
            : System.Text.Json.Serialization.JsonConverter<DateTimeOffset>
        {
            private const string Format = "yyyy-MM-ddTHH:mm:ss.fffffffzzz";

            public override DateTimeOffset Read(
                ref System.Text.Json.Utf8JsonReader reader,
                Type typeToConvert,
                System.Text.Json.JsonSerializerOptions options
            )
            {
                return DateTimeOffset.Parse(reader.GetString()!);
            }

            public override void Write(
                System.Text.Json.Utf8JsonWriter writer,
                DateTimeOffset value,
                System.Text.Json.JsonSerializerOptions options
            )
            {
                writer.WriteStringValue(value.ToString(Format));
            }
        }
    }
}