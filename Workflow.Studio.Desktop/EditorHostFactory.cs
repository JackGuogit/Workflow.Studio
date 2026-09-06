using System;
using System.Collections.Generic;
using System.IO;
using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Session;
using Workflow.Studio.Nodes.BuiltIn;
using Workflow.Studio.Plugins.BuiltIn;

namespace Workflow.Studio.Desktop;

internal static class EditorHostFactory
{
    public static WorkflowDefinitionRegistry BuildRegistry()
    {
        var codec = new OpenCvImageCodecPlugin();
        var processing = new OpenCvImageProcessingPlugin();
        var registry = new WorkflowDefinitionRegistry();
        BuiltInNodeCatalog.RegisterAll(registry, codec, processing);
        return registry;
    }

    public static IReadOnlyList<string> LoadExtensionNodes(WorkflowDefinitionRegistry registry)
    {
        var directories = new List<string>();

        var deployedExtensions = Path.Combine(AppContext.BaseDirectory, "extensions");
        if (Directory.Exists(deployedExtensions))
        {
            directories.Add(deployedExtensions);
        }

        // 本地开发便利：直接加载 SDK 示例产物，便于在节点库中看到扩展节点。
        var devSample = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "SampleSdkNode", "bin", "Debug", "net10.0"));
        if (Directory.Exists(devSample))
        {
            directories.Add(devSample);
        }

        var loaded = new List<string>();
        foreach (var directory in directories)
        {
            try
            {
                loaded.AddRange(ExternalNodeLoader.LoadFromDirectory(registry, directory));
            }
            catch
            {
                // 目录不可用时静默跳过，不影响启动。
            }
        }

        return loaded;
    }

    public static WorkflowDocument CreateDemoDocument()
    {
        var document = new WorkflowDocument();
        document.VariableDeclarations.Add(new VariableDeclaration
        {
            Name = "seedText",
            TypeId = "text/plain",
            DefaultValue = "DevWorkflow Studio"
        });

        var source = new NodeDocument
        {
            NodeId = "node-source",
            NodeTypeId = "demo.node.text-source",
            X = 80,
            Y = 100
        };
        source.SettingsBindings.Add(new SettingsBinding
        {
            Setting = "text",
            Variable = "seedText"
        });

        document.Nodes.Add(source);
        document.Nodes.Add(new NodeDocument
        {
            NodeId = "node-transform",
            NodeTypeId = "demo.node.uppercase-transform",
            X = 360,
            Y = 100
        });
        document.Nodes.Add(new NodeDocument
        {
            NodeId = "node-preview",
            NodeTypeId = "demo.node.preview",
            X = 640,
            Y = 100
        });
        document.Connections.Add(Connect("node-source", "content", "node-transform", "incoming"));
        document.Connections.Add(Connect("node-transform", "result", "node-preview", "incoming"));
        return document;
    }

    private static ConnectionDocument Connect(string sourceNodeId, string sourcePortId, string targetNodeId, string targetPortId)
    {
        return new ConnectionDocument
        {
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId
        };
    }
}
