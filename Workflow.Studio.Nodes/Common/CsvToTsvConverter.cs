using System.Text;

namespace Workflow.Studio.Nodes.Common;

public sealed class CsvToTsvConverter
{
    public string Convert(string csvContent)
    {
        ArgumentNullException.ThrowIfNull(csvContent);

        var rows = ParseCsvRows(csvContent);
        return BuildTsv(rows);
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseCsvRows(string csvContent)
    {
        var rows = new List<IReadOnlyList<string>>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < csvContent.Length; index++)
        {
            var currentCharacter = csvContent[index];

            if (insideQuotes)
            {
                if (currentCharacter == '"')
                {
                    var nextIndex = index + 1;
                    if (nextIndex < csvContent.Length && csvContent[nextIndex] == '"')
                    {
                        currentField.Append('"');
                        index++;
                    }
                    else
                    {
                        insideQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(currentCharacter);
                }

                continue;
            }

            switch (currentCharacter)
            {
                case '"':
                    insideQuotes = true;
                    break;
                case ',':
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    break;
                case '\r':
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow.ToArray());
                    currentRow = [];

                    if (index + 1 < csvContent.Length && csvContent[index + 1] == '\n')
                    {
                        index++;
                    }

                    break;
                case '\n':
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow.ToArray());
                    currentRow = [];
                    break;
                default:
                    currentField.Append(currentCharacter);
                    break;
            }
        }

        if (insideQuotes)
        {
            throw new FormatException("CSV 内容格式无效，存在未闭合的引号。");
        }

        if (currentField.Length > 0 || currentRow.Count > 0 || csvContent.Length == 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow.ToArray());
        }

        return rows;
    }

    private static string BuildTsv(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];

            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                if (columnIndex > 0)
                {
                    builder.Append('\t');
                }

                builder.Append(EscapeTsvField(row[columnIndex]));
            }

            if (rowIndex < rows.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static string EscapeTsvField(string value)
    {
        if (!value.Contains('\t') && !value.Contains('\r') && !value.Contains('\n') && !value.Contains('"'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
