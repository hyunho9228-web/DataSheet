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
ShrineValidationChecks.Run(data, Assert, Fails, workspace);
GalimgilValidationChecks.Run(data, Assert, Fails, workspace);
CutsceneValidationChecks.Run(data, Assert, Fails);
NpcDeploymentChecks.Run(data, Assert, Fails, workspace);
JsonPipelineChecks.Run(data, Assert, Fails, workspace);
EnumDeploymentChecks.Run(data, Assert, Fails, workspace);
int expectedTables = Directory.EnumerateFiles("tables", "*.xlsx", SearchOption.AllDirectories)
    .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
    .Sum(path => new SheetRuleReader().Read(new ExcelTableReader().Read(path)).Count);
Assert(data.Tables.Count == expectedTables, "전체 파일의 추출 대상 탭을 읽어야 합니다.");
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
// 도구 내부에서는 아래 최신 문법을 사용할 수 있어도 Unity 배포 결과에는 허용하지 않습니다.
File.WriteAllText(Path.Combine(invalidClasses, "Invalid.cs"), "namespace ToolOnly; public class Modern { public int[] Values => [1, 2]; }");
Fails(() => new GeneratedCodeCompiler().Compile(invalidClasses, workspace.Folder("ModernCompile")), "최신 전용 문법이 Unity 생성 코드 검사에 통과했습니다.");
Assert(data.Tables.All(table => File.Exists(Path.Combine(classes, table.Schema.Name + ".cs"))), "테이블 정의 클래스 생성 누락");
var prefabValidator = new HealthDisplayPrefabValidator();
prefabValidator.Validate(data, Path.GetFullPath("../Human_Ghost-"));
TypedTable healthTable = data.Tables.Single(table => table.Schema.Name == "HealthDisplayConfig");
var prefabCell = new TypedCell(healthTable.Schema.Columns.Single(column => column.Name == "PrefabKey"),
    "PlayerHealthBar", new SourceLocation("검사 자료", "HealthDisplayConfig", "B7"));
ProjectData prefabData = data with { Tables = [healthTable with
    { Rows = [new TypedRow(7, new Dictionary<string, TypedCell> { ["PrefabKey"] = prefabCell })] }] };
string prefabClient = workspace.Folder("PrefabClient");
string prefabFolder = Directory.CreateDirectory(Path.Combine(prefabClient, "Assets", "2.Model", "Prefabs", "UI")).FullName;
Fails(() => prefabValidator.Validate(prefabData, prefabClient), "프리팹 누락 검사 실패");
File.WriteAllText(Path.Combine(prefabFolder, "PlayerHealthBar.prefab"), "프리팹 존재 검사 자료");
prefabValidator.Validate(prefabData, prefabClient);
Directory.CreateDirectory(Path.Combine(prefabFolder, "Other"));
File.WriteAllText(Path.Combine(prefabFolder, "Other", "PlayerHealthBar.prefab"), "중복 검사 자료");
Fails(() => prefabValidator.Validate(prefabData, prefabClient), "프리팹 이름 중복 검사 실패");
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
string spawnRoot = workspace.Folder("SpawnSource");
string spawnFolder = Directory.CreateDirectory(Path.Combine(spawnRoot, "spawn")).FullName;
string spawnSource = Path.Combine(spawnFolder, "8002.json");
File.WriteAllText(spawnSource, "{\"FormatVersion\":2,\"MaxMonsterCount\":30}");
var collector = new SpawnJsonRule();
var spawnFiles = collector.Collect(spawnRoot, workspace.Folder("SpawnSnapshot"));
deployment.Deploy(spawnFiles, client, workspace);
string spawnTarget = Path.Combine(client, OutputCatalog.SpawnFolder, "8002.json");
Assert(File.ReadAllText(spawnTarget) == File.ReadAllText(spawnSource), "신규 스폰 JSON 배포 실패");
File.WriteAllText(spawnSource, "{\"FormatVersion\":3,\"MaxMonsterCount\":30}");
spawnFiles = collector.Collect(spawnRoot, workspace.Folder("SpawnUpdate"));
deployment.Deploy(spawnFiles, client, workspace);
Assert(File.ReadAllText(spawnTarget).Contains(":3"), "수정 스폰 JSON 덮어쓰기 실패");
Fails(() => deployment.Deploy([new OutputFile("Assets/Data/Json/spawn/../8002.json", spawnSource)], client, workspace), "스폰 경로 이탈 허용");
File.WriteAllText(spawnSource, "{");
Fails(() => collector.Collect(spawnRoot, workspace.Folder("InvalidSpawn")), "잘못된 스폰 JSON 허용");
Assert(File.ReadAllText(spawnTarget).Contains(":3"), "원본 오류로 기존 스폰 배포값 변경");
// 상한은 도구의 고정 숫자가 아니라 각 JSON의 값입니다.
string Layout(int maximum, params int[] counts) => System.Text.Json.JsonSerializer.Serialize(new
{
    FormatVersion = 2, StageID = 8002, MaxMonsterCount = maximum,
    Placements = counts.Select((count, index) => new { PlacementID = index + 1,
        Monsters = new[] { new { MonsterCode = 3001, MonsterCount = count,
            Points = Enumerable.Range(0, count).Select(i => new[] { i * 100, 0 }).ToArray() } } }).ToArray()
});
void CheckLayout(string json) => File.WriteAllText(spawnSource, json);
CheckLayout(Layout(30, 14, 16));
Assert(collector.Collect(spawnRoot, workspace.Folder("SpawnLimit30")).Count == 1, "최대 30마리 배포 허용");
CheckLayout(Layout(30, 14, 17));
Fails(() => collector.Collect(spawnRoot, workspace.Folder("SpawnOverLimit")), "여러 배치 합계 상한 초과 허용");
CheckLayout(Layout(12, 6, 6));
Assert(collector.Collect(spawnRoot, workspace.Folder("SpawnLimit12")).Count == 1, "JSON별 상한 변경 미반영");
CheckLayout(Layout(31, 31));
Assert(collector.Collect(spawnRoot, workspace.Folder("SpawnLimit31")).Count == 1, "도구에 30마리 상한 하드코딩");
CheckLayout(Layout(0));
Assert(collector.Collect(spawnRoot, workspace.Folder("SpawnEmpty")).Count == 1, "몬스터 없는 맵의 최대 0 허용");
CheckLayout("{\"FormatVersion\":2}");
Fails(() => collector.Collect(spawnRoot, workspace.Folder("SpawnNoLimit")), "최대 수 누락 허용");
CheckLayout(Layout(-1));
Fails(() => collector.Collect(spawnRoot, workspace.Folder("SpawnNegativeLimit")), "음수 최대 수 허용");
CheckLayout(Layout(20, 30).Replace("\"MonsterCount\":30", "\"MonsterCount\":1"));
Fails(() => collector.Collect(spawnRoot, workspace.Folder("SpawnPositionLimit")), "위치 수에 의한 상한 초과 허용");
Console.WriteLine($"검사 {checks}개 통과 ({(args.Contains("--deployment-only") ? "배포 정책" : "전체")}). 임시 파일은 정리합니다.");
