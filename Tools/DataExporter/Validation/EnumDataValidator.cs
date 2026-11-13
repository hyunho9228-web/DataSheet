namespace DataSheet.Export;

public sealed class EnumDataValidator
{
    public void Validate(ProjectData data)
    {
        var table = data.Tables.FirstOrDefault(table => table.Schema.Name == "EnumDefinition");
        if (table is null) return;
        var reserved = data.Tables.Select(table => table.Schema.Name).Append("StateKey").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groups = table.Rows.GroupBy(row => (string)row.Cells["EnumName"].Value!).ToArray();
        if (groups.Select(group => group.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != groups.Length)
            throw new InvalidDataException("열거형 이름의 대소문자 중복을 확인하세요.");
        var names = new HashSet<string>(reserved, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            string name = NamePolicy.Require(group.Key);
            if (!names.Add(name) || !names.Add(name + "Keys")) throw new InvalidDataException("열거형과 기존 타입 이름이 겹칩니다: " + name);
            foreach (string column in new[] { "Key", "Value", "SheetKey" })
                if (group.Select(row => row.Cells[column].Value).Distinct().Count() != group.Count())
                    throw new InvalidDataException($"{name}: {column} 값이 중복됩니다.");
            foreach (var row in group)
            {
                NamePolicy.Require((string)row.Cells["Key"].Value!);
                // 출력 코드의 문자열에 직접 들어갈 수 있는 안정적인 키만 허용합니다.
                if (!NamePolicy.IsStem((string)row.Cells["SheetKey"].Value!)) throw new InvalidDataException("열거형 시트 키는 공백 없는 영문·숫자·밑줄을 사용하세요.");
            }
        }
    }
}
