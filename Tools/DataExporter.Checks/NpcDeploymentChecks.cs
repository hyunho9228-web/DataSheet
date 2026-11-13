using DataSheet.Export;
using System.Text.Json.Nodes;

internal static class NpcDeploymentChecks
{
    public static void Run(ProjectData data, Action<bool, string> check, Action<Action, string> fails, TemporaryWorkspace workspace)
    {
        // 실제 시트 대신 임시 검사 정의를 사용합니다. 운영 밸런스나 원본 파일은 바꾸지 않습니다.
        const int npcId = 25001, dialogueId = 24001;
        int[] texts = data.Tables.Single(table => table.Schema.Name == "Language").Rows.Take(2)
            .Select(row => (int)row.Cells["ID"].Value!).ToArray();
        TypedTable npc = Table("Npc", ("ID", npcId));
        TypedTable dialogue = Table("Dialogue", ("ID", dialogueId), ("LineFile", "24001.json"), ("TriggerType", "TALK"), ("TriggerTargetID", 0));
        ProjectData With(params TypedTable[] tables) => data with
        { Tables = data.Tables.Where(table => tables.All(added => added.Schema.Name != table.Schema.Name)).Concat(tables).ToArray() };
        ProjectData ready = With(npc, dialogue);
        string root = workspace.Folder("NpcSource"), snapshot = workspace.Folder("NpcSnapshot");
        var speeches = new DialogueJsonRule();
        check(speeches.Collect(root, snapshot, data with { Tables = data.Tables.Where(table => table.Schema.Name != "Dialogue").ToArray() }).Count == 0,
            "대화 시트와 폴더가 없는 준비 상태는 허용해야 합니다.");
        fails(() => speeches.Collect(root, snapshot, ready), "Dialogue 행이 있는데 대사 파일 누락을 허용했습니다.");
        string folder = Directory.CreateDirectory(Path.Combine(root, "dialogue")).FullName;
        string source = Path.Combine(folder, "24001.json");
        string original = new JsonObject { ["DialogueID"] = dialogueId,
            ["Lines"] = new JsonArray(new JsonObject { ["SpeakerTextID"] = texts[0], ["TextID"] = texts[1] }) }.ToJsonString();
        File.WriteAllText(source, original);
        IReadOnlyList<OutputFile> Collect() => speeches.Collect(root, snapshot, ready);
        var files = Collect();
        check(files.Count == 1 && files[0].RelativePath == OutputCatalog.DialogueFolder + "/24001.json", "대사 JSON 수집 경로가 잘못됐습니다.");
        fails(() => speeches.Collect(root, snapshot, data with { Tables = data.Tables.Where(table => table.Schema.Name != "Dialogue").ToArray() }),
            "Dialogue 표 없이 대사 JSON 참조를 허용했습니다.");
        void Change(Action<JsonNode> change)
        { var value = JsonNode.Parse(original)!; change(value); File.WriteAllText(source, value.ToJsonString()); }
        Change(value => value["DialogueID"] = dialogueId + 1); fails(() => Collect(), "없는 대화 ID를 허용했습니다.");
        Change(value => value["Lines"] = new JsonArray()); fails(() => Collect(), "빈 대사 목록을 허용했습니다.");
        Change(value => value["Lines"]![0]!["TextID"] = int.MaxValue); fails(() => Collect(), "없는 Language 대사 ID를 허용했습니다.");
        Change(value => value["Lines"]![0]!["SpeakerTextID"] = 1.5); fails(() => Collect(), "실수 화자 ID를 허용했습니다.");
        File.WriteAllText(source, original);
        File.Move(source, Path.Combine(folder, "024001.json"));
        fails(() => Collect(), "파일 이름과 DialogueID 불일치를 허용했습니다.");
        File.Move(Path.Combine(folder, "024001.json"), source);
        string nested = Directory.CreateDirectory(Path.Combine(folder, "Nested")).FullName;
        File.Move(source, Path.Combine(nested, "24001.json"));
        fails(() => Collect(), "대사 원본의 미지원 중첩 경로를 허용했습니다.");
        File.Move(Path.Combine(nested, "24001.json"), source);
        ProjectData badTrigger = With(npc, Table("Dialogue", ("ID", dialogueId), ("LineFile", "24001.json"), ("TriggerType", "TALK"), ("TriggerTargetID", 1)));
        fails(() => speeches.Collect(root, snapshot, badTrigger), "말 걸기 대화의 잘못된 발생 대상을 허용했습니다.");
        foreach (var trigger in new[] { ("BOSS_STATE_ENTER", "BossState"), ("BOSS_DEFEATED", "UnitStat") })
        {
            int id = (int)data.Tables.Single(table => table.Schema.Name == trigger.Item2).Rows[0].Cells["ID"].Value!;
            ProjectData triggered = With(npc, Table("Dialogue", ("ID", dialogueId), ("LineFile", "24001.json"), ("TriggerType", trigger.Item1), ("TriggerTargetID", id)));
            check(speeches.Collect(root, snapshot, triggered).Count == 1, "유효한 보스 대화 발생 대상을 거부했습니다.");
            triggered = With(npc, Table("Dialogue", ("ID", dialogueId), ("LineFile", "24001.json"), ("TriggerType", trigger.Item1), ("TriggerTargetID", int.MaxValue)));
            fails(() => speeches.Collect(root, snapshot, triggered), "없는 보스 대화 발생 대상을 허용했습니다.");
        }

        string spawnFolder = Directory.CreateDirectory(Path.Combine(root, "spawn")).FullName;
        string spawn = Path.Combine(spawnFolder, "8001.json");
        string layout = new JsonObject { ["MaxMonsterCount"] = 0, ["Placements"] = new JsonArray(
            new JsonObject { ["PlacementID"] = 101, ["Objects"] = new JsonArray(
                new JsonObject { ["ObjectID"] = 901, ["Type"] = "NPC", ["NpcID"] = npcId, ["Anchor"] = "Anchor_NPC" }) }) }.ToJsonString();
        var spawns = new SpawnJsonRule();
        IReadOnlyList<OutputFile> CollectSpawn() => spawns.Collect(root, snapshot, ready);
        void ChangeSpawn(Action<JsonNode> change)
        { var value = JsonNode.Parse(layout)!; change(value); File.WriteAllText(spawn, value.ToJsonString()); }
        File.WriteAllText(spawn, layout);
        check(CollectSpawn().Count == 1, "플랫폼 내부 NPC를 거부했습니다.");
        fails(() => spawns.Collect(root, snapshot, data with { Tables = data.Tables.Where(table => table.Schema.Name != "Npc").ToArray() }), "Npc 표가 없는 참조를 허용했습니다.");
        ChangeSpawn(value => value["Objects"] = new JsonArray()); fails(() => CollectSpawn(), "구형 최상위 Objects를 허용했습니다.");
        ChangeSpawn(value => value["Placements"]![0]!["Objects"]![0]!["ParentPlacementID"] = 0); fails(() => CollectSpawn(), "제거한 부모 필드를 허용했습니다.");
        ChangeSpawn(value => value["Placements"]![0]!["Objects"]![0]!["NpcID"] = int.MaxValue); fails(() => CollectSpawn(), "없는 NPC를 허용했습니다.");
        ChangeSpawn(value => value["Placements"]![0]!["Objects"]![0]!["ObjectID"] = 0); fails(() => CollectSpawn(), "배치 ID 0을 허용했습니다.");
        ChangeSpawn(value => value["Placements"]![0]!["Objects"]![0]!["Type"] = "SHOP"); fails(() => CollectSpawn(), "지원하지 않는 배치 Type을 허용했습니다.");
        ChangeSpawn(value => value["Placements"]![0]!["Objects"]![0]!["Anchor"] = "NPC/Anchor"); fails(() => CollectSpawn(), "범위를 벗어나는 앵커 이름을 허용했습니다.");
        ChangeSpawn(value => { var second = value["Placements"]![0]!.DeepClone(); second["PlacementID"] = 102; value["Placements"]!.AsArray().Add(second); });
        fails(() => CollectSpawn(), "다른 플랫폼 사이의 ObjectID 중복을 허용했습니다.");
        ChangeSpawn(value => { var second = value["Placements"]![0]!.DeepClone(); second["PlacementID"] = 102; second["Objects"]![0]!["ObjectID"] = 902; value["Placements"]!.AsArray().Add(second); });
        check(CollectSpawn().Count == 1, "다른 플랫폼의 같은 NPC·앵커 재사용을 거부했습니다.");
        ChangeSpawn(value => value["Placements"]![0]!["Objects"] = new JsonArray());
        check(CollectSpawn().Count == 1, "빈 Objects를 거부했습니다.");

        string client = workspace.Folder("NpcClient");
        Directory.CreateDirectory(Path.Combine(client, "Assets")); Directory.CreateDirectory(Path.Combine(client, "ProjectSettings"));
        File.WriteAllText(Path.Combine(client, "ProjectSettings", "ProjectVersion.txt"), "검사 자료");
        var deploy = new DeploymentService(); deploy.Deploy(Collect(), client, workspace);
        string target = Path.Combine(client, OutputCatalog.DialogueFolder, "24001.json");
        check(File.ReadAllText(target) == original, "신규 대사 배포에 실패했습니다.");
        Change(value => value["_note"] = "수정 배포 검사"); deploy.Deploy(Collect(), client, workspace);
        string updated = File.ReadAllText(target);
        check(updated.Contains("_note"), "수정 대사 배포에 실패했습니다.");
        Change(value => value["Lines"] = new JsonArray()); fails(() => Collect(), "배포 전 잘못된 대사를 허용했습니다.");
        check(File.ReadAllText(target) == updated, "검사 실패로 기존 배포 파일이 바뀌었습니다.");
        check(!OutputCatalog.IsOwnedPath(OutputCatalog.DialogueFolder + "/../24001.json"), "대사 경로 이탈을 허용했습니다.");
        File.WriteAllText(target + ".meta", "보존"); deploy.Deploy([], client, workspace);
        check(!File.Exists(target) && File.Exists(target + ".meta"), "대사 삭제 반영과 메타 보존에 실패했습니다.");
    }

    private static TypedTable Table(string name, params (string Name, object Value)[] fields)
    {
        var columns = fields.Select(field => new ColumnSchema(field.Name, field.Value is int ? FieldType.Integer : FieldType.Text)).ToArray();
        var cells = fields.Select((field, i) => new TypedCell(columns[i], field.Value, new SourceLocation("NPC 검사 자료", name, field.Name + "7")))
            .ToDictionary(cell => cell.Schema.Name);
        return new TypedTable(new SheetTable(name, OutputFormat.Csv, [], []), new TableSchema(name, OutputFormat.Csv, columns, []), [new TypedRow(7, cells)]);
    }
}
