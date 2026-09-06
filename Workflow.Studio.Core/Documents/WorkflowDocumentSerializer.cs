using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Workflow.Studio.Core.Documents;

/// <summary>
/// Document v2 的 JSON 读写（V2 决策 D19：单 JSON、无 v1 迁移）。
/// 加载/保存前执行结构校验。
/// </summary>
public static class WorkflowDocumentSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static string Serialize(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        WorkflowDocumentValidator.EnsureValid(document);
        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    public static WorkflowDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var document = JsonSerializer.Deserialize<WorkflowDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Unable to read workflow document content.");

        WorkflowDocumentValidator.EnsureValid(document);
        return document;
    }

    public static async Task SaveAsync(WorkflowDocument document, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var json = Serialize(document);
        var directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    public static async Task<WorkflowDocument> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Deserialize(json);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        options.Converters.Add(new JsonObjectValueConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

/// <summary>
/// 让 Dictionary&lt;string, object?&gt;（设置字段、变量默认值）以运行时类型写出、
/// 以 .NET 基元读回的转换器。
/// </summary>
public sealed class JsonObjectValueConverter : JsonConverter<object?>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(object);
    }

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return ConvertElement(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var runtimeType = value.GetType();
        if (runtimeType == typeof(object))
        {
            // 无运行时类型信息的装箱对象：退化为空对象。
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        JsonSerializer.Serialize(writer, value, runtimeType, options);
    }

    private static object? ConvertElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();
            case JsonValueKind.Number:
                return element.TryGetInt64(out var int64Value)
                    ? int64Value
                    : element.GetDouble();
            case JsonValueKind.Array:
            {
                var array = new object?[element.GetArrayLength()];
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    array[index++] = ConvertElement(item);
                }

                return array;
            }
            case JsonValueKind.Object:
            {
                var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    dictionary[property.Name] = ConvertElement(property.Value);
                }

                return dictionary;
            }
            default:
                return element.Clone();
        }
    }
}
