using System.Text.Json;

namespace DataSheet.Export;

// 오브젝트는 자신을 포함한 플랫폼의 앵커만 사용합니다. 실제 앵커의 존재는 Unity 조립 경계에서 확인합니다.
public class SpawnObjectValidator
{
    public void Validate(JsonElement root, string source, ProjectData? data)
    {
        if (root.TryGetProperty("Objects", out _))
            throw new InvalidDataException($"스폰 Objects는 최상위가 아니라 각 Placements 항목 안에 작성해 주세요: {source}");
        if (!root.TryGetProperty("Placements", out var placements)) return;
        placements = JsonAssetFields.Rows(root, "Placements", source);
        var objectIds = new HashSet<int>();
        var placementIds = new HashSet<int>();
        Action<string, int, string> reference = JsonAssetFields.References(data ?? new ProjectData([], new Dictionary<string, SheetInstructions>()), "Npc");
        foreach (JsonElement placement in placements.EnumerateArray())
        {
            if (placement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"스폰 Placements의 각 항목은 객체여야 합니다: {source}");
            // 기존 플랫폼 배치 검사와 별개로, Objects를 쓰는 부모의 식별은 반드시 고유해야 합니다.
            if (placement.TryGetProperty("PlacementID", out _))
            {
                int id = JsonAssetFields.Integer(placement, "PlacementID", 1, source);
                if (!placementIds.Add(id)) throw new InvalidDataException($"스폰 PlacementID {id}가 중복됩니다: {source}");
            }
            if (placement.TryGetProperty("ParentPlacementID", out _)) RejectParent(source);
            if (!placement.TryGetProperty("Objects", out _)) continue;
            JsonElement objects = JsonAssetFields.Rows(placement, "Objects", source);
            if (objects.GetArrayLength() == 0) continue;
            int parent = JsonAssetFields.Integer(placement, "PlacementID", 1, source);
            foreach (JsonElement item in objects.EnumerateArray())
            {
                string location = $"{source} / PlacementID {parent}";
                int id = JsonAssetFields.Integer(item, "ObjectID", 1, location);
                location += $" / ObjectID {id}";
                if (!objectIds.Add(id)) throw new InvalidDataException($"스테이지 안에서 ObjectID {id}가 중복됩니다: {location}");
                if (item.TryGetProperty("ParentPlacementID", out _)) RejectParent(location);
                if (JsonAssetFields.Text(item, "Type", location) != "NPC")
                    throw new InvalidDataException($"지원하는 스폰 Objects Type은 NPC입니다: {location}");
                string anchor = JsonAssetFields.Text(item, "Anchor", location);
                if (!NamePolicy.IsStem(anchor))
                    throw new InvalidDataException($"NPC Anchor는 영문 식별자여야 합니다: {location}");
                reference("Npc", JsonAssetFields.Integer(item, "NpcID", 1, location), location);
            }
        }
    }

    private static void RejectParent(string source) => throw new InvalidDataException(
        $"ParentPlacementID는 사용하지 않습니다. NPC를 부모 플랫폼의 Objects 안으로 옮겨 주세요: {source}");
}
