using System.Text.Json;
using Workflow.Studio.Core.Catalog;
using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Workflow.Studio.Nodes.BuiltIn;
using Workflow.Studio.Plugins.BuiltIn;

namespace Workflow.Studio.Cli;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await RunAsync(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"错误: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 2)
        {
            PrintUsage();
            return 2;
        }

        var command = args[0].ToLowerInvariant();
        var filePath = args[1];

        if (command is not ("validate" or "run"))
        {
            PrintUsage();
            return 2;
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"文件不存在: {filePath}");
            return 2;
        }

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? upTo = null;
        string? from = null;

        for (var index = 2; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--var":
                    if (index + 1 >= args.Length)
                    {
                        throw new ArgumentException("--var 需要一个 key=value 参数。");
                    }

                    var pair = args[++index];
                    var separator = pair.IndexOf('=');
                    if (separator <= 0)
                    {
                        throw new ArgumentException($"--var 参数格式应为 key=value: {pair}");
                    }

                    variables[pair[..separator]] = pair[(separator + 1)..];
                    break;
                case "--up-to":
                    upTo = RequireValue(args, ref index, "--up-to");
                    break;
                case "--from":
                    from = RequireValue(args, ref index, "--from");
                    break;
                default:
                    throw new ArgumentException($"未知参数: {argument}");
            }
        }

        if (upTo is not null && from is not null)
        {
            throw new ArgumentException("--up-to 与 --from 不能同时使用。");
        }

        var document = await WorkflowDocumentSerializer.LoadAsync(filePath);
        var documentDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrWhiteSpace(documentDirectory))
        {
            Directory.SetCurrentDirectory(documentDirectory);
        }

        ApplyVariableOverrides(document, variables);

        var registry = BuildRegistry();
        var session = new WorkflowSession(document, registry.CreateResolver());

        return command == "validate"
            ? await ValidateAsync(session)
            : await RunAsync(session, upTo, from);
    }

    private static async Task<int> ValidateAsync(WorkflowSession session)
    {
        session.ConfigureAll();

        var problems = session.Nodes
            .Where(node => node.State != NodeState.Configured)
            .Select(node => new
            {
                node.NodeId,
                node.Path,
                node.NodeTypeId,
                Error = node.LastError ?? node.State.ToString()
            })
            .ToList();

        var payload = new
        {
            ok = problems.Count == 0,
            nodeCount = session.Nodes.Count,
            problems
        };

        Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        return problems.Count == 0 ? 0 : 1;
    }

    private static async Task<int> RunAsync(WorkflowSession session, string? upTo, string? from)
    {
        var executor = new WorkflowExecutor(session);

        WorkflowExecutionResult result = (upTo, from) switch
        {
            (not null, _) => await executor.ExecuteUpToAsync(NormalizeNodeId(upTo)),
            (_, not null) => await executor.ExecuteFromAsync(NormalizeNodeId(from)),
            _ => await executor.ExecuteAllAsync()
        };

        var nodes = session.Nodes
            .OrderBy(node => node.Path, StringComparer.Ordinal)
            .Select(node => new
            {
                node.NodeId,
                node.Path,
                node.NodeTypeId,
                State = node.State.ToString(),
                Error = node.LastError,
                OutputVariables = node.ProducedFlowVariables
            })
            .ToList();

        var payload = new
        {
            ok = !result.HasFailures,
            canceled = result.IsCanceled,
            executed = result.ExecutedNodeIds,
            failed = result.FailedNodeIds,
            blocked = result.BlockedNodeIds,
            canceledNodes = result.CanceledNodeIds,
            nodes
        };

        Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        return result.HasFailures ? 1 : 0;
    }

    private static WorkflowDefinitionRegistry BuildRegistry()
    {
        var codec = new OpenCvImageCodecPlugin();
        var processing = new OpenCvImageProcessingPlugin();
        var registry = new WorkflowDefinitionRegistry();
        BuiltInNodeCatalog.RegisterAll(registry, codec, processing);
        return registry;
    }

    private static void ApplyVariableOverrides(WorkflowDocument document, IReadOnlyDictionary<string, string> overrides)
    {
        if (overrides.Count == 0)
        {
            return;
        }

        var valueTypes = ValueTypeRegistry.CreateDefault();

        foreach (var declaration in document.VariableDeclarations)
        {
            if (!overrides.TryGetValue(declaration.Name, out var rawValue))
            {
                continue;
            }

            var payloadType = valueTypes.Get(declaration.TypeId).PayloadType;
            declaration.DefaultValue = ConvertRawValue(rawValue, payloadType);
        }

        var unknown = overrides.Keys
            .Where(name => document.VariableDeclarations.All(declaration =>
                !string.Equals(declaration.Name, name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (unknown.Count > 0)
        {
            throw new ArgumentException($"未知入口变量: {string.Join(", ", unknown)}。");
        }
    }

    private static object? ConvertRawValue(string rawValue, Type payloadType)
    {
        if (payloadType == typeof(string))
        {
            return rawValue;
        }

        if (payloadType == typeof(long))
        {
            return long.Parse(rawValue);
        }

        if (payloadType == typeof(double))
        {
            return double.Parse(rawValue);
        }

        if (payloadType == typeof(bool))
        {
            return bool.Parse(rawValue);
        }

        throw new ArgumentException($"暂不支持入口变量类型 '{payloadType.Name}' 的命令行覆盖。");
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} 需要一个值。");
        }

        return args[++index];
    }

    private static string NormalizeNodeId(string nodePath)
    {
        return nodePath.TrimStart('/');
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            用法:
              ws validate <workflow.json>
              ws run <workflow.json> [--var key=value]... [--up-to <nodePath> | --from <nodePath>]
            """);
    }
}
