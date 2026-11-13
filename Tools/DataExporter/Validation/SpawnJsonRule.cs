using System.Text.Json;

namespace DataSheet.Export;

// 별도로 작성한 스폰 원본을 검사한 뒤 기존 배포 목록에 추가합니다.
public class SpawnJsonRule : JsonAssetRule
{
    public override string SourceFolder => "spawn";
    protected override JsonAssetValidation Prepare(string sourceFolder, ProjectData data) => new((value, source) =>
    {
        string name = Path.GetRelativePath(sourceFolder, source);
        if (!name.EndsWith(".json", StringComparison.Ordinal) || name[..^5].Length == 0
            || !name[..^5].All(char.IsAsciiDigit))
            throw new InvalidDataException("스폰 파일명은 숫자와 .json으로 작성해 주세요: " + source);
        ValidateMaximum(value, source);
        new SpawnObjectValidator().Validate(value, source, data);
    });

    private static void ValidateMaximum(JsonElement root, string source)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("MaxMonsterCount", out JsonElement limit)
            || limit.ValueKind != JsonValueKind.Number || !limit.TryGetInt32(out int maximum) || maximum < 0)
            throw new InvalidDataException($"스폰 JSON의 MaxMonsterCount에 0 이상의 정수를 입력해 주세요: {source}");
        long total = 0;
        if (root.TryGetProperty("Placements", out JsonElement placements) && placements.ValueKind == JsonValueKind.Array)
            foreach (JsonElement placement in placements.EnumerateArray())
            {
                if (!placement.TryGetProperty("Monsters", out JsonElement groups) || groups.ValueKind != JsonValueKind.Array) continue;
                foreach (JsonElement group in groups.EnumerateArray())
                {
                    if (!group.TryGetProperty("MonsterCount", out JsonElement count) || count.ValueKind != JsonValueKind.Number
                        || !count.TryGetInt32(out int requested) || requested < 0)
                        throw new InvalidDataException($"스폰 JSON의 MonsterCount에 0 이상의 정수를 입력해 주세요: {source}");
                    // 수량과 위치가 어긋나도 초과 배치가 숨겨지지 않도록 큰 쪽으로 상한을 확인합니다.
                    int points = group.TryGetProperty("Points", out JsonElement positions) && positions.ValueKind == JsonValueKind.Array
                        ? positions.GetArrayLength() : 0;
                    total += Math.Max(requested, points);
                }
            }
        if (total > maximum)
            throw new InvalidDataException($"스폰 JSON의 배치 수 {total}마리가 MaxMonsterCount {maximum}마리를 초과합니다: {source}");
    }
}
