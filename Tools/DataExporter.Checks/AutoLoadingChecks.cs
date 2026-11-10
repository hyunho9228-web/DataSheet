using System.Reflection;
using System.Runtime.Loader;
using DataSheet.Export;

internal static class AutoLoadingChecks
{
    public static async Task RunAsync(Action<bool, string> assert, TemporaryWorkspace workspace)
    {
        string root = workspace.Folder("AutoLoadFixture");
        string tables = Directory.CreateDirectory(Path.Combine(root, "tables")).FullName;
        foreach (string source in Directory.EnumerateFiles("tables", "*.xlsx")) File.Copy(source, Path.Combine(tables, Path.GetFileName(source)));
        WorkbookFixture.Write(Path.Combine(tables, "AddedWorkbook.xlsx"), ["NewAlpha", "NewBeta"]);
        WorkbookFixture.Write(Path.Combine(tables, "AddedJsonWorkbook.xlsx"), ["NewJson"], true);
        ProjectData data = ProjectData.Load(root);
        string classes = workspace.Folder("AutoLoadClasses");
        new CodeGenerator().Generate(data, classes);
        var shrineFiles = new ShrineFileCollector().Collect(Directory.GetCurrentDirectory(), workspace.Folder("AutoShrine"), data);
        string plan = new LoadPlanGenerator().Generate(data, workspace.Folder("AutoLoadPlan"), shrineFiles);
        string text = File.ReadAllText(plan);
        assert(text.Contains("LoadCsvAsync<NewAlpha>") && text.Contains("LoadCsvAsync<NewBeta>"), "신규 시트의 자동 등록 실패");
        assert(text.Contains("reader.Read<NewJson>"), "신규 구조화 데이터의 자동 등록 실패");
        assert(text.Contains("RegisterDefinition(new ShrineDefinitions"), "서낭당 정의 자동 등록 누락");
        foreach (OutputFile file in shrineFiles.Where(file => file.RelativePath.Contains("/stat/")))
            assert(text.Contains($"\"{Path.GetFileNameWithoutExtension(file.RelativePath)}\""), "능력치 JSON의 로딩 계획 반영 누락");
        var generatedNames = shrineFiles.Select(file => Path.GetFileNameWithoutExtension(file.RelativePath)).ToArray();
        string expandedPlan = new LoadPlanGenerator().Generate(data, workspace.Folder("ExtraShrinePlan"), shrineFiles.Append(
            new OutputFile(OutputCatalog.ShrineFolder + "/stat/NewStat.json", "검사 자료")).ToArray());
        assert(File.ReadAllText(expandedPlan).Contains("\"NewStat\""), "새 능력치 파일의 자동 로딩 대응 누락");
        OutputCatalog.ValidateJsonNames(shrineFiles);
        bool duplicateFailed = false;
        try { OutputCatalog.ValidateJsonNames(shrineFiles.Append(new OutputFile("Assets/Data/Json/AttackSpeed.json", "검사 자료"))); }
        catch (InvalidDataException) { duplicateFailed = true; }
        assert(duplicateFailed, "하위 폴더의 JSON 이름 충돌 감지 누락");
        string client = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Human_Ghost-"));
        byte[] compiled = new ClientLoadPlanCompiler().Compile(client, classes, plan);
        foreach (TypedTable table in data.Tables)
        {
            ITableSerializer serializer = TableSerializers.For(table.Schema.Format);
            string folder = Directory.CreateDirectory(Path.Combine(root, table.Schema.Format == OutputFormat.Csv ? "Csv" : "Json")).FullName;
            serializer.Write(table, Path.Combine(folder, table.Schema.Name + serializer.Extension));
        }
        foreach (OutputFile file in shrineFiles)
            File.Copy(file.SourcePath, Path.Combine(root, "Json", Path.GetFileName(file.RelativePath)));
        var context = new AssemblyLoadContext("자동 로딩 통합 검사", true);
        try
        {
            Assembly tool = typeof(GeneratedCodeCompiler).Assembly;
            using Stream reference = tool.GetManifestResourceStream("CompilerReferences.Newtonsoft.Json.dll")!;
            context.LoadFromStream(reference);
            using var image = new MemoryStream(compiled);
            Assembly assembly = context.LoadFromStream(image);
            object source = Activator.CreateInstance(assembly.GetType("HumanGhost.Data.Infrastructure.Utf8CsvFileSource")!)!;
            Type bootstrapType = assembly.GetType("HumanGhost.Data.Infrastructure.DataBootstrapper")!;
            object bootstrap = Activator.CreateInstance(bootstrapType, source, new Func<string, string>(path => Path.Combine(root, path)), null)!;
            Task task = (Task)bootstrapType.GetMethod("InitializeAsync")!.Invoke(bootstrap, [CancellationToken.None])!;
            await task;
            object catalog = task.GetType().GetProperty("Result")!.GetValue(task)!;
            Type shrineType = assembly.GetType("HumanGhost.Data.Definitions.ShrineDefinitions")!;
            object shrine = catalog.GetType().GetMethod("GetDefinition")!.MakeGenericMethod(shrineType).Invoke(catalog, null)!;
            object stats = shrineType.GetProperty("Stats")!.GetValue(shrine)!;
            assert((int)stats.GetType().GetProperty("Count")!.GetValue(stats)! == generatedNames.Length - 1,
                "서낭당 능력치 정의 조회 수가 원본 파일 수와 다름");
            foreach (string name in new[] { "NewAlpha", "NewBeta", "NewJson" })
            {
                Type model = assembly.GetType("HumanGhost.Data.Definitions." + name)!;
                object table = catalog.GetType().GetMethod("GetTable")!.MakeGenericMethod(model).Invoke(catalog, null)!;
                object row = table.GetType().GetMethod("Get")!.Invoke(table, [1])!;
                assert((int)model.GetProperty("Value")!.GetValue(row)! == 42, $"{name}: 자동 등록 후 식별자 조회 실패");
            }
        }
        finally { context.Unload(); }

        ProjectData reduced = data with { Tables = data.Tables.Where(table => table.Schema.Name != "NewBeta").ToArray() };
        string updated = new LoadPlanGenerator().Generate(reduced, workspace.Folder("ReducedPlan"));
        assert(!File.ReadAllText(updated).Contains("LoadCsvAsync<NewBeta>"), "삭제한 탭이 자동 로딩 계획에 남았습니다.");
    }
}
