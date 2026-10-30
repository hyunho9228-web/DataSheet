namespace DataSheet.Export;

public sealed record OutputFile(string RelativePath, string SourcePath);

public static class OutputCatalog
{
    public static string DataFolder(OutputFormat format) => format == OutputFormat.Csv ? "Assets/Data/Csv" : "Assets/Data/Json";
    public const string ClassesFolder = "Assets/3.Script/Data";
    public const string LoadPlanPath = ClassesFolder + "/Generated/GeneratedDataLoadPlan.cs";
    public static bool IsLoadPlan(string path) => string.Equals(path, LoadPlanPath, StringComparison.OrdinalIgnoreCase);
    private const string _legacyClassesFolder = "Assets/Scripts/Data";
    public static string NormalizePath(string path) => path.StartsWith(_legacyClassesFolder + "/", StringComparison.OrdinalIgnoreCase)
        ? ClassesFolder + path[_legacyClassesFolder.Length..] : path;
    public static bool IsCode(string path) => path.StartsWith(ClassesFolder + "/", StringComparison.OrdinalIgnoreCase)
        && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    public static bool IsOwnedPath(string path)
    {
        if (IsLoadPlan(path)) return true;
        string directory = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
        string extension = Path.GetExtension(path);
        return NamePolicy.IsStem(Path.GetFileNameWithoutExtension(path)) && (directory, extension) is
            ("Assets/Data/Csv", ".csv") or ("Assets/Data/Json", ".json") or (ClassesFolder, ".cs");
    }
}
