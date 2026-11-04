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
            try { using JsonDocument document = JsonDocument.Parse(text); }
            catch (JsonException error) { throw new InvalidDataException($"스폰 JSON 형식이 잘못됐습니다: {source}", error); }
            string snapshot = Path.Combine(snapshotFolder, Path.GetFileName(source));
            File.WriteAllText(snapshot, text, new UTF8Encoding(false));
            files.Add(new OutputFile(relative, snapshot));
        }
        return files;
    }
}
