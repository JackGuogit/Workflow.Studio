using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Workflow.Studio.Core.Session;
using Workflow.Studio.Nodes.Common;

namespace Workflow.Studio.Nodes.BuiltIn;

/// <summary>
/// 内置文本类节点的契约实现（内置节点迁移切片）。
/// 端口类型使用 TypeId；设置以字典承载（类型化设置 POCO 后续叠加）。
/// </summary>
public sealed class TextSourceNode : INodeDefinition
{
    public IReadOnlyList<NodePortDefinition> InputPorts => [];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
        [new NodePortDefinition("content", "text/plain", DisplayName: "内容")];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?> { ["content"] = "text/plain" }
        };
    }

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var text = SettingReader.GetString(request.Settings, "text", string.Empty);

        return Task.FromResult(new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?> { ["content"] = text }
        });
    }
}

public sealed class CsvReadNode : INodeDefinition
{
    public IReadOnlyList<NodePortDefinition> InputPorts => [];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
    [
        new NodePortDefinition("csv-content", "text/csv", DisplayName: "CSV 内容"),
        new NodePortDefinition("source-path", "path/file", DisplayName: "源路径")
    ];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?>
            {
                ["csv-content"] = "text/csv",
                ["source-path"] = "path/file"
            }
        };
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var filePath = SettingReader.GetString(request.Settings, "filePath", "sample-data.csv");
        var fullPath = Path.GetFullPath(filePath);
        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);

        return new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?>
            {
                ["csv-content"] = content,
                ["source-path"] = fullPath
            }
        };
    }
}

public sealed class UppercaseTransformNode : INodeDefinition
{
    public IReadOnlyList<NodePortDefinition> InputPorts =>
        [new NodePortDefinition("incoming", "text/plain", DisplayName: "输入")];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
        [new NodePortDefinition("result", "text/plain", DisplayName: "结果")];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables =>
        [new FlowVariableDeclaration("lastTransform", "text/plain")];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?> { ["result"] = "text/plain" }
        };
    }

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var input = SettingReader.GetInputString(request.InputValues, "incoming");
        var result = input.ToUpperInvariant();

        return Task.FromResult(new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?> { ["result"] = result },
            OutputVariables = new Dictionary<string, object?> { ["lastTransform"] = result }
        });
    }
}

public sealed class CsvToTsvTransformNode : INodeDefinition
{
    private readonly CsvToTsvConverter _converter = new();

    public IReadOnlyList<NodePortDefinition> InputPorts =>
        [new NodePortDefinition("incoming", "text/csv", DisplayName: "CSV 输入")];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
        [new NodePortDefinition("result", "text/tsv", DisplayName: "TSV 结果")];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?> { ["result"] = "text/tsv" }
        };
    }

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var csvContent = SettingReader.GetInputString(request.InputValues, "incoming");
        var tsvContent = _converter.Convert(csvContent);

        return Task.FromResult(new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?> { ["result"] = tsvContent }
        });
    }
}

public sealed class PreviewNode : INodeDefinition
{
    public IReadOnlyList<NodePortDefinition> InputPorts =>
        [new NodePortDefinition("incoming", "text/plain", DisplayName: "输入")];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
        [new NodePortDefinition("preview-text", "text/plain", DisplayName: "预览文本")];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables => [];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?> { ["preview-text"] = "text/plain" }
        };
    }

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var input = SettingReader.GetInputString(request.InputValues, "incoming");
        return Task.FromResult(new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?> { ["preview-text"] = input }
        });
    }
}

public sealed class TsvSaveNode : INodeDefinition
{
    public IReadOnlyList<NodePortDefinition> InputPorts =>
        [new NodePortDefinition("incoming", "text/tsv", DisplayName: "TSV 输入")];

    public IReadOnlyList<NodePortDefinition> OutputPorts =>
        [new NodePortDefinition("saved-path", "path/file", DisplayName: "保存路径")];

    public IReadOnlyList<FlowVariableDeclaration> OutputVariables =>
        [new FlowVariableDeclaration("lastTsvSavedPath", "path/file")];

    public NodeConfigureResult Configure(NodeConfigureRequest request)
    {
        return new NodeConfigureResult
        {
            OutputSpecs = new Dictionary<string, object?> { ["saved-path"] = "path/file" }
        };
    }

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var content = SettingReader.GetInputString(request.InputValues, "incoming");
        var filePath = SettingReader.GetString(request.Settings, "filePath", "output-data.tsv");
        var fullPath = Path.GetFullPath(filePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);

        return Task.FromResult(new NodeExecutionResult
        {
            OutputValues = new Dictionary<string, object?> { ["saved-path"] = fullPath },
            OutputVariables = new Dictionary<string, object?> { ["lastTsvSavedPath"] = fullPath }
        });
    }
}

internal static class SettingReader
{
    public static string GetString(IReadOnlyDictionary<string, object?> settings, string key, string defaultValue)
    {
        return settings.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value) ?? defaultValue
            : defaultValue;
    }

    public static string GetInputString(IReadOnlyDictionary<string, object?> inputValues, string key)
    {
        return inputValues.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value) ?? string.Empty
            : string.Empty;
    }

    public static bool GetBool(IReadOnlyDictionary<string, object?> settings, string key, bool defaultValue)
    {
        return settings.TryGetValue(key, out var value) && value is not null
            ? Convert.ToBoolean(value)
            : defaultValue;
    }

    public static byte GetByte(IReadOnlyDictionary<string, object?> settings, string key, byte defaultValue)
    {
        if (settings.TryGetValue(key, out var value) && value is not null)
        {
            return value switch
            {
                byte byteValue => byteValue,
                _ => Convert.ToByte(value)
            };
        }

        return defaultValue;
    }

    public static TEnum GetEnum<TEnum>(IReadOnlyDictionary<string, object?> settings, string key, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        if (!settings.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        if (value is TEnum enumValue)
        {
            return enumValue;
        }

        if (value is string text && Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        if (value is long number)
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), number);
        }

        return defaultValue;
    }
}
