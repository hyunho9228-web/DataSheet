namespace DataSheet.Export;

// 행의 연결·형식은 공급 도구가 검사하고 실제 앵커 수명은 맵 조립자가 소유합니다.
public sealed class StageExitDataValidator
{
    public void Validate(ProjectData data)
    {
        TypedTable? table = data.Tables.SingleOrDefault(table => table.Schema.Name == "StageExit");
        if (table is null) return;
        foreach (TypedRow row in table.Rows)
        {
            string kind = (string)row.Cells["TriggerType"].Value!;
            string anchor = (string?)row.Cells["AnchorName"].Value ?? "";
            int x = (int)row.Cells["RangeX"].Value!, y = (int)row.Cells["RangeY"].Value!;
            bool anchored = kind is "Area" or "Interact";
            if (anchored && !NamePolicy.IsStem(anchor)) Fail("AnchorName", "출발 맵 Markers의 영문 앵커 이름이 필요합니다.");
            if (!anchored && anchor.Length != 0) Fail("AnchorName", "맵 끝 출구에는 앵커 이름을 비워 주세요.");
            if (kind is "LeftEdge" or "RightEdge" && x <= 0) Fail("RangeX", "맵 끝 출구의 가로 범위는 양의 정수여야 합니다.");
            if (kind == "Area" && (x <= 0 || y <= 0)) Fail("RangeX", "영역 출구의 가로·세로 반폭은 양의 정수여야 합니다.");
            if (kind == "Interact" && (int)row.Cells["PromptTextID"].Value! <= 0) Fail("PromptTextID", "문 상호작용의 안내 Language ID가 필요합니다.");
            void Fail(string name, string reason)
            {
                var source = row.Cells[name].Source;
                throw new InvalidDataException($"{source.File} / {source.Sheet} / {source.Cell}: {reason}");
            }
        }
    }
}
