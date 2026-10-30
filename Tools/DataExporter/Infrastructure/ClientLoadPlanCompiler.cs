namespace DataSheet.Export;

public sealed class ClientLoadPlanCompiler
{
    public byte[] Compile(string clientRoot, string definitions, string plan)
    {
        string scripts = Path.Combine(clientRoot, "Assets", "3.Script");
        if (!Directory.Exists(scripts)) throw new DirectoryNotFoundException("자동 로딩 계획 검사에 필요한 형제 클라이언트의 스크립트 폴더가 없습니다.");
        var sources = new List<string> { plan };
        foreach (string folder in new[] { "Common/Csv", "Common/Text", "Common/Json", "Data/Mapping", "Data/Storage" })
            sources.AddRange(Directory.EnumerateFiles(Path.Combine(scripts, folder.Replace('/', Path.DirectorySeparatorChar)), "*.cs"));
        foreach (string name in new[] { "DataBootstrapper", "BootstrapTableLoader", "DataLoadSequence", "Utf8CsvFileSource" })
            sources.Add(Path.Combine(scripts, "Data", "Infrastructure", name + ".cs"));
        // 실제로 보존할 클라이언트 정의를 우선하여 생성 계획과의 호환성도 검사합니다.
        var models = Directory.EnumerateFiles(Path.Combine(scripts, "Data"), "*.cs").ToDictionary(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(definitions, "*.cs")) models.TryAdd(Path.GetFileName(path), path);
        sources.AddRange(models.Values);
        return new GeneratedCodeCompiler().CompileSources(sources);
    }
}
