using System.Text;
using System.Text.Json;

namespace DataSheet.Export;

// 별도로 작성한 스폰 원본을 검사한 뒤 기존 배포 목록에 추가합니다.
public sealed class SpawnFileCollector
{
    public IReadOnlyList<OutputFile> Collect(string root, string snapshotFolder)
    {
        string sourceFolder = Path.Combine(root, "spawn");
        if (!Directory.Exists(sourceFolder)) return [];
        var files = new List<OutputFile>();
        foreach (string source in Directory.EnumerateFiles(sourceFolder, "*.json").Order(StringComparer.Ordinal))
        {
            string relative = OutputCatalog.SpawnFolder + "/" + Path.GetFileName(source);
            if (!OutputCatalog.IsSpawnPath(relative)) throw new InvalidDataException($"스폰 파일명은 숫자로 된 스테이지 ID를 사용해 주세요: {source}");
            string text = File.ReadAllText(source, new UTF8Encoding(false, true));
            try
            {
                using JsonDocument document = JsonDocument.Parse(text);
                ValidateMaximum(document.RootElement, source);
            }
            catch (JsonException error) { throw new InvalidDataException($"스폰 JSON 형식이 잘못됐습니다: {source}", error); }
            string snapshot = Path.Combine(snapshotFolder, Path.GetFileName(source));
            File.WriteAllText(snapshot, text, new UTF8Encoding(false));
            files.Add(new OutputFile(relative, snapshot));
        }
        return files;
    }

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
