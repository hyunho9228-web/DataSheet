using System.Reflection;
using System.Text.Json;

namespace DataSheet.Export;

public record JsonAssetValidation(Action<JsonElement, string> Validate,
    Action<IReadOnlyList<OutputFile>>? Complete = null);

// 기능별 코드는 검사만 제공합니다. 탐색·인코딩·사본·배포 경로는 공용으로 결정합니다.
public abstract class JsonAssetRule
{
    public abstract string SourceFolder { get; }
    protected abstract JsonAssetValidation Prepare(string sourceFolder, ProjectData data);

    public IReadOnlyList<OutputFile> Collect(string root, string snapshotFolder, ProjectData? data = null)
    {
        JsonAssetValidation validation = Prepare(Path.Combine(root, SourceFolder),
            data ?? new ProjectData([], new Dictionary<string, SheetInstructions>()));
        var files = new JsonAssetCollector().Collect(Path.Combine(root, SourceFolder),
            OutputCatalog.JsonAssetPath(SourceFolder), snapshotFolder, OutputCatalog.IsJsonPath, validation.Validate);
        validation.Complete?.Invoke(files);
        return files;
    }

    public static IReadOnlyList<JsonAssetRule> Discover(Assembly assembly) => assembly.GetTypes()
        .Where(type => !type.IsAbstract && typeof(JsonAssetRule).IsAssignableFrom(type))
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .Select(type => (JsonAssetRule)Activator.CreateInstance(type)!).ToArray();
}
