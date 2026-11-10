using System.Text;
using System.Text.Json;

namespace DataSheet.Export;

// 원본 JSON의 파일 수집·문법 검사·배포용 사본 생성을 공용으로 처리합니다.
public sealed class JsonAssetCollector
{
    public IReadOnlyList<OutputFile> Collect(string sourceFolder, string outputFolder, string snapshotFolder,
        Func<string, bool> allowsPath, Action<JsonElement, string> validate)
    {
        if (!Directory.Exists(sourceFolder)) return [];
        var files = new List<OutputFile>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string source in Directory.EnumerateFiles(sourceFolder, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string name = Path.GetRelativePath(sourceFolder, source).Replace('\\', '/');
            string relative = outputFolder + "/" + name;
            if (!allowsPath(relative) || !paths.Add(relative))
                throw new InvalidDataException($"JSON 배포 경로가 잘못됐거나 중복됩니다: {source}");
            byte[] bytes = File.ReadAllBytes(source);
            // UTF-8 BOM만 허용하며 다른 인코딩을 자동 감지해 통과시키지 않습니다.
            int offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
            string text = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
            try
            {
                using JsonDocument document = JsonDocument.Parse(text);
                validate(document.RootElement, source);
            }
            catch (JsonException error) { throw new InvalidDataException($"JSON 형식이 잘못됐습니다: {source}", error); }
            string snapshot = Path.Combine(snapshotFolder, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
            File.WriteAllText(snapshot, text, new UTF8Encoding(false));
            files.Add(new OutputFile(relative, snapshot));
        }
        return files;
    }
}
