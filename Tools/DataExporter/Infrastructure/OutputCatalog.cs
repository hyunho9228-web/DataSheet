namespace DataSheet.Export;

public sealed record OutputFile(string RelativePath, string SourcePath);

public static class OutputCatalog
{
    public static string DataFolder(OutputFormat format) => format == OutputFormat.Csv ? "Assets/Data/Csv" : "Assets/Data/Json";
    public const string SpawnFolder = "Assets/Data/Json/spawn";
    public const string ShrineFolder = "Assets/Data/Json/shrine";
    public const string GalimgilFolder = "Assets/Data/Json/galimgil";
    public const string DialogueFolder = "Assets/Data/Json/dialogue";
    public static string JsonAssetPath(string sourceRelative) => "Assets/Data/Json/"
        + (sourceRelative.StartsWith("stats/", StringComparison.Ordinal) ? sourceRelative[6..] : sourceRelative);
    public static bool IsJsonPath(string path) => path.StartsWith("Assets/Data/Json/", StringComparison.Ordinal)
        && path.EndsWith(".json", StringComparison.Ordinal) && path["Assets/Data/Json/".Length..^5].Split('/').All(segment =>
            segment.Length > 0 && segment.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'));
    public const string ClassesFolder = "Assets/3.Script/Data";
    public const string EnumsFolder = ClassesFolder + "/Generated/Enums";
    public static bool IsGeneratedCode(string path) => IsLoadPlan(path) || IsGeneratedEnum(path);
    public static bool IsGeneratedEnum(string path) => string.Equals(path, ClassesFolder + "/StateKey.cs", StringComparison.OrdinalIgnoreCase)
        || (string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), EnumsFolder, StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && NamePolicy.IsStem(Path.GetFileNameWithoutExtension(path)));
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
        if (IsGeneratedCode(path) || IsJsonPath(path)) return true;
        string directory = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
        string extension = Path.GetExtension(path);
        return NamePolicy.IsStem(Path.GetFileNameWithoutExtension(path)) && (directory, extension) is
            ("Assets/Data/Csv", ".csv") or ("Assets/Data/Json", ".json") or (ClassesFolder, ".cs");
    }
}
