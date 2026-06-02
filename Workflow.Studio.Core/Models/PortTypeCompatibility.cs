namespace Workflow.Studio.Core.Models;

public static class PortTypeCompatibility
{
    public static bool AreCompatible(PortMetadata sourceMetadata, PortMetadata targetMetadata)
    {
        ArgumentNullException.ThrowIfNull(sourceMetadata);
        ArgumentNullException.ThrowIfNull(targetMetadata);

        return AreCompatible(sourceMetadata.DataType, targetMetadata.DataType)
            && AreSemanticTypesCompatible(sourceMetadata.SemanticTypeKey, targetMetadata.SemanticTypeKey);
    }

    public static bool AreCompatible(Type sourceType, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(targetType);

        var normalizedSourceType = NormalizeNullableType(sourceType);
        var normalizedTargetType = NormalizeNullableType(targetType);

        return normalizedTargetType.IsAssignableFrom(normalizedSourceType);
    }

    public static bool IsValueCompatible(Type declaredType, object? value)
    {
        ArgumentNullException.ThrowIfNull(declaredType);

        if (value is null)
        {
            return true;
        }

        return NormalizeNullableType(declaredType).IsInstanceOfType(value);
    }

    public static void EnsureValueMatches(Type declaredType, object? value, string context)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        if (IsValueCompatible(declaredType, value))
        {
            return;
        }

        var actualType = value?.GetType() ?? typeof(object);

        throw new InvalidOperationException(
            $"{context} 期望数据类型为 '{GetDisplayName(declaredType)}'，但实际收到 '{GetDisplayName(actualType)}'。");
    }

    public static string GetDisplayName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var normalizedType = NormalizeNullableType(type);
        return normalizedType.Name;
    }

    public static string GetSemanticDisplayName(string? semanticTypeKey)
    {
        return string.IsNullOrWhiteSpace(semanticTypeKey) ? "未指定" : semanticTypeKey;
    }

    public static bool AreSemanticTypesCompatible(string? sourceSemanticTypeKey, string? targetSemanticTypeKey)
    {
        var normalizedSourceKey = NormalizeSemanticTypeKey(sourceSemanticTypeKey);
        var normalizedTargetKey = NormalizeSemanticTypeKey(targetSemanticTypeKey);

        if (string.IsNullOrWhiteSpace(normalizedSourceKey) && string.IsNullOrWhiteSpace(normalizedTargetKey))
        {
            return true;
        }

        return string.Equals(normalizedSourceKey, normalizedTargetKey, StringComparison.OrdinalIgnoreCase);
    }

    private static Type NormalizeNullableType(Type type)
    {
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static string NormalizeSemanticTypeKey(string? semanticTypeKey)
    {
        return semanticTypeKey?.Trim() ?? string.Empty;
    }
}
