namespace DataSheet.Export;

// 타입·중복·참조는 기존 규칙을 사용하고, 컷씬에만 있는 구간 조건을 검사합니다.
public sealed class CutsceneDataValidator
{
    public void Validate(ProjectData data)
    {
        var tables = data.Tables.ToDictionary(table => table.Schema.Name, StringComparer.Ordinal);
        bool hasSequences = tables.TryGetValue("Cutscene", out TypedTable? sequences);
        bool hasSteps = tables.TryGetValue("CutsceneStep", out TypedTable? steps);
        if (!hasSequences && !hasSteps) return;
        if (!hasSequences || !hasSteps) throw new InvalidDataException("Cutscene·CutsceneStep 탭은 함께 있어야 합니다.");
        var groups = steps!.Rows.GroupBy(row => (int)row.Cells["CutsceneID"].Value!).ToDictionary(group => group.Key);
        var issues = new List<DataIssue>();
        void Require(bool valid, TypedCell cell, string message)
        {
            if (!valid) issues.Add(new DataIssue("CUTSCENE", cell.Source, cell.Schema.Name, message));
        }
        foreach (TypedRow row in sequences!.Rows)
        {
            int id = (int)row.Cells["ID"].Value!;
            Require(groups.TryGetValue(id, out var group), row.Cells["ID"], "재생 구간이 없는 컷씬입니다.");
            if (group is null) continue;
            int expected = 1;
            foreach (TypedRow step in group.OrderBy(step => (int)step.Cells["Order"].Value!))
                Require((int)step.Cells["Order"].Value! == expected++, step.Cells["Order"], "재생 순서는 1부터 빈 번호 없이 이어져야 합니다.");
        }
        foreach (TypedRow row in steps.Rows)
        {
            string media = (string)row.Cells["MediaType"].Value!;
            int duration = (int)row.Cells["DurationMs"].Value!;
            bool advance = (bool)row.Cells["CanAdvance"].Value!;
            Require(media == "Video" ? duration == 0 : duration > 0, row.Cells["DurationMs"], "영상 시간은 0, 그림 시간은 양수 밀리초여야 합니다.");
            Require(media != "Video" || !advance, row.Cells["CanAdvance"], "영상은 다음 장 입력을 허용하지 않습니다.");
            string address = (string)row.Cells["ResourceAddress"].Value!;
            Require(address.All(character => character >= 33 && character <= 126), row.Cells["ResourceAddress"], "주소는 공백 없는 영문 ASCII로 작성합니다.");
        }
        if (issues.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, issues));
    }
}
