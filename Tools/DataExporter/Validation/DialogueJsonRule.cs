using System.Text.Json;

namespace DataSheet.Export;

// 대사 파일도 기존 JSON 검사·스냅샷·원복 가능한 배포 경로를 사용합니다.
public class DialogueJsonRule : JsonAssetRule
{
    public override string SourceFolder => "dialogue";
    protected override JsonAssetValidation Prepare(string sourceFolder, ProjectData data)
    {
        TypedTable? dialogues = data.Tables.SingleOrDefault(table => table.Schema.Name == "Dialogue");
        Action<string, int, string> reference = JsonAssetFields.References(data, "Dialogue", "Language", "BossState", "UnitStat", "Stage");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ledgerStages = new HashSet<int>();
        if (dialogues is not null)
            foreach (TypedRow row in dialogues.Rows)
            {
                string location = $"Dialogue / 행 {row.SourceRow}";
                int id = Cell<int>(row, "ID", location);
                string name = Cell<string>(row, "LineFile", location);
                if (id <= 0 || name != $"{id}.json" || !names.Add(name))
                    throw new InvalidDataException($"Dialogue LineFile은 ID와 같은 중복 없는 파일명이어야 합니다: {location}");
                int target = Cell<int>(row, "TriggerTargetID", location);
                switch (Cell<string>(row, "TriggerType", location))
                {
                    case "TALK" when target == 0: break;
                    case "BOSS_STATE_ENTER": reference("BossState", target, location); break;
                    case "BOSS_DEFEATED": reference("UnitStat", target, location); break;
                    case LedgerTrigger:
                        // 명부 조사 완료 단위는 Stage입니다. 클라이언트는 Stage마다 대화 하나와 해금 Skill 하나를 요구합니다.
                        reference("Stage", target, location);
                        if (!ledgerStages.Add(target))
                            throw new InvalidDataException($"Stage {target}의 {LedgerTrigger} 대화가 중복됐습니다: {location}");
                        RequireLedgerSkill(data, target, location);
                        break;
                    default: throw new InvalidDataException($"Dialogue TriggerType과 TriggerTargetID를 확인해 주세요: {location}");
                }
            }

        return new((value, source) =>
            {
                int id = JsonAssetFields.Integer(value, "DialogueID", 1, source);
                reference("Dialogue", id, source);
                string name = Path.GetRelativePath(sourceFolder, source);
                if (name != $"{id}.json" || !names.Contains(name))
                    throw new InvalidDataException($"대사 파일 이름·DialogueID·Dialogue.LineFile이 일치하지 않습니다: {source}");
                JsonElement lines = JsonAssetFields.Rows(value, "Lines", source);
                if (lines.GetArrayLength() == 0) throw new InvalidDataException($"대사 Lines에 한 줄 이상 필요합니다: {source}");
                int order = 0;
                foreach (JsonElement line in lines.EnumerateArray())
                {
                    string location = $"{source} / Lines {++order}";
                    foreach (string field in new[] { "SpeakerTextID", "TextID" })
                        reference("Language", JsonAssetFields.Integer(line, field, 1, location), location);
                }
            }, files =>
            {
                var supplied = files.Select(file => Path.GetFileName(file.RelativePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                string[] missing = names.Except(supplied).ToArray();
                if (missing.Length > 0) throw new InvalidDataException($"Dialogue가 참조하는 대사 JSON이 없습니다: {string.Join(", ", missing)}");
            });
    }

    public const string LedgerTrigger = "LEDGER_INVESTIGATE", LedgerSkillUnlockType = "ROOM_LEDGER";
    private const int StageClearedCondition = 1;

    // 명부 대화가 끝난 뒤 적용할 Skill이 Stage 클리어 조건으로 정확히 하나 연결됐는지 배포 전에 확인합니다.
    private static void RequireLedgerSkill(ProjectData data, int stageId, string location)
    {
        TypedTable? skills = data.Tables.SingleOrDefault(table => table.Schema.Name == "Skill");
        TypedRow[] matches = skills is null ? [] : skills.Rows.Where(row =>
            row.Cells.TryGetValue("UnlockType", out var type) && type.Value is string text && text == LedgerSkillUnlockType
            && row.Cells.TryGetValue("UnlockTargetID", out var target) && target.Value is int id && id == stageId).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException($"{LedgerTrigger} 대화에는 Skill UnlockType={LedgerSkillUnlockType}, UnlockTargetID={stageId} 행이 하나 필요합니다(현재 {matches.Length}개): {location}");
        string skill = $"Skill / 행 {matches[0].SourceRow}";
        if (Cell<int>(matches[0], "UnlockConditionType", skill) != StageClearedCondition || Cell<int>(matches[0], "UnlockRequiredValue", skill) > 1)
            throw new InvalidDataException($"명부 해금 Skill은 UnlockConditionType {StageClearedCondition}(Stage 클리어)·UnlockRequiredValue 1 이하여야 합니다: {skill}");
    }

    private static T Cell<T>(TypedRow row, string field, string location) =>
        row.Cells.TryGetValue(field, out var cell) && cell.Value is T value ? value
            : throw new InvalidDataException($"{location} / {field}에 {(typeof(T) == typeof(int) ? "정수" : "문자열")} 값이 필요합니다.");
}
