using System.ComponentModel.DataAnnotations;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 条件范围验证属性
/// </summary>
public class ConditionalRangeAttribute : RangeAttribute
{
    private readonly string _dependentProperty;
    private readonly object _targetValue;

    public ConditionalRangeAttribute(
        double minimum,
        double maximum,
        string dependentProperty,
        object targetValue
    )
        : base(minimum, maximum)
    {
        _dependentProperty = dependentProperty;
        _targetValue = targetValue;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // 获取依赖属性的值
        var dependentPropertyValue = validationContext
            .ObjectType.GetProperty(_dependentProperty)
            ?.GetValue(validationContext.ObjectInstance);

        // 只有当依赖属性的值等于目标值时，才进行范围验证
        if (dependentPropertyValue != null && dependentPropertyValue.Equals(_targetValue))
        {
            return base.IsValid(value, validationContext);
        }

        // 否则验证通过
        return ValidationResult.Success;
    }
}
