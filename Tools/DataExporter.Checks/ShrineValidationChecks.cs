using DataSheet.Export;
using System.Text.Json.Nodes;

internal static class ShrineValidationChecks
{
    public static void Run(ProjectData data, Action<bool, string> check, Action<Action, string> fails, TemporaryWorkspace workspace)
    {
        string root = workspace.Folder("ShrineSource");
        string folder = Directory.CreateDirectory(Path.Combine(root, "stats", "shrine")).FullName;
        Directory.CreateDirectory(Path.Combine(folder, "stat"));
        foreach (string file in Directory.EnumerateFiles("stats/shrine", "*.json", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(folder, Path.GetRelativePath("stats/shrine", file)));
        var collector = new ShrineFileCollector();
        IReadOnlyList<OutputFile> Collect() => collector.Collect(root, workspace.Folder("ShrineSnapshot"), data);
        var files = Collect();
        check(files.Count == Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories).Count(), "서낭당 전체 파일 수집 누락");
        check(files.All(file => OutputCatalog.IsOwnedPath(file.RelativePath)), "서낭당 배포 소유 경로 누락");
        check(!OutputCatalog.IsOwnedPath(OutputCatalog.ShrineFolder + "/stat/../outside.json"), "서낭당 경로 이탈 허용");
        string client = workspace.Folder("ShrineClient");
        Directory.CreateDirectory(Path.Combine(client, "Assets"));
        Directory.CreateDirectory(Path.Combine(client, "ProjectSettings"));
        File.WriteAllText(Path.Combine(client, "ProjectSettings", "ProjectVersion.txt"), "검사 자료");
        var deployment = new DeploymentService();
        deployment.Deploy(files, client, workspace);
        check(files.All(file => File.ReadAllText(file.SourcePath) == File.ReadAllText(Path.Combine(client, file.RelativePath))), "서낭당 신규 배포 내용 불일치");
        string levelFile = Path.Combine(folder, "shrine_level.json");
        string original = File.ReadAllText(levelFile);
        void ChangeLevel(Action<JsonNode> change)
        {
            JsonNode value = JsonNode.Parse(original)!;
            change(value);
            File.WriteAllText(levelFile, value.ToJsonString());
        }
        ChangeLevel(value => value["levels"]![0]!["soulCostToNext"] = 65);
        deployment.Deploy(Collect(), client, workspace);
        string target = Path.Combine(client, OutputCatalog.ShrineFolder, "shrine_level.json");
        check(JsonNode.Parse(File.ReadAllText(target))!["levels"]![0]!["soulCostToNext"]!.GetValue<int>() == 65, "서낭당 기존 배포 갱신 실패");
        string previous = File.ReadAllText(target);
        ChangeLevel(value => value["soulCurrencyId"] = int.MaxValue);
        fails(() => Collect(), "서낭당 없는 재화 참조 허용");
        check(File.ReadAllText(target) == previous, "검사 실패 시 기존 배포값 변경");
        ChangeLevel(value => value["deathSoulLossRate"] = 10001);
        fails(() => Collect(), "서낭당 확률 상한 초과 허용");
        ChangeLevel(value => value["levels"]![0]!["level"] = 2);
        fails(() => Collect(), "서낭당 중복·누락 단계 허용");
        ChangeLevel(value => value["levels"]![0]!["soulCostToNext"] = 1.5);
        fails(() => Collect(), "서낭당 실수 비용 허용");
        File.WriteAllText(levelFile, original);
        string statFile = Path.Combine(folder, "stat", "ParryStamina.json");
        string stat = File.ReadAllText(statFile);
        check(JsonNode.Parse(stat)!["steps"]![0]!["value"]!.GetValue<int>() < 0 && Collect().Count == files.Count, "패링 소모 감소의 음수 정수 거부");
        JsonNode invalid = JsonNode.Parse(stat)!;
        invalid["textId"] = int.MaxValue;
        File.WriteAllText(statFile, invalid.ToJsonString());
        fails(() => Collect(), "서낭당 없는 문구 참조 허용");
        File.WriteAllText(statFile, "{");
        fails(() => Collect(), "잘못된 서낭당 JSON 허용");
        File.WriteAllBytes(statFile, [0xff]);
        fails(() => Collect(), "잘못된 서낭당 UTF-8 허용");
        File.WriteAllText(statFile, stat, System.Text.Encoding.Unicode);
        fails(() => Collect(), "서낭당 UTF-16 원본 허용");
        File.WriteAllText(statFile, stat, new System.Text.UTF8Encoding(true));
        check(Collect().Count == files.Count, "서낭당 UTF-8 BOM 거부");
        File.WriteAllText(statFile, stat);
        JsonNode added = JsonNode.Parse(stat)!;
        added["statType"] = "NewStat";
        string addedFile = Path.Combine(folder, "stat", "NewStat.json");
        File.WriteAllText(addedFile, added.ToJsonString());
        var expanded = Collect();
        check(expanded.Count == files.Count + 1, "서낭당 신규 능력치 파일 자동 수집 실패");
        deployment.Deploy(expanded, client, workspace);
        string addedTarget = Path.Combine(client, OutputCatalog.ShrineFolder, "stat", "NewStat.json");
        File.WriteAllText(addedTarget + ".meta", "기존 GUID 보존");
        File.Delete(addedFile);
        deployment.Deploy(Collect(), client, workspace);
        check(!File.Exists(addedTarget) && File.Exists(addedTarget + ".meta"), "서낭당 삭제 반영·meta 보존 실패");
    }
}
