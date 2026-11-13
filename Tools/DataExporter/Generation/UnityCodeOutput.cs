namespace DataSheet.Export;

// 최신 문법을 쓰는 도구 소스를 배포하지 않습니다. 생성 템플릿의 결과만 검사 후 배포 목록으로 돌려줍니다.
public class UnityCodeOutput
{
    public IReadOnlyList<OutputFile> Create(ProjectData data, string client, TemporaryWorkspace workspace,
        IReadOnlyList<OutputFile> jsonFiles)
    {
        string definitions = workspace.Folder("UnityDefinitions");
        new CodeGenerator().Generate(data, definitions);
        new GeneratedCodeCompiler().Compile(definitions, workspace.Folder("UnityCompile"));
        string plan = new LoadPlanGenerator().Generate(data, workspace.Folder("UnityLoadPlan"), jsonFiles);
        new ClientLoadPlanCompiler().Compile(client, definitions, plan);
        return [.. Directory.EnumerateFiles(definitions, "*.cs").Select(path =>
            new OutputFile(OutputCatalog.ClassesFolder + "/" + Path.GetFileName(path), path)), new(OutputCatalog.LoadPlanPath, plan)];
    }
}
