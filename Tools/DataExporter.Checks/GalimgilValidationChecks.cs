using DataSheet.Export;
using System.Text;
using System.Text.Json.Nodes;

internal static class GalimgilValidationChecks
{
    public static void Run(ProjectData data, Action<bool, string> check, Action<Action, string> fails, TemporaryWorkspace workspace)
    {
        string root = workspace.Folder("GalimgilSource");
        string folder = Directory.CreateDirectory(Path.Combine(root, "stats", "galimgil")).FullName;
        string source = Path.Combine(folder, "galimgil_reward.json");
        string original = File.ReadAllText("stats/galimgil/galimgil_reward.json");
        File.WriteAllText(source, original, new UTF8Encoding(false));
        var collector = new GalimgilFileCollector();
        IReadOnlyList<OutputFile> Collect() => collector.Collect(root, workspace.Folder("GalimgilSnapshot"), data);
        var files = Collect();
        check(files.Count == 1 && OutputCatalog.IsOwnedPath(files[0].RelativePath), "갈림길 수집·배포 소유 경로");
        string client = workspace.Folder("GalimgilClient");
        Directory.CreateDirectory(Path.Combine(client, "Assets")); Directory.CreateDirectory(Path.Combine(client, "ProjectSettings"));
        File.WriteAllText(Path.Combine(client, "ProjectSettings", "ProjectVersion.txt"), "검사 자료");
        new DeploymentService().Deploy(files, client, workspace);
        string target = Path.Combine(client, OutputCatalog.GalimgilFolder, "galimgil_reward.json");
        check(File.ReadAllText(target) == original, "갈림길 신규 배포 내용 보존");
        void Change(Action<JsonNode> change)
        {
            var node = JsonNode.Parse(original)!; change(node); File.WriteAllText(source, node.ToJsonString());
        }
        foreach (string field in new[] { "baseSoul", "soulPerKill", "soulPerInteraction" })
        { Change(node => node[field] = -1); fails(() => Collect(), "갈림길 음수 수량 허용: " + field); }
        Change(node => node["soulPerKill"] = 1.5); fails(() => Collect(), "갈림길 실수 계수 허용");
        Change(node => node["soulCurrencyId"] = int.MaxValue); fails(() => Collect(), "없는 재화 ID 허용");
        Change(node => node["countStages"] = new JsonArray(8002, 8002)); fails(() => Collect(), "중복 스테이지 허용");
        Change(node => node["countStages"] = new JsonArray(int.MaxValue)); fails(() => Collect(), "없는 스테이지 허용");
        Change(node => node["countStages"] = new JsonArray()); fails(() => Collect(), "빈 집계 스테이지 허용");
        Change(node => node["rounding"] = "round"); fails(() => Collect(), "지원하지 않는 반올림 허용");
        Change(node => node["showGrade"] = 1); fails(() => Collect(), "등급 표시 여부 타입 오류 허용");
        Change(node => node["grades"]![1]!["grade"] = "HIGH"); fails(() => Collect(), "중복 등급 키 허용");
        Change(node => node["grades"]![2]!["minKillCount"] = 1); fails(() => Collect(), "기본 등급 기준0 누락 허용");
        check(File.ReadAllText(target) == original, "검사 실패가 기존 배포 파일을 변경함");
        File.WriteAllText(source, original, new UnicodeEncoding(false, true)); fails(() => Collect(), "UTF-16 원본 허용");
        File.WriteAllText(source, original, new UTF8Encoding(true)); check(Collect().Count == 1, "UTF-8 BOM 원본 거부");
        Change(node => node["soulPerKill"] = 4);
        new DeploymentService().Deploy(Collect(), client, workspace);
        check(JsonNode.Parse(File.ReadAllText(target))!["soulPerKill"]!.GetValue<int>() == 4, "갈림길 수정 배포 누락");
    }
}
