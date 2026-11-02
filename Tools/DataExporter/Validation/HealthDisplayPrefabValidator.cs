namespace DataSheet.Export;

// 화면 설정의 파일 참조는 배포 전에 검사합니다. Unity 컴포넌트 연결은 에디터가 검사합니다.
public sealed class HealthDisplayPrefabValidator
{
    public void Validate(ProjectData data, string client)
    {
        TypedTable? table = data.Tables.SingleOrDefault(value => value.Schema.Name == "HealthDisplayConfig");
        if (table is null) return;
        string folder = Path.Combine(client, "Assets", "2.Model", "Prefabs", "UI");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.prefab", SearchOption.AllDirectories) : [])
            if (!keys.Add(Path.GetFileNameWithoutExtension(path)))
                throw new InvalidDataException($"체력 표시 프리팹 이름 중복: {Path.GetFileNameWithoutExtension(path)}");
        foreach (TypedRow row in table.Rows)
        {
            TypedCell cell = row.Cells["PrefabKey"];
            if (cell.Value is not string key || !keys.Contains(key))
                throw new InvalidDataException($"{cell.Source.File} / {cell.Source.Sheet} / {cell.Source.Cell}: 체력 표시 프리팹이 없습니다: {cell.Value}. 클라이언트 UI 폴더의 파일명을 확인해주세요.");
        }
    }
}
