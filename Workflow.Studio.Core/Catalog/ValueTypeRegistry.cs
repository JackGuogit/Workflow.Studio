using System;
using System.Collections.Generic;
using System.Linq;

namespace Workflow.Studio.Core.Catalog;

/// <summary>
/// 值类型注册表（进程级、只读使用）。V2 中端口声明、流变量、变量映射
/// 全部引用这里的 TypeId。
/// </summary>
public sealed class ValueTypeRegistry
{
    private readonly Dictionary<string, ValueTypeDefinition> _definitions;

    public ValueTypeRegistry(IEnumerable<ValueTypeDefinition>? initial = null)
    {
        _definitions = new Dictionary<string, ValueTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        if (initial is not null)
        {
            foreach (var definition in initial)
            {
                Register(definition);
            }
        }
    }

    public static ValueTypeRegistry CreateDefault()
    {
        return new ValueTypeRegistry(BuiltInValueTypes.All);
    }

    public void Register(ValueTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (_definitions.ContainsKey(definition.TypeId))
        {
            throw new InvalidOperationException($"Value type '{definition.TypeId}' is already registered.");
        }

        _definitions.Add(definition.TypeId, definition);
    }

    public bool TryGet(string typeId, out ValueTypeDefinition? definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        return _definitions.TryGetValue(typeId, out definition);
    }

    public ValueTypeDefinition Get(string typeId)
    {
        if (TryGet(typeId, out var definition) && definition is not null)
        {
            return definition;
        }

        throw new KeyNotFoundException($"Value type '{typeId}' was not found.");
    }

    public IReadOnlyList<ValueTypeDefinition> All => _definitions.Values
        .OrderBy(definition => definition.TypeId, StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>
    /// V2 兼容规则：源 TypeId 与目标 TypeId 精确相等，或目标为 meta/any。
    /// </summary>
    public bool AreCompatible(string sourceTypeId, string targetTypeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTypeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTypeId);

        if (string.Equals(sourceTypeId, targetTypeId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(targetTypeId, ValueTypeIds.Any, StringComparison.OrdinalIgnoreCase);
    }
}
