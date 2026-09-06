using System;
using System.Collections.Generic;
using Workflow.Studio.Core.Models;

namespace Workflow.Studio.Core.Catalog;

/// <summary>
/// V2 内置值类型 TypeId 常量。
/// </summary>
public static class ValueTypeIds
{
    public const string PlainText = "text/plain";
    public const string CsvText = "text/csv";
    public const string TsvText = "text/tsv";
    public const string FilePath = "path/file";
    public const string ImageFrame = "image/frame";
    public const string Int64 = "scalar/int64";
    public const string Double = "scalar/double";
    public const string Boolean = "scalar/bool";

    /// <summary>仅允许作为目标端口的通配类型（接收任意源类型）。</summary>
    public const string Any = "meta/any";
}

/// <summary>
/// Core 内置值类型清单（V2 架构文档 3.1 节）。
/// </summary>
public static class BuiltInValueTypes
{
    public static IReadOnlyList<ValueTypeDefinition> All { get; } =
    [
        new ValueTypeDefinition
        {
            TypeId = ValueTypeIds.PlainText,
            DisplayName = "文本 (text/plain)",
            PayloadType = typeof(string),
            CanBeFlowVariable = true
        },
        new ValueTypeDefinition
        {
            TypeId = ValueTypeIds.CsvText,
            DisplayName = "CSV 文本",
            PayloadType = typeof(string),
            CanBeFlowVariable = true
        },
        new ValueTypeDefinition
        {
            TypeId = ValueTypeIds.TsvText,
            DisplayName = "TSV 文本",
            PayloadType = typeof(string),
            CanBeFlowVariable = true
        },
        new ValueTypeDefinition
        {
            TypeId = ValueTypeIds.FilePath,
            DisplayName = "文件路径",
            PayloadType = typeof(string),
            CanBeFlowVariable = true
        },
        new ValueTypeDefinition
        {
            TypeId = ValueTypeIds.ImageFrame,
            DisplayName = "图像帧 (image/frame)",
            PayloadType = typeof(WorkflowImageFrame),
            CanBeFlowVariable = false
        },
        new ValueTypeDefinition
        {
            TypeId = ValueTypeIds.Int64,
            DisplayName = "整数 (int64)",
            PayloadType = typeof(long),
            CanBeFlowVariable = true
        },
        new ValueTypeDefinition
        {
            TypeId = ValueTypeIds.Double,
            DisplayName = "浮点 (double)",
            PayloadType = typeof(double),
            CanBeFlowVariable = true
        },
        new ValueTypeDefinition
        {
            TypeId = ValueTypeIds.Boolean,
            DisplayName = "布尔 (bool)",
            PayloadType = typeof(bool),
            CanBeFlowVariable = true
        },
        new ValueTypeDefinition
        {
            TypeId = ValueTypeIds.Any,
            DisplayName = "任意类型（仅目标端口）",
            PayloadType = typeof(object),
            CanBeFlowVariable = false
        }
    ];
}
