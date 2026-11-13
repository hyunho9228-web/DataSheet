using DataSheet.Export;
using System.Security.Cryptography;
using System.Text;

int exitCode = Run(args);
if (args.Length == 0 && !Console.IsInputRedirected)
{
    Console.WriteLine("엔터 키를 누르면 종료합니다.");
    Console.ReadLine();
}
return exitCode;

static int Run(string[] args)
{
try
{
    if (args.Length > 1 || args.Any(argument => argument is not ("--check" or "--deploy"))) throw new ArgumentException("사용법: DeployData.exe [--check | --deploy]");
    bool deploy = args.Length == 0 || args.Contains("--deploy");
    string root = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "tables")) ? AppContext.BaseDirectory : Directory.GetCurrentDirectory();
    if (!Directory.Exists(Path.Combine(root, "tables"))) throw new DirectoryNotFoundException("DataSheet 폴더에서 실행해 주세요.");
    string lockName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToUpperInvariant())));
    using var mutex = new Mutex(false, "DataSheetExport_" + lockName);
    bool acquired;
    try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
    if (!acquired) throw new IOException("이미 추출·배포 작업이 실행 중입니다.");
    try
    {
        using var workspace = new TemporaryWorkspace();
        Console.WriteLine("시트의 지정 규칙·값·참조를 검사합니다.");
        ProjectData data = ProjectData.Load(root);
        string client = Path.GetFullPath(Path.Combine(root, "..", "Human_Ghost-"));
        new HealthDisplayPrefabValidator().Validate(data, client);
        string outputs = workspace.Folder("Data");
        var files = new List<OutputFile>();
        foreach (TypedTable table in data.Tables)
        {
            ITableSerializer serializer = TableSerializers.For(table.Schema.Format);
            string path = Path.Combine(outputs, table.Schema.Name + serializer.Extension);
            serializer.Write(table, path); TableSerializers.Verify(table, path);
            files.Add(new(OutputCatalog.DataFolder(table.Schema.Format) + "/" + Path.GetFileName(path), path));
        }
        IReadOnlyList<OutputFile> jsonFiles = new JsonAssetPipeline().Collect(root, workspace.Folder("JsonAssets"), data);
        files.AddRange(jsonFiles);
        OutputCatalog.ValidateJsonNames(files);
        files.AddRange(new UnityCodeOutput().Create(data, client, workspace, jsonFiles));
        if (deploy) new DeploymentService().Deploy(files, client, workspace);
        string jsonSummary = string.Join(", ", jsonFiles.GroupBy(file => Path.GetDirectoryName(file.RelativePath)!.Replace('\\', '/'))
            .Select(group => $"{group.Key}: {group.Count()}개"));
        Console.WriteLine($"{(deploy ? "배포" : "검사")} 완료: {data.Tables.Count}개 탭, JSON 원본 {jsonFiles.Count}개 ({jsonSummary}). Unity 호환 코드 검사를 통과했습니다. 임시 생성 파일을 정리합니다.");
    }
    finally { mutex.ReleaseMutex(); }
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
}
