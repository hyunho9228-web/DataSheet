using System.Text.Json;
using static DataSheet.Export.JsonAssetFields;

namespace DataSheet.Export;

// 서낭당 고유의 필드·단계·참조 검사만 담당하고 파일 처리와 배포는 기존 공용 경계를 사용합니다.
public class ShrineJsonRule : JsonAssetRule
{
    public override string SourceFolder => "stats/shrine";
    protected override JsonAssetValidation Prepare(string source, ProjectData data)
    {
        if (!Directory.Exists(source)) return new((_, _) => { });
        if (!File.Exists(Path.Combine(source, "shrine_level.json")))
            throw new InvalidDataException("필수 JSON이 없습니다: " + Path.Combine(source, "shrine_level.json"));
        var reference = References(data, "Currency", "Language");
        void Validate(JsonElement value, string file)
        {
            if (Path.GetRelativePath(source, file) == "shrine_level.json")
            {
                int maximum = Integer(value, "maxLevel", 1, file);
                Integer(value, "soulCostBase", 1, file);
                Integer(value, "soulCostRate", 10000, file);
                reference("Currency", Integer(value, "soulCurrencyId", 1, file), file);
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
                reference("Language", Integer(value, "textId", 1, file), file);
                Text(value, "target", file);
                Boolean(value, "enabled", file);
                int maximum = Integer(value, "maxStep", 1, file);
                Steps(value, "steps", "step", maximum, file, (row, _) => Integer(row, "value", int.MinValue, file));
            }
        }
        return new((value, file) =>
        {
            string name = Path.GetRelativePath(source, file).Replace('\\', '/');
            if (name != "shrine_level.json" && !(name.StartsWith("stat/", StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.Ordinal) && NamePolicy.IsStem(name[5..^5])))
                throw new InvalidDataException("서낭당 JSON 경로를 확인해 주세요: " + file);
            Validate(value, file);
        });
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
