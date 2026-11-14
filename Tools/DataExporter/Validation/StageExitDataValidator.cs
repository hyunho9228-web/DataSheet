namespace DataSheet.Export;

// 행의 연결·형식은 공급 도구가 검사하고 실제 앵커 수명은 맵 조립자가 소유합니다.
public sealed class StageExitDataValidator
{
    public void Validate(ProjectData data)
    {
        TypedTable? table = data.Tables.SingleOrDefault(table => table.Schema.Name == "StageExit");
        if (table is null) return;
        // 같은 갈림길(출발 StageID) 안에서 경로 통계 번호가 겹치면 두 길이 한 경로로 집계됩니다.
        var routes = new Dictionary<(int Stage, int Route), int>();
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
            // Travel은 다른 맵으로 이동하고 FinishDemo는 시연을 마무리합니다. 가짜 목적지를 두지 않습니다.
            string action = row.Cells.TryGetValue("ExitAction", out var actionCell) ? (string?)actionCell.Value ?? "Travel" : "Travel";
            int target = (int)row.Cells["TargetStageID"].Value!;
            string arrival = (string?)row.Cells["ArrivalAnchorName"].Value ?? "";
            if (action == "Travel")
            {
                if (target <= 0) Fail("TargetStageID", "이동 출구에는 목적지 Stage ID가 필요합니다.");
                if (!NamePolicy.IsStem(arrival) || arrival == "value__") Fail("ArrivalAnchorName", "이동 출구에는 목적지 Markers의 영문 앵커 이름이 필요합니다.");
            }
            else
            {
                if (target != 0) Fail("TargetStageID", "시연 마무리 출구의 목적지는 0이어야 합니다.");
                if (arrival.Length != 0) Fail("ArrivalAnchorName", "시연 마무리 출구에는 도착 앵커를 비워 주세요.");
                if (row.Cells.TryGetValue("RouteID", out var finishRoute) && finishRoute.Value is int finishRouteId && finishRouteId != 0) Fail("RouteID", "시연 마무리 출구는 경로 통계 대상이 아닙니다.");
            }
            if (row.Cells.TryGetValue("RouteID", out var routeCell) && routeCell.Value is int route && route > 0)
            {
                int stage = (int)row.Cells["StageID"].Value!, id = (int)row.Cells["ID"].Value!;
                if (routes.TryGetValue((stage, route), out int other)) Fail("RouteID", $"같은 StageID의 출구 ID {other}와 경로 번호가 겹칩니다.");
                routes.Add((stage, route), id);
            }
            void Fail(string name, string reason)
            {
                var source = row.Cells[name].Source;
                throw new InvalidDataException($"{source.File} / {source.Sheet} / {source.Cell}: {reason}");
            }
        }
    }
}
