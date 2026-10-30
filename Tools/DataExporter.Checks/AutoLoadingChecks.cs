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
        string plan = new LoadPlanGenerator().Generate(data, workspace.Folder("AutoLoadPlan"));
        string text = File.ReadAllText(plan);
        assert(text.Contains("LoadCsvAsync<NewAlpha>") && text.Contains("LoadCsvAsync<NewBeta>"), "신규 시트의 자동 등록 실패");
        assert(text.Contains("reader.Read<NewJson>"), "신규 구조화 데이터의 자동 등록 실패");
        string client = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Human_Ghost-"));
        byte[] compiled = new ClientLoadPlanCompiler().Compile(client, classes, plan);
        foreach (TypedTable table in data.Tables)
        {
            ITableSerializer serializer = TableSerializers.For(table.Schema.Format);
            string folder = Directory.CreateDirectory(Path.Combine(root, table.Schema.Format == OutputFormat.Csv ? "Csv" : "Json")).FullName;
            serializer.Write(table, Path.Combine(folder, table.Schema.Name + serializer.Extension));
        }
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
            object bootstrap = Activator.CreateInstance(bootstrapType, source, new Func<string, string>(path => Path.Combine(root, path)))!;
            Task task = (Task)bootstrapType.GetMethod("InitializeAsync")!.Invoke(bootstrap, [CancellationToken.None])!;
            await task;
            object catalog = task.GetType().GetProperty("Result")!.GetValue(task)!;
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
