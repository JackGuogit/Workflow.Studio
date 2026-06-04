using System.IO;
using OpenCvSharp;
using Workflow.Studio.Core.Models;
using Workflow.Studio.Core.Services;
using Workflow.Studio.Nodes;
using Workflow.Studio.Nodes.OpenCvImageRead.ViewModels;
using Workflow.Studio.Nodes.OpenCvImageSave.ViewModels;
using Workflow.Studio.Nodes.OpenCvThreshold.ViewModels;
using Workflow.Studio.Plugins.BuiltIn;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class OpenCvWorkflowNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldProcessImagePipelineAndSaveOutput()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("workflow-studio-opencv-test");

        try
        {
            var inputPath = Path.Combine(tempDirectory.FullName, "input.png");
            var outputPath = Path.Combine(tempDirectory.FullName, "output.png");
            CreateSampleImage(inputPath);

            var pluginManager = new PluginManager();
            var codecPlugin = new OpenCvImageCodecPlugin();
            var processingPlugin = new OpenCvImageProcessingPlugin();
            pluginManager.Register(codecPlugin);
            pluginManager.Register(processingPlugin);

            var nodeManager = new NodeManager();
            nodeManager.RegisterType(new OpenCvImageReadNode(codecPlugin));
            nodeManager.RegisterType(new OpenCvGrayscaleNode(processingPlugin));
            nodeManager.RegisterType(new OpenCvThresholdNode(processingPlugin));
            nodeManager.RegisterType(new OpenCvImageSaveNode(codecPlugin));

            var workflow = new WorkflowData();
            var factory = new NodeFactory(nodeManager);

            var readNode = factory.CreateNode(OpenCvImageReadNode.TypeId, 0, 0, "read");
            var grayscaleNode = factory.CreateNode(OpenCvGrayscaleNode.TypeId, 200, 0, "grayscale");
            var thresholdNode = factory.CreateNode(OpenCvThresholdNode.TypeId, 400, 0, "threshold");
            var saveNode = factory.CreateNode(OpenCvImageSaveNode.TypeId, 600, 0, "save");

            ((OpenCvImageReadNodeSettingsViewModel)readNode.Settings!).FilePath = inputPath;
            ((OpenCvThresholdNodeSettingsViewModel)thresholdNode.Settings!).ThresholdValue = 100;
            ((OpenCvThresholdNodeSettingsViewModel)thresholdNode.Settings!).MaxValue = 255;
            ((OpenCvThresholdNodeSettingsViewModel)thresholdNode.Settings!).ThresholdMode = WorkflowThresholdMode.Binary;
            ((OpenCvThresholdNodeSettingsViewModel)thresholdNode.Settings!).AutoConvertToGrayscale = false;
            ((OpenCvImageSaveNodeSettingsViewModel)saveNode.Settings!).FilePath = outputPath;

            workflow.AddNode(readNode);
            workflow.AddNode(grayscaleNode);
            workflow.AddNode(thresholdNode);
            workflow.AddNode(saveNode);

            workflow.Connect("read", "image", "grayscale", "image");
            workflow.Connect("grayscale", "result", "threshold", "image");
            workflow.Connect("threshold", "result", "save", "image");

            var engine = new WorkflowEngine(
                pluginManager,
                nodeManager,
                new WorkflowEventHub(),
                new WorkflowConnectionValidator(),
                new WorkflowDebugController());

            var context = await engine.ExecuteAsync(workflow);

            Assert.True(File.Exists(outputPath));
            Assert.Equal(NodeStatus.Success, readNode.Status);
            Assert.Equal(NodeStatus.Success, grayscaleNode.Status);
            Assert.Equal(NodeStatus.Success, thresholdNode.Status);
            Assert.Equal(NodeStatus.Success, saveNode.Status);
            Assert.Equal(outputPath, context.GlobalVariables["LastImageSavedPath"]);

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
}
