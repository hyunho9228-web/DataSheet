namespace DataSheet.Export;

// 원본 폴더와 검사 클래스를 자동 발견합니다. 규칙이 없는 데이터는 복사 전에 중단합니다.
public class JsonAssetPipeline(IEnumerable<JsonAssetRule>? rules = null)
{
    private readonly IReadOnlyList<JsonAssetRule> _rules = (rules ?? JsonAssetRule.Discover(typeof(JsonAssetRule).Assembly)).ToArray();
    private static readonly HashSet<string> _projectFolders = new(StringComparer.OrdinalIgnoreCase) { "Tools", "Docs", "tables" };

    public IReadOnlyList<OutputFile> Collect(string root, string snapshotFolder, ProjectData data)
    {
        root = Path.GetFullPath(root);
        if (_rules.Any(rule => !rule.SourceFolder.Split('/').All(NamePolicy.IsStem)))
            throw new InvalidDataException("JSON 검사 규칙의 원본 폴더는 상대 영문 경로여야 합니다.");
        string[] sources = DiscoverSources(root).Order(StringComparer.Ordinal).ToArray();
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string source in sources)
        {
            string relative = Path.GetRelativePath(root, source).Replace('\\', '/');
            JsonAssetRule[] matches = _rules.Where(rule => relative.StartsWith(rule.SourceFolder + "/", StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException(matches.Length == 0
                    ? $"JSON 검사 규칙이 없습니다: {relative}. 이 구조를 검사하는 JsonAssetRule을 준비해 주세요. 배포는 중단했습니다."
                    : $"JSON 검사 규칙이 중복 연결됐습니다: {relative}");
            string output = OutputCatalog.JsonAssetPath(relative);
            if (!OutputCatalog.IsJsonPath(output) || !expected.Add(output))
                throw new InvalidDataException($"JSON 배포 경로가 잘못됐거나 중복됩니다: {relative}");
        }
        var files = new List<OutputFile>();
        foreach (JsonAssetRule rule in _rules)
            files.AddRange(rule.Collect(root, Path.Combine(snapshotFolder, rule.SourceFolder), data));
        if (!expected.SetEquals(files.Select(file => file.RelativePath)) || files.Count != expected.Count)
            throw new InvalidDataException("발견한 JSON과 검사 결과의 배포 목록이 다릅니다. 원본 변경 또는 검사 규칙 범위를 확인해 주세요.");
        OutputCatalog.ValidateJsonNames(files);
        return files;
    }

    private static IEnumerable<string> DiscoverSources(string root)
    {
        foreach (string source in Walk(root)) yield return source;
        IEnumerable<string> Walk(string folder)
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(folder))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                string name = Path.GetFileName(path);
                if (name.StartsWith('.') || (folder == root && _projectFolders.Contains(name))
                    || relative.Equals("stats/item", StringComparison.OrdinalIgnoreCase)) continue;
                FileAttributes attributes = File.GetAttributes(path);
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new InvalidDataException($"JSON 탐색 경로에 링크를 사용할 수 없습니다: {relative}");
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    foreach (string file in Walk(path)) yield return file;
                }
                else if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)) yield return path;
            }
        }
    }
}
