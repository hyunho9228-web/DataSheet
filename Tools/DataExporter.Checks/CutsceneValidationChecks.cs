using DataSheet.Export;

internal static class CutsceneValidationChecks
{
    public static void Run(ProjectData data, Action<bool, string> assert, Action<Action, string> fails)
    {
        var validator = new CutsceneDataValidator();
        ProjectData SetRows(ProjectData source, string name, params object?[][] values) => source with
        {
            Tables = source.Tables.Select(table => table.Schema.Name != name ? table : table with
            {
                Rows = values.Select((row, index) => new TypedRow(index + 7, table.Schema.Columns.Select((column, cell) =>
                    new TypedCell(column, row[cell], new SourceLocation("컷씬 검사 자료", name, $"{column.Name}{index + 7}")))
                    .ToDictionary(cell => cell.Schema.Name))).ToArray()
            }).ToArray()
        };
        validator.Validate(data);
        assert(true, "빈 컷씬 탭은 데이터 입력 대기 상태로 허용");
        ProjectData sequence = SetRows(data, "Cutscene", [18001, "Intro"]);
        fails(() => validator.Validate(sequence), "구간 없는 컷씬 거부");
        ProjectData Video(int duration = 0, bool advance = false, int order = 1, string address = "Cutscene/Intro/Main") =>
            SetRows(sequence, "CutsceneStep", [19001, 18001, order, "Video", address, duration, null, advance]);
        validator.Validate(Video());
        assert(true, "영상 시간0·다음 장 불가 허용");
        fails(() => validator.Validate(Video(duration: 1)), "영상 시간 검사");
        fails(() => validator.Validate(Video(advance: true)), "영상 다음 장 검사");
        fails(() => validator.Validate(Video(order: 2)), "재생 순서 시작·누락 검사");
        fails(() => validator.Validate(Video(address: "컷씬/영상")), "주소 ASCII 검사");
        ProjectData image = SetRows(sequence, "CutsceneStep", [19001, 18001, 1, "Image", "Cutscene/Ending/01", 4000, 30025, true]);
        validator.Validate(image);
        assert(true, "그림 표시 시간과 문구 허용");
        fails(() => validator.Validate(SetRows(sequence, "CutsceneStep", [19001, 18001, 1, "Image", "Cutscene/Ending/01", 0, null, true])), "그림 시간0 거부");
    }
}
