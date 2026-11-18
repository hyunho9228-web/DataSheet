using DataSheet.Export;

internal static class SoundSchemaChecks
{
    public static void Run(ProjectData data, Action<bool, string> check, Action<Action, string> fails)
    {
        var sound = data.Tables.Single(table => table.Schema.Name == "Sound");
        var enums = data.Tables.Single(table => table.Schema.Name == "EnumDefinition");
        var categories = enums.Rows.Where(row => Equals(row.Cells["EnumName"].Value, "SoundCategory"))
            .ToDictionary(row => (string)row.Cells["Key"].Value!, row => (int)row.Cells["Value"].Value!);
        check(sound.Schema.Columns.Single(field => field.Name == "Category").EnumName == "SoundCategory", "소리 분류의 enum 원본 연결이 필요합니다.");
        check(sound.Schema.Columns.Single(field => field.Name == "RefIndex").Optional, "공용 참조는 빈칸을 지원해야 합니다.");
        check(!new DataValidator().Validate(data.Tables).Any(), "현재 소리와 enum·참조 데이터가 유효해야 합니다.");

        IReadOnlyList<TypedTable> Changed(int category, int? reference)
        {
            var row = sound.Rows[0] with { Cells = sound.Rows[0].Cells.ToDictionary(pair => pair.Key,
                pair => pair.Value with { Value = pair.Key switch { "Category" => category, "RefIndex" => reference, _ => pair.Value.Value } }) };
            var altered = sound with { Rows = new[] { row }.Concat(sound.Rows.Skip(1)).ToArray() };
            return data.Tables.Select(table => table == sound ? altered : table).ToArray();
        }
        bool Rejected(int category, int? reference) => new DataValidator().Validate(Changed(category, reference)).Any();
        int Id(string table) => (int)data.Tables.Single(item => item.Schema.Name == table).Rows[0].Cells["ID"].Value!;

        foreach (var (key, tableName) in new[] { ("Ambience", "Stage"), ("Player", "Player"), ("Monster", "Monster") })
        {
            check(!Rejected(categories[key], Id(tableName)), "분류에 맞는 참조를 허용해야 합니다: " + key);
            check(Rejected(categories[key], null), "필수 대상 참조 누락을 막아야 합니다: " + key);
            check(Rejected(categories[key], 0), "참조 0을 허용하지 않습니다: " + key);
            check(Rejected(categories[key], int.MaxValue), "없는 참조 ID를 막아야 합니다: " + key);
        }
        var units = data.Tables.Single(table => table.Schema.Name == "UnitStat");
        int boss = (int)units.Rows.First(row => Equals(row.Cells["Type"].Value, "Boss")).Cells["ID"].Value!;
        int nonBoss = (int)units.Rows.First(row => !Equals(row.Cells["Type"].Value, "Boss")).Cells["ID"].Value!;
        check(!Rejected(categories["Boss"], boss), "보스의 UnitStat 참조를 허용해야 합니다.");
        check(Rejected(categories["Boss"], nonBoss), "일반 UnitStat를 보스 참조로 허용하지 않습니다.");
        check(!Rejected(categories["Common"], null), "공용은 빈 참조를 허용해야 합니다.");
        check(Rejected(categories["Common"], Id("Stage")), "공용의 불필요한 참조를 막아야 합니다.");
        check(Rejected(int.MaxValue, null), "시트에 없는 enum 값을 막아야 합니다.");

        var missing = sound with { Schema = sound.Schema with { Columns = sound.Schema.Columns.Select(field =>
            field.Name == "Category" ? field with { EnumName = "MissingSoundCategory" } : field).ToArray() } };
        fails(() => new DataValidator().Validate(data.Tables.Select(table => table == sound ? missing : table).ToArray()).ToArray(),
            "존재하지 않는 enum 원본을 허용했습니다.");
        var textEnum = missing with { Schema = missing.Schema with { Columns = missing.Schema.Columns.Select(field =>
            field.Name == "Category" ? field with { Type = FieldType.Text, EnumName = "SoundCategory" } : field).ToArray() } };
        fails(() => new DataValidator().Validate(data.Tables.Select(table => table == sound ? textEnum : table).ToArray()).ToArray(),
            "정수가 아닌 enum 컬럼을 허용했습니다.");
        fails(() => new TableRule(new RuleValues("Kind=ConditionalReference;WhenColumn=Category;WhenValue=1;Column=RefIndex;Table=Stage;TargetColumn=Type")),
            "대상 조건의 짝이 빠진 규칙을 허용했습니다.");
        fails(() => new TableRule(new RuleValues("Kind=ConditionalValue;WhenColumn=Category;WhenValue=1;Column=RefIndex")),
            "비교할 Value가 빠진 규칙을 허용했습니다.");
    }
}
