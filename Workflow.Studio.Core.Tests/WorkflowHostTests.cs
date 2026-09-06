using System.IO;
using OpenCvSharp;
using Workflow.Studio.Core.Documents;
using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Session;
using Workflow.Studio.Nodes.BuiltIn;
using Workflow.Studio.Plugins.BuiltIn;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowHostTests
{
    [Fact]
    public async Task DefaultDemo_TextChain_ExecutesThroughRegistry()
    {
        var registry = new WorkflowDefinitionRegistry();
        registry.Register("demo.node.text-source", new TextSourceNode());
        registry.Register("demo.node.uppercase-transform", new UppercaseTransformNode());
        registry.Register("demo.node.preview", new PreviewNode());

        var document = new WorkflowDocument();
        document.Nodes.Add(CreateNode("node-source", "demo.node.text-source"));
        document.Nodes.Add(CreateNode("node-transform", "demo.node.uppercase-transform"));
        document.Nodes.Add(CreateNode("node-preview", "demo.node.preview"));
        document.Nodes[0].Settings["text"] = "DevWorkflow Studio";
        document.Connections.Add(Connect("node-source", "content", "node-transform", "incoming"));
        document.Connections.Add(Connect("node-transform", "result", "node-preview", "incoming"));

        var session = new WorkflowSession(document, registry.CreateResolver());
        var executor = new WorkflowExecutor(session, maxConcurrency: 2);

        var result = await executor.ExecuteAllAsync();

        Assert.False(result.HasFailures);
        Assert.True(session.GetNode("node-preview").TryReadOutputValue("preview-text", out var preview));
        Assert.Equal("DEVWORKFLOW STUDIO", preview);
    }

    [Fact]
    public async Task OpenCvPipeline_ExecutesThroughRegistry()
    {
        var codecPlugin = new OpenCvImageCodecPlugin();
        var processingPlugin = new OpenCvImageProcessingPlugin();

        var registry = new WorkflowDefinitionRegistry();
        registry.Register("opencv.node.image-read", new OpenCvImageReadNode(codecPlugin));
        registry.Register("opencv.node.grayscale", new OpenCvGrayscaleNode(processingPlugin));
        registry.Register("opencv.node.threshold", new OpenCvThresholdNode(processingPlugin));
        registry.Register("opencv.node.image-save", new OpenCvImageSaveNode(codecPlugin));

        var tempDirectory = Directory.CreateTempSubdirectory("workflow-studio-v2-opencv");

        try
        {
            var inputPath = Path.Combine(tempDirectory.FullName, "input.png");
            var outputPath = Path.Combine(tempDirectory.FullName, "output.png");
            CreateSampleImage(inputPath);

            var document = new WorkflowDocument();
            document.Nodes.Add(CreateNode("read", "opencv.node.image-read"));
            document.Nodes.Add(CreateNode("grayscale", "opencv.node.grayscale"));
            document.Nodes.Add(CreateNode("threshold", "opencv.node.threshold"));
            document.Nodes.Add(CreateNode("save", "opencv.node.image-save"));
            document.Nodes[0].Settings["filePath"] = inputPath;
            document.Nodes[0].Settings["readMode"] = WorkflowImageReadMode.Color;
            document.Nodes[2].Settings["thresholdValue"] = (byte)100;
            document.Nodes[2].Settings["maxValue"] = (byte)255;
            document.Nodes[2].Settings["thresholdMode"] = WorkflowThresholdMode.Binary;
            document.Nodes[2].Settings["autoConvertToGrayscale"] = false;
            document.Nodes[3].Settings["filePath"] = outputPath;
            document.Connections.Add(Connect("read", "image", "grayscale", "image"));
            document.Connections.Add(Connect("grayscale", "result", "threshold", "image"));
            document.Connections.Add(Connect("threshold", "result", "save", "image"));

            var session = new WorkflowSession(document, registry.CreateResolver());
            var executor = new WorkflowExecutor(session, maxConcurrency: 2);

            var result = await executor.ExecuteAllAsync();

            Assert.False(result.HasFailures);
            Assert.True(File.Exists(outputPath));
            Assert.Equal(NodeState.Succeeded, session.GetNode("save").State);
            Assert.Equal(outputPath, session.GetNode("save").ProducedFlowVariables!["lastImageSavedPath"]);

            using var outputImage = Cv2.ImRead(outputPath, ImreadModes.Unchanged);
            Assert.False(outputImage.Empty());
            Assert.Equal(1, outputImage.Channels());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    private static void CreateSampleImage(string outputPath)
    {
        using var image = new Mat(new Size(24, 24), MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(image, new Rect(4, 4, 16, 16), new Scalar(255, 255, 255), -1);
        Cv2.ImWrite(outputPath, image);
    }

    private static NodeDocument CreateNode(string nodeId, string nodeTypeId)
    {
        return new NodeDocument
        {
            NodeId = nodeId,
            NodeTypeId = nodeTypeId
        };
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
