using System.Text.Json;

namespace DataSheet.Export;

// 서낭당 고유의 필드·단계·참조 검사만 담당하고 파일 처리와 배포는 기존 공용 경계를 사용합니다.
public sealed class ShrineFileCollector
{
    public IReadOnlyList<OutputFile> Collect(string root, string snapshotFolder, ProjectData data)
    {
        string source = Path.Combine(root, "stats", "shrine");
        if (!Directory.Exists(source)) return [];
        if (!File.Exists(Path.Combine(source, "shrine_level.json")))
            throw new InvalidDataException("서낭당 데이터의 shrine_level.json이 없습니다.");
        var references = data.Tables.Where(table => table.Schema.Name is "Currency" or "Language")
            .ToDictionary(table => table.Schema.Name, table => table.Rows.Select(row => (int)row.Cells["ID"].Value!).ToHashSet());
        void Reference(string table, int id, string file)
        {
            if (!references.TryGetValue(table, out var ids) || !ids.Contains(id))
                throw new InvalidDataException($"서낭당 JSON에서 {table} ID {id}를 찾을 수 없습니다: {file}");
        }
        void Validate(JsonElement value, string file)
        {
            if (Path.GetRelativePath(source, file) == "shrine_level.json")
            {
                int maximum = Integer(value, "maxLevel", 1, file);
                Reference("Currency", Integer(value, "soulCurrencyId", 1, file), file);
                if (Integer(value, "deathSoulLossRate", 0, file) > 10000)
                    throw new InvalidDataException($"서낭당 재화 손실 확률은 10000 이하여야 합니다: {file}");
                Steps(value, "levels", "level", maximum, file, (row, step) =>
                {
                    int cost = Integer(row, "soulCostToNext", 0, file);
                    if ((step == maximum && cost != 0) || (step < maximum && cost == 0))
                        throw new InvalidDataException($"서낭당 마지막 레벨 비용은 0, 이전 레벨 비용은 양수여야 합니다: {file}");
                });
            }
            else
            {
                string name = Text(value, "statType", file);
                if (name != Path.GetFileNameWithoutExtension(file))
                    throw new InvalidDataException($"서낭당 능력치 파일명과 statType이 다릅니다: {file}");
                Reference("Language", Integer(value, "textId", 1, file), file);
                Text(value, "target", file);
                if (!value.TryGetProperty("enabled", out var enabled) || enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new InvalidDataException($"서낭당 enabled는 true 또는 false여야 합니다: {file}");
                int maximum = Integer(value, "maxStep", 1, file);
                Steps(value, "steps", "step", maximum, file, (row, _) => Integer(row, "value", int.MinValue, file));
            }
        }
        return new JsonAssetCollector().Collect(source, OutputCatalog.ShrineFolder,
            snapshotFolder, OutputCatalog.IsShrinePath, Validate);
    }

    private static int Integer(JsonElement value, string field, int minimum, string file)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(field, out var item)
            || item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out int number) || number < minimum)
            throw new InvalidDataException($"서낭당 {field}는 {minimum} 이상의 정수여야 합니다: {file}");
        return number;
    }

    private static string Text(JsonElement value, string field, string file)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(field, out var item)
            || item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            throw new InvalidDataException($"서낭당 {field}에 문자열이 필요합니다: {file}");
        return item.GetString()!;
    }

    private static void Steps(JsonElement value, string field, string key, int maximum, string file, Action<JsonElement, int> validate)
    {
        if (!value.TryGetProperty(field, out var rows) || rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != maximum)
            throw new InvalidDataException($"서낭당 {field} 행 수와 최대 단계가 다릅니다: {file}");
        int expected = 0;
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (Integer(row, key, 1, file) != ++expected)
                throw new InvalidDataException($"서낭당 {key}는 1부터 순서대로 중복 없이 작성해야 합니다: {file}");
            validate(row, expected);
        }
    }
}
