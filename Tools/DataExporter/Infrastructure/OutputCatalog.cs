namespace DataSheet.Export;

public sealed record OutputFile(string RelativePath, string SourcePath);

public static class OutputCatalog
{
    public static string DataFolder(OutputFormat format) => format == OutputFormat.Csv ? "Assets/Data/Csv" : "Assets/Data/Json";
    public const string ClassesFolder = "Assets/Scripts/Data";
    public static bool IsOwnedPath(string path)
    {
        string directory = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
        string extension = Path.GetExtension(path);
        return NamePolicy.IsStem(Path.GetFileNameWithoutExtension(path)) && (directory, extension) is
            ("Assets/Data/Csv", ".csv") or ("Assets/Data/Json", ".json") or (ClassesFolder, ".cs");
    }
}
