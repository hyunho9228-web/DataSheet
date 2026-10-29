using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace DataSheet.Export;

public sealed class CsvExporter : ITableSerializer
{
    public string Extension => ".csv";
    public void Write(TypedTable table, string path)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false)) { NewLine = "\n" };
        string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        writer.WriteLine(string.Join(',', table.Schema.Columns.Select(column => Escape(column.Name))));
        foreach (TypedRow row in table.Rows) writer.WriteLine(string.Join(',', table.Schema.Columns.Select(column => Escape(CellValueConverter.Format(row.Cells[column.Name].Value)))));
    }
    public IReadOnlyList<object?[]> Read(TableSchema schema, string path)
    {
        using var parser = new TextFieldParser(path, new UTF8Encoding(false, true)) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        if (!(parser.ReadFields() ?? []).SequenceEqual(schema.Columns.Select(column => column.Name))) throw new InvalidDataException("CSV 헤더 불일치");
        var rows = new List<object?[]>();
        while (!parser.EndOfData)
        {
            string[] fields = parser.ReadFields() ?? [];
            if (fields.Length != schema.Columns.Count) throw new InvalidDataException("CSV 열 수 불일치");
            rows.Add(fields.Select((text, index) => CellValueConverter.Parse(text, schema.Columns[index])).ToArray());
        }
        return rows;
    }
}
