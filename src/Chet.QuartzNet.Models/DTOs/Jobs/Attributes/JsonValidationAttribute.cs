using System.ComponentModel.DataAnnotations;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// JSON格式验证属性
/// </summary>
public class JsonValidationAttribute : ValidationAttribute
{
    public JsonValidationAttribute()
    {
        ErrorMessage = "必须是有效的JSON格式";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // 如果值为空，则验证通过（可以结合RequiredAttribute使用）
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success;
        }

        try
        {
            // 尝试解析JSON字符串
            var jsonString = value.ToString();
            if (jsonString != null)
            {
                System.Text.Json.JsonDocument.Parse(jsonString);
            }
            return ValidationResult.Success;
        }
        catch (System.Text.Json.JsonException)
        {
            // JSON解析失败，返回验证错误
            return new ValidationResult(ErrorMessage);
        }
    }
}
