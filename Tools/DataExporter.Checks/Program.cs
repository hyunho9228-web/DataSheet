using DataSheet.Export;

using var workspace = new TemporaryWorkspace();
int checks = 0;
void Assert(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
void Fails(Action action, string message)
{
    bool failed = false;
    try { action(); } catch (Exception) { failed = true; }
    Assert(failed, message);
}
if (!args.Contains("--deployment-only"))
{
ProjectData data = ProjectData.Load(Directory.GetCurrentDirectory());
Assert(data.Tables.Count == 13, "기존 13개 탭을 읽어야 합니다.");
foreach (TypedTable table in data.Tables)
{
    string path = Path.Combine(workspace.PathName, table.Schema.Name + TableSerializers.For(table.Schema.Format).Extension);
    TableSerializers.For(table.Schema.Format).Write(table, path); TableSerializers.Verify(table, path);
    Assert(!table.Schema.Columns.Any(column => column.Name is "Name" or "Note"), "제외 컬럼 누출");
}
TypedTable units = data.Tables.Single(table => table.Schema.Name == "UnitStat");
var duplicate = units with { Rows = units.Rows.Append(units.Rows[0]).ToArray() };
Assert(new DataValidator().Validate(data.Tables.Select(table => table == units ? duplicate : table).ToArray()).Any(issue => issue.Code == "DUPLICATE_ID"), "중복 ID 검사 누락");
TypedRow badRow = units.Rows[0] with { Cells = units.Rows[0].Cells.ToDictionary(pair => pair.Key, pair => pair.Key == "Dodge" ? pair.Value with { Value = 10001 } : pair.Value) };
Assert(new DataValidator().Validate(data.Tables.Select(table => table == units ? units with { Rows = new[] { badRow }.Concat(units.Rows.Skip(1)).ToArray() } : table).ToArray()).Any(issue => issue.Column == "Dodge"), "확률 범위 검사 누락");
string classes = workspace.Folder("Classes"); new CodeGenerator().Generate(data, classes);
new GeneratedCodeCompiler().Compile(classes, workspace.Folder("Compile"));
string invalidClasses = workspace.Folder("InvalidClasses");
File.WriteAllText(Path.Combine(invalidClasses, "Invalid.cs"), "public sealed class Invalid { public MissingType Value { get; } }");
Fails(() => new GeneratedCodeCompiler().Compile(invalidClasses, workspace.Folder("InvalidCompile")), "내장 C# 검사기의 잘못된 타입 감지 실패");
Assert(Directory.GetFiles(classes, "*.cs").Length == 17, "생성 클래스 수 오류");
Assert(!File.ReadAllText(Path.Combine(classes, "Player.cs")).Contains("public int HP"), "상속 스탯 중복 선언");
string fixture = workspace.Folder("Fixture"); Directory.CreateDirectory(Path.Combine(fixture, "tables"));
WorkbookFixture.Write(Path.Combine(fixture, "tables", "NewWorkbook.xlsx"), ["NewAlpha", "NewBeta"]);
ProjectData added = ProjectData.Load(fixture);
Assert(added.Tables.Select(table => table.Schema.Name).SequenceEqual(["NewAlpha", "NewBeta"]), "신규 파일·탭 자동 대응 실패");
string newClasses = workspace.Folder("NewClasses"); new CodeGenerator().Generate(added, newClasses);
new GeneratedCodeCompiler().Compile(newClasses, workspace.Folder("NewCompile"));
await AutoLoadingChecks.RunAsync(Assert, workspace);
}
string client = workspace.Folder("Client"); Directory.CreateDirectory(Path.Combine(client, "Assets"));
Directory.CreateDirectory(Path.Combine(client, "ProjectSettings")); File.WriteAllText(Path.Combine(client, "ProjectSettings", "ProjectVersion.txt"), "test");
string source = Path.Combine(workspace.PathName, "NewAlpha.csv"); File.WriteAllText(source, "ID\n1\n");
var output = new OutputFile("Assets/Data/Csv/NewAlpha.csv", source);
var deployment = new DeploymentService(); deployment.Deploy([output], client, workspace);
string target = Path.Combine(client, output.RelativePath); Assert(File.ReadAllText(target) == File.ReadAllText(source), "정상 배포 실패");
File.WriteAllText(target + ".meta", "preserve");
deployment.Deploy([], client, workspace);
Assert(!File.Exists(target) && File.ReadAllText(target + ".meta") == "preserve", "이전 생성 파일 정리·meta 보존 오류");
File.WriteAllText(target, "manual"); Fails(() => deployment.Deploy([output], client, workspace), "수동 파일 덮어쓰기 방지 실패");
File.Delete(target); deployment.Deploy([output], client, workspace); File.WriteAllText(source, "changed");
string blocked = Path.Combine(client, "Assets", "Data", "Csv", "Blocked.csv"); Directory.CreateDirectory(blocked);
Fails(() => deployment.Deploy([output, new OutputFile("Assets/Data/Csv/Blocked.csv", source)], client, workspace), "배포 오류 감지 실패");
Assert(File.ReadAllText(target) == "ID\n1\n", "배포 실패 원복 실패");
Fails(() => deployment.Deploy([new OutputFile("../escape.csv", source)], client, workspace), "상대 경로 탈출 방지 실패");
string codeSource = Path.Combine(workspace.PathName, "NewAlpha.cs"); File.WriteAllText(codeSource, "original class");
var code = new OutputFile("Assets/3.Script/Data/NewAlpha.cs", codeSource);
deployment.Deploy([code], client, workspace);
string codeTarget = Path.Combine(client, code.RelativePath);
Assert(File.ReadAllText(codeTarget) == "original class", "신규 C# 배포 실패");
File.WriteAllText(codeTarget, "client modified class");
DateTime codeTime = File.GetLastWriteTimeUtc(codeTarget);
File.WriteAllText(codeTarget + ".meta", "code meta");
File.WriteAllText(codeSource, "new generated class");
deployment.Deploy([code], client, workspace);
Assert(File.ReadAllText(codeTarget) == "client modified class" && File.GetLastWriteTimeUtc(codeTarget) == codeTime, "기존 C# 내용·수정시각 변경");
deployment.Deploy([], client, workspace);
Assert(File.Exists(codeTarget) && File.ReadAllText(codeTarget + ".meta") == "code meta", "시트 삭제 시 C#·meta 삭제");
string manualCode = Path.Combine(client, "Assets", "3.Script", "Data", "Manual.cs"); File.WriteAllText(manualCode, "manual class");
deployment.Deploy([new OutputFile("Assets/3.Script/Data/Manual.cs", codeSource)], client, workspace);
Assert(File.ReadAllText(manualCode) == "manual class", "목록에 없는 기존 C# 변경");
var newCode = new OutputFile("Assets/3.Script/Data/NewBeta.cs", codeSource);
Fails(() => deployment.Deploy([code, newCode, new OutputFile("Assets/Data/Csv/Blocked.csv", source)], client, workspace), "C# 추가 후 배포 오류 감지 실패");
Assert(!File.Exists(Path.Combine(client, newCode.RelativePath)), "실패한 배포의 신규 C# 원복 실패");
Assert(File.ReadAllText(codeTarget) == "client modified class" && File.GetLastWriteTimeUtc(codeTarget) == codeTime, "배포 실패 시 기존 C# 변경");
File.WriteAllText(Path.Combine(client, ".datasheet-generated.json"), "{\"Files\":[\"Assets/Scripts/Data/NewAlpha.cs\"]}");
deployment.Deploy([code], client, workspace);
Assert(File.ReadAllText(codeTarget) == "client modified class" && File.GetLastWriteTimeUtc(codeTarget) == codeTime, "경로 이전 후 기존 클래스 보존 실패");
Assert(!Directory.Exists(Path.Combine(client, "Assets", "Scripts")), "이전 스크립트 폴더 재생성");
var planOutput = new OutputFile(OutputCatalog.LoadPlanPath, codeSource);
deployment.Deploy([code, planOutput], client, workspace);
File.WriteAllText(codeSource, "자동 로딩 계획 갱신 검사");
deployment.Deploy([code, planOutput], client, workspace);
Assert(File.ReadAllText(Path.Combine(client, OutputCatalog.LoadPlanPath)) == "자동 로딩 계획 갱신 검사", "자동 로딩 계획 갱신 실패");
Assert(File.ReadAllText(codeTarget) == "client modified class", "자동 로딩 계획 갱신 중 기존 정의 클래스 변경");
string previousPlan = File.ReadAllText(Path.Combine(client, OutputCatalog.LoadPlanPath));
File.WriteAllText(codeSource, "원복 검사에 사용할 새 계획");
Fails(() => deployment.Deploy([code, planOutput, new OutputFile("Assets/Data/Csv/Blocked.csv", source)], client, workspace), "자동 계획 갱신 후 배포 실패 감지 누락");
Assert(File.ReadAllText(Path.Combine(client, OutputCatalog.LoadPlanPath)) == previousPlan, "배포 실패 시 자동 로딩 계획 원복 실패");
Console.WriteLine($"검사 {checks}개 통과 ({(args.Contains("--deployment-only") ? "배포 정책" : "전체")}). 임시 파일은 정리합니다.");
