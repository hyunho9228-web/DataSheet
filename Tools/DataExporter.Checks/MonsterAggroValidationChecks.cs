using DataSheet.Export;

internal static class MonsterAggroValidationChecks
{
    public static void Run(ProjectData data, Action<bool, string> check)
    {
        var table = data.Tables.Single(item => item.Schema.Name == "MonsterAggro");
        var source = table.Rows[0];
        var directions = new[] {"FrontRange", "BackRange", "UpRange", "DownRange", "FrontUpRange", "BackUpRange", "FrontDownRange", "BackDownRange"};
        bool Rejected(string column, int value)
        {
            var altered = source with { Cells = source.Cells.ToDictionary(pair => pair.Key,
                pair => pair.Key == column ? pair.Value with { Value = value } : pair.Value) };
            var replacement = table with { Rows = new[] {altered}.Concat(table.Rows.Skip(1)).ToArray() };
            return new DataValidator().Validate(data.Tables.Select(item => item == table ? replacement : item).ToArray()).Any();
        }
        check(!new DataValidator().Validate(data.Tables).Any(), "8방향 원본 규칙이 유효해야 합니다.");
        foreach (string column in directions.Append("SameLevelHeight"))
        {
            check(source.Cells.ContainsKey(column), "8방향 인식 컬럼 누락: " + column);
            check(Rejected(column, -1), "인식 음수 검사 누락: " + column);
        }
        foreach (string column in directions.Where(name => name != "UpRange" && name != "DownRange"))
            check(Rejected(column, (int)source.Cells["LoseRange"].Value! + 1), "가로 해제 거리보다 큰 값 검사 누락: " + column);
        foreach (string column in directions.Where(name => name != "FrontRange" && name != "BackRange").Append("SameLevelHeight"))
            check(Rejected(column, (int)source.Cells["LoseHeight"].Value! + 1), "해제 높이보다 큰 값 검사 누락: " + column);
        check(Rejected("LeashRange", 0), "복귀 거리 0 검사 누락");
        check(Rejected("MonsterID", int.MaxValue), "인식 표 Monster 참조 검사 누락");
    }
}
