using Workflow.Studio.Core.Catalog;
using Workflow.Studio.Core.Session;
using Xunit;

namespace Workflow.Studio.Core.Tests;

public sealed class WorkflowSettingMetadataTests
{
    [Fact]
    public void ExtractSettingsFields_ReadsWorkflowSettingAttributes()
    {
        var fields = WorkflowDefinitionRegistry.ExtractSettingsFields(typeof(SampleSettings));

        Assert.Equal(3, fields.Count);
        Assert.Equal("FilePath", fields[0].Key);
        Assert.Equal("文件路径", fields[0].DisplayName);
        Assert.Equal("Text", fields[1].Key);
        Assert.Equal("文本内容", fields[1].DisplayName);
        Assert.Equal(2, fields[2].Options!.Count);
    }

    private sealed class SampleSettings
    {
        public enum SampleMode
        {
            A,
            B
        }

        [WorkflowSetting("文件路径", "file")]
        public string FilePath { get; set; } = string.Empty;

        [WorkflowSetting("文本内容")]
        public string Text { get; set; } = string.Empty;

        [WorkflowSetting("模式", "enum", typeof(SampleMode))]
        public SampleMode Mode { get; set; }
    }
}
