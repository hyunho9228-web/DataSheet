namespace DataSheet.Export;

public sealed record OutputFile(string RelativePath, string SourcePath);

public static class OutputCatalog
{
    public static string DataFolder(OutputFormat format) => format == OutputFormat.Csv ? "Assets/Data/Csv" : "Assets/Data/Json";
    public const string SpawnFolder = "Assets/Data/Json/spawn";
    public const string ShrineFolder = "Assets/Data/Json/shrine";
    public const string GalimgilFolder = "Assets/Data/Json/galimgil";
    public static bool IsGalimgilPath(string path) => path == GalimgilFolder + "/galimgil_reward.json";
    public static bool IsShrinePath(string path)
    {
        if (path == ShrineFolder + "/shrine_level.json") return true;
        const string prefix = ShrineFolder + "/stat/";
        return path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.Ordinal)
            && NamePolicy.IsStem(path[prefix.Length..^5]);
    }
    public static bool IsSpawnPath(string path) => path.StartsWith(SpawnFolder + "/", StringComparison.Ordinal)
        && path.EndsWith(".json", StringComparison.Ordinal)
        && path[(SpawnFolder.Length + 1)..^5] is { Length: > 0 } name
        && name.All(character => character is >= '0' and <= '9');
    public const string ClassesFolder = "Assets/3.Script/Data";
    public const string LoadPlanPath = ClassesFolder + "/Generated/GeneratedDataLoadPlan.cs";
    // 클라이언트의 기존 텍스트 입력은 파일 이름을 키로 쓰므로 하위 폴더가 달라도 이름 충돌을 허용하지 않습니다.
    public static void ValidateJsonNames(IEnumerable<OutputFile> files)
    {
        var duplicate = files.Where(file => file.RelativePath.StartsWith("Assets/Data/Json/", StringComparison.Ordinal)
                && file.RelativePath.EndsWith(".json", StringComparison.Ordinal))
            .GroupBy(file => Path.GetFileNameWithoutExtension(file.RelativePath), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"JSON 파일 이름이 중복되어 클라이언트 로딩 키가 충돌합니다: {string.Join(", ", duplicate.Select(file => file.RelativePath))}");
    }
    public static bool IsLoadPlan(string path) => string.Equals(path, LoadPlanPath, StringComparison.OrdinalIgnoreCase);
    private const string _legacyClassesFolder = "Assets/Scripts/Data";
    public static string NormalizePath(string path) => path.StartsWith(_legacyClassesFolder + "/", StringComparison.OrdinalIgnoreCase)
        ? ClassesFolder + path[_legacyClassesFolder.Length..] : path;
    public static bool IsCode(string path) => path.StartsWith(ClassesFolder + "/", StringComparison.OrdinalIgnoreCase)
        && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    public static bool IsOwnedPath(string path)
    {
        if (IsLoadPlan(path) || IsSpawnPath(path) || IsShrinePath(path) || IsGalimgilPath(path)) return true;
        string directory = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
        string extension = Path.GetExtension(path);
        return NamePolicy.IsStem(Path.GetFileNameWithoutExtension(path)) && (directory, extension) is
            ("Assets/Data/Csv", ".csv") or ("Assets/Data/Json", ".json") or (ClassesFolder, ".cs");
    }
}
