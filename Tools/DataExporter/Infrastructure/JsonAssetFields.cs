using System.Text.Json;

namespace DataSheet.Export;

// 정의 JSON 사이에서 반복되는 필드·참조 검사를 공유합니다. 기능별 규칙은 각 수집기에 둡니다.
public static class JsonAssetFields
{
    public static int Integer(JsonElement value, string field, int minimum, string file)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(field, out var item)
            || item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out int number) || number < minimum)
            throw new InvalidDataException($"JSON {field}는 {minimum} 이상의 정수여야 합니다: {file}");
        return number;
    }
    public static string Text(JsonElement value, string field, string file)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(field, out var item)
            || item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            throw new InvalidDataException($"JSON {field}에 문자열이 필요합니다: {file}");
        return item.GetString()!;
    }
    public static bool Boolean(JsonElement value, string field, string file)
    {
        if (!value.TryGetProperty(field, out var item) || item.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException($"JSON {field}는 true 또는 false여야 합니다: {file}");
        return item.GetBoolean();
    }
    public static JsonElement Rows(JsonElement value, string field, string file)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(field, out var rows) || rows.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"JSON {field}에는 배열이 필요합니다: {file}");
        return rows;
    }
    public static Action<string, int, string> References(ProjectData data, params string[] tables)
    {
        var references = data.Tables.Where(table => tables.Contains(table.Schema.Name))
            .ToDictionary(table => table.Schema.Name, table => table.Rows.Select(row => (int)row.Cells["ID"].Value!).ToHashSet());
        return (table, id, file) =>
        {
            if (!references.TryGetValue(table, out var ids) || !ids.Contains(id))
                throw new InvalidDataException($"JSON에서 {table} ID {id}를 찾을 수 없습니다: {file}");
        };
    }
}
