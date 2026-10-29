using System.Text.Json;

namespace DataSheet.Export;

public sealed class JsonExporter : ITableSerializer
{
    public string Extension => ".json";
    public void Write(TypedTable table, string path)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        void WriteRow(TypedRow row)
        {
            writer.WriteStartObject();
            foreach (ColumnSchema column in table.Schema.Columns)
            {
                writer.WritePropertyName(column.Name);
                JsonSerializer.Serialize(writer, row.Cells[column.Name].Value);
            }
            writer.WriteEndObject();
        }
        writer.WriteStartArray();
        if (table.Schema.JsonGroupColumn is string group)
        {
            foreach (var rows in table.Rows.GroupBy(row => row.Cells[group].Value))
            {
                writer.WriteStartObject(); writer.WritePropertyName(group); JsonSerializer.Serialize(writer, rows.Key);
                writer.WriteStartArray("States"); foreach (TypedRow row in rows) WriteRow(row); writer.WriteEndArray(); writer.WriteEndObject();
            }
        }
        else foreach (TypedRow row in table.Rows) WriteRow(row);
        writer.WriteEndArray();
    }
    public IReadOnlyList<object?[]> Read(TableSchema schema, string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        var rows = new List<object?[]>();
        void ReadRow(JsonElement row, JsonElement? group)
        {
            string[] names = row.EnumerateObject().Select(property => property.Name).ToArray();
            if (!names.SequenceEqual(schema.Columns.Select(column => column.Name))) throw new InvalidDataException("JSON 속성 불일치");
            if (group is JsonElement key && row.GetProperty(schema.JsonGroupColumn!).GetRawText() != key.GetRawText()) throw new InvalidDataException("JSON 그룹 ID 불일치");
            rows.Add(schema.Columns.Select(column =>
            {
                JsonElement value = row.GetProperty(column.Name);
                if (value.ValueKind == JsonValueKind.Null && column.Optional) return null;
                return column.Type switch
                {
                    FieldType.Integer => (object)value.GetInt32(), FieldType.Flag => value.GetBoolean(), FieldType.Text => value.GetString(),
                    _ => throw new InvalidDataException("JSON 타입 오류")
                };
            }).ToArray());
        }
        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            if (schema.JsonGroupColumn is string column)
            {
                if (!element.EnumerateObject().Select(property => property.Name).SequenceEqual([column, "States"])) throw new InvalidDataException("JSON 그룹 속성 불일치");
                foreach (JsonElement row in element.GetProperty("States").EnumerateArray()) ReadRow(row, element.GetProperty(column));
            }
            else ReadRow(element, null);
        }
        return rows;
    }
}
