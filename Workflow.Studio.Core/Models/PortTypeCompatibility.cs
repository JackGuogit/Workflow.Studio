namespace Workflow.Studio.Core.Models;

public static class PortTypeCompatibility
{
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

        throw new InvalidOperationException(
            $"{context} 期望数据类型为 '{GetDisplayName(declaredType)}'，但实际收到 '{GetDisplayName(value.GetType())}'。");
    }

    public static string GetDisplayName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var normalizedType = NormalizeNullableType(type);
        return normalizedType.Name;
    }

    private static Type NormalizeNullableType(Type type)
    {
        return Nullable.GetUnderlyingType(type) ?? type;
    }
}
