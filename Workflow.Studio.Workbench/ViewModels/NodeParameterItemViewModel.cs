using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace Workflow.Studio.Workbench.ViewModels;

public sealed partial class NodeParameterItemViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly Type _valueType;

    [ObservableProperty]
    private string _valueText;

    public NodeParameterItemViewModel(string key, object? value)
    {
        Key = key;
        _valueType = value?.GetType() ?? typeof(string);
        _valueText = SerializeValue(value, _valueType);
    }

    public string Key { get; }

    public string TypeName => GetFriendlyTypeName(_valueType);

    public string EditorHint => UsesStructuredEditor
        ? "复杂类型请使用 JSON 格式编辑。"
        : "直接输入参数值即可。";

    public bool UsesStructuredEditor => !IsSimpleValueType(_valueType);

    public bool TryBuildValue(out object? value, out string? error)
    {
        var targetType = Nullable.GetUnderlyingType(_valueType) ?? _valueType;

        try
        {
            if (targetType == typeof(string))
            {
                value = ValueText;
                error = null;
                return true;
            }

            if (string.IsNullOrWhiteSpace(ValueText))
            {
                value = null;
                error = null;
                return true;
            }

            if (targetType.IsEnum)
            {
                value = Enum.Parse(targetType, ValueText, ignoreCase: true);
                error = null;
                return true;
            }

            if (targetType == typeof(bool))
            {
                value = bool.Parse(ValueText);
                error = null;
                return true;
            }

            if (targetType == typeof(Guid))
            {
                value = Guid.Parse(ValueText);
                error = null;
                return true;
            }

            if (targetType == typeof(DateTime))
            {
                value = DateTime.Parse(ValueText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                error = null;
                return true;
            }

            if (targetType == typeof(DateTimeOffset))
            {
                value = DateTimeOffset.Parse(ValueText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                error = null;
                return true;
            }

            if (targetType.IsPrimitive || targetType == typeof(decimal))
            {
                value = Convert.ChangeType(ValueText, targetType, CultureInfo.InvariantCulture);
                error = null;
                return true;
            }

            value = JsonSerializer.Deserialize(ValueText, _valueType, JsonOptions);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            value = null;
            error = $"参数 \"{Key}\" 无法转换为 {TypeName}: {ex.Message}";
            return false;
        }
    }

    private static bool IsSimpleValueType(Type type)
    {
        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        return targetType.IsPrimitive
            || targetType.IsEnum
            || targetType == typeof(string)
            || targetType == typeof(decimal)
            || targetType == typeof(Guid)
            || targetType == typeof(DateTime)
            || targetType == typeof(DateTimeOffset)
            || targetType == typeof(TimeSpan);
    }

    private static string SerializeValue(object? value, Type valueType)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (valueType == typeof(string))
        {
            return (string)value;
        }

        if (IsSimpleValueType(valueType))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        try
        {
            return JsonSerializer.Serialize(value, valueType, JsonOptions);
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var genericArguments = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
        var typeName = type.Name[..type.Name.IndexOf('`')];
        return $"{typeName}<{genericArguments}>";
    }
}
