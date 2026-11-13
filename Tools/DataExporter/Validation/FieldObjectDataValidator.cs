namespace DataSheet.Export;

// 클라이언트 실행 분기와 맞지 않는 활성 행은 배포 전에 중단합니다.
public sealed class FieldObjectDataValidator
{
    public void Validate(ProjectData data, string? root = null)
    {
        var tables = data.Tables.ToDictionary(table => table.Schema.Name, StringComparer.Ordinal);
        var categories = tables.TryGetValue("EnumDefinition", out var enums)
            ? enums.Rows.Where(row => (string)row.Cells["EnumName"].Value! == "EffectCategory")
                .Select(row => (string)row.Cells["SheetKey"].Value!).Where(key => key != "NONE").ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        if (tables.TryGetValue("StatusEffect", out var statusEffects))
        {
            var statusKinds = enums?.Rows.Where(row => (string)row.Cells["EnumName"].Value! == "StatusEffectKind")
                .Select(row => (string)row.Cells["SheetKey"].Value!).ToHashSet(StringComparer.Ordinal) ?? [];
            foreach (var row in statusEffects.Rows)
            {
                string kind = (string)row.Cells["Category"].Value!;
                if (!statusKinds.Contains(kind)) throw new InvalidDataException($"{row.Cells["Category"].Source}: 상태 효과 분류가 enum 시트에 없습니다.");
                if (kind == "Knockback" && ((int)row.Cells["Value"].Value! <= 0 || (int)row.Cells["Duration"].Value! <= 0))
                    throw new InvalidDataException($"{row.Cells["Value"].Source}: 넉백 거리와 시간은 양의 정수여야 합니다.");
            }
        }
        if (tables.TryGetValue("Monster", out var monsters))
            foreach (var row in monsters.Rows)
                if (row.Cells.TryGetValue("EffectImmunities", out var cell))
                    foreach (string key in ((string?)cell.Value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                        if (!categories.Contains(key))
                            throw new InvalidDataException($"{cell.Source}: EnumDefinition의 EffectCategory에 없는 효과 면역 분류입니다: {key}");
        bool hasFields = tables.TryGetValue("FieldObject", out var fields);
        bool hasPlacements = tables.TryGetValue("FieldObjectPlacement", out var placements);
        if (!hasFields && !hasPlacements) return;
        if (!hasFields || !hasPlacements) throw new InvalidDataException("FieldObject와 FieldObjectPlacement 탭은 함께 필요합니다.");
        foreach (var row in fields!.Rows.Where(row => (bool)row.Cells["ContentEnabled"].Value!))
        {
            string kind = (string)row.Cells["Kind"].Value!;
            int Number(string name) => (int)row.Cells[name].Value!;
            bool valid = kind switch {
                "FIRE_STATIC" => Number("Damage") > 0 && Number("TickMs") > 0,
                "ICE" => Number("KnockbackRate") > 0 && Number("SlideDistance") == 0 && Number("TurnMs") == 0,
                _ => false
            };
            if (!valid) throw new InvalidDataException($"{row.Cells["Kind"].Source}: 활성 필드의 실행 방식·양의 피해/주기·배율을 확인하세요. 현재 잔불과 넉백 보정 얼음만 지원합니다.");
        }
        if (root is null) return;
        var enabled = fields.Rows.Where(row => (bool)row.Cells["ContentEnabled"].Value!).Select(row => (int)row.Cells["ID"].Value!).ToHashSet();
        foreach (var stage in placements!.Rows.Where(row => enabled.Contains((int)row.Cells["FieldObjectID"].Value!)).GroupBy(row => (int)row.Cells["StageID"].Value!))
        {
            string path = Path.Combine(root, "spawn", stage.Key + ".json");
            if (!File.Exists(path)) throw new InvalidDataException("필드 배치에 연결할 스폰 JSON이 없습니다: " + path);
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var ids = document.RootElement.GetProperty("Placements").EnumerateArray().Select(value => value.GetProperty("PlacementID").GetInt32()).ToHashSet();
            foreach (var row in stage)
                if (!ids.Contains((int)row.Cells["PlacementID"].Value!)) throw new InvalidDataException($"{row.Cells["PlacementID"].Source}: 스폰 JSON에 연결할 플랫폼 배치가 없습니다.");
        }
    }
}
