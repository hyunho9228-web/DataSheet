using System.Text.Json;
using static DataSheet.Export.JsonAssetFields;

namespace DataSheet.Export;

public sealed class GalimgilFileCollector
{
    public IReadOnlyList<OutputFile> Collect(string root, string snapshotFolder, ProjectData data)
    {
        string source = Path.Combine(root, "stats", "galimgil");
        if (!Directory.Exists(source)) return [];
        if (!File.Exists(Path.Combine(source, "galimgil_reward.json")))
            throw new InvalidDataException("갈림길 데이터의 galimgil_reward.json이 없습니다.");
        var reference = References(data, "Currency", "Stage");
        void Validate(JsonElement value, string file)
        {
            reference("Currency", Integer(value, "soulCurrencyId", 1, file), file);
            foreach (string field in new[] { "baseSoul", "soulPerKill", "soulPerInteraction" }) Integer(value, field, 0, file);
            if (Text(value, "rounding", file) != "floor") throw new InvalidDataException($"갈림길 보상은 floor만 지원합니다: {file}");
            Boolean(value, "showGrade", file);
            var stages = new HashSet<int>();
            foreach (JsonElement stage in Rows(value, "countStages", file).EnumerateArray())
            {
                if (stage.ValueKind != JsonValueKind.Number || !stage.TryGetInt32(out int id) || !stages.Add(id))
                    throw new InvalidDataException($"갈림길 countStages는 중복 없는 스테이지 정수 ID여야 합니다: {file}");
                reference("Stage", id, file);
            }
            if (stages.Count == 0) throw new InvalidDataException($"갈림길 집계 스테이지가 비어 있습니다: {file}");
            var names = new HashSet<string>(StringComparer.Ordinal);
            long previous = (long)int.MaxValue + 1;
            foreach (JsonElement grade in Rows(value, "grades", file).EnumerateArray())
            {
                string name = Text(grade, "grade", file);
                int minimum = Integer(grade, "minKillCount", 0, file);
                if (!NamePolicy.IsStem(name) || !names.Add(name) || minimum >= previous)
                    throw new InvalidDataException($"갈림길 등급 이름은 고유한 영문 키이며 처치 기준은 큰 값부터 중복 없이 작성해야 합니다: {file}");
                previous = minimum;
            }
            if (names.Count == 0 || previous != 0) throw new InvalidDataException($"갈림길 마지막 등급의 처치 기준은 0이어야 합니다: {file}");
        }
        return new JsonAssetCollector().Collect(source, OutputCatalog.GalimgilFolder,
            snapshotFolder, OutputCatalog.IsGalimgilPath, Validate);
    }
}
