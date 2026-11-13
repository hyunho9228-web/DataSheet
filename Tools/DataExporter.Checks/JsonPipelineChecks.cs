using DataSheet.Export;

internal static class JsonPipelineChecks
{
    public static void Run(ProjectData data, Action<bool, string> check, Action<Action, string> fails, TemporaryWorkspace workspace)
    {
        var pipeline = new JsonAssetPipeline();
        var files = pipeline.Collect(Directory.GetCurrentDirectory(), workspace.Folder("AllJson"), data);
        check(files.Count > 0 && files.Select(file => file.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() == files.Count,
            "JSON 자동 수집이 비었거나 배포 경로가 중복됩니다.");
        check(files.All(file => !file.RelativePath.Contains("/item/")), "예시 아이템 JSON이 배포 목록에 들어갔습니다.");
        var rules = JsonAssetRule.Discover(typeof(JsonAssetRule).Assembly);
        check(rules.Any(rule => rule is SpawnJsonRule) && rules.Select(rule => rule.SourceFolder).Distinct().Count() == rules.Count,
            "기본 JSON 검사 규칙 자동 발견에 실패했습니다.");
        string root = workspace.Folder("NewJsonSource");
        string folder = Directory.CreateDirectory(Path.Combine(root, "new_content")).FullName;
        string source = Path.Combine(folder, "First.json");
        File.WriteAllText(source, "{\"ID\":1}");
        fails(() => pipeline.Collect(root, workspace.Folder("UnknownJson"), data), "검사 규칙 없는 새 폴더가 배포됐습니다.");
        // 검사 규칙 추가만으로 새 폴더를 처리합니다. Program·배포 경로 목록은 바꾸지 않습니다.
        var added = new JsonAssetPipeline(JsonAssetRule.Discover(typeof(NewContentJsonRule).Assembly));
        IReadOnlyList<OutputFile> Collect() => added.Collect(root, workspace.Folder("NewJsonSnapshot"), data);
        var first = Collect();
        check(first.Single().RelativePath == "Assets/Data/Json/new_content/First.json", "새 폴더 자동 경로 결정에 실패했습니다.");
        string client = workspace.Folder("GenericJsonClient");
        Directory.CreateDirectory(Path.Combine(client, "Assets")); Directory.CreateDirectory(Path.Combine(client, "ProjectSettings"));
        File.WriteAllText(Path.Combine(client, "ProjectSettings", "ProjectVersion.txt"), "검사 자료");
        var deploy = new DeploymentService(); deploy.Deploy(first, client, workspace);
        string target = Path.Combine(client, first[0].RelativePath);
        check(File.ReadAllText(target) == "{\"ID\":1}", "새 종류 JSON 공통 배포에 실패했습니다.");
        File.WriteAllText(source, "{\"ID\":2}");
        string second = Path.Combine(folder, "Second.json"); File.WriteAllText(second, "{\"ID\":3}");
        deploy.Deploy(Collect(), client, workspace);
        check(File.ReadAllText(target) == "{\"ID\":2}" && File.Exists(Path.Combine(client, "Assets/Data/Json/new_content/Second.json")),
            "파일 추가·수정 자동 반영에 실패했습니다.");
        File.WriteAllText(source, "{\"ID\":0}"); fails(() => Collect(), "연결된 검사 규칙이 실행되지 않았습니다.");
        check(File.ReadAllText(target) == "{\"ID\":2}", "검사 실패가 배포 파일을 변경했습니다.");
        File.Delete(source); File.WriteAllText(target + ".meta", "보존");
        deploy.Deploy(Collect(), client, workspace);
        check(!File.Exists(target) && File.Exists(target + ".meta"), "신규 종류 삭제 반영·메타 보존 실패");
        string upper = Path.Combine(root, "stats", "new_content"); Directory.CreateDirectory(upper);
        File.WriteAllText(Path.Combine(upper, "Second.json"), "{}");
        fails(() => Collect(), "규칙 없는 stats 신규 폴더를 허용했습니다.");
        check(!OutputCatalog.IsOwnedPath("Assets/Data/Json/new_content/../Other.json"), "공통 경로의 상위 이동 허용");
        check(!OutputCatalog.IsOwnedPath("Assets/Data/Json/new_content/Other.cs"), "JSON 경로로 도구 코드 배포 허용");
        check(!OutputCatalog.IsOwnedPath("Tools/DataExporter/Program.cs"), "도구 소스 경로를 배포 대상으로 허용했습니다.");
        var code = new UnityCodeOutput().Create(data, Path.GetFullPath("../Human_Ghost-"), workspace, files);
        check(code.All(file => file.SourcePath.StartsWith(workspace.Folder("UnityDefinitions") + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) || file.SourcePath.StartsWith(workspace.Folder("UnityLoadPlan") + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)), "생성 전용 폴더 밖의 소스가 Unity 배포 목록에 들어갔습니다.");
    }
}

// 검사 실행파일에만 있는 새 구조입니다. 실제 원본·실제 배포 도구에는 추가하지 않습니다.
public class NewContentJsonRule : JsonAssetRule
{
    public override string SourceFolder => "new_content";
    protected override JsonAssetValidation Prepare(string sourceFolder, ProjectData data) => new((value, file) =>
        JsonAssetFields.Integer(value, "ID", 1, file));
}
