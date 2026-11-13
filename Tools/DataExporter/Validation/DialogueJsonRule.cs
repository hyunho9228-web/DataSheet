using System.Text.Json;

namespace DataSheet.Export;

// 대사 파일도 기존 JSON 검사·스냅샷·원복 가능한 배포 경로를 사용합니다.
public class DialogueJsonRule : JsonAssetRule
{
    public override string SourceFolder => "dialogue";
    protected override JsonAssetValidation Prepare(string sourceFolder, ProjectData data)
    {
        TypedTable? dialogues = data.Tables.SingleOrDefault(table => table.Schema.Name == "Dialogue");
        Action<string, int, string> reference = JsonAssetFields.References(data, "Dialogue", "Language", "BossState", "UnitStat");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

    private static T Cell<T>(TypedRow row, string field, string location) =>
        row.Cells.TryGetValue(field, out var cell) && cell.Value is T value ? value
            : throw new InvalidDataException($"{location} / {field}에 {(typeof(T) == typeof(int) ? "정수" : "문자열")} 값이 필요합니다.");
}
