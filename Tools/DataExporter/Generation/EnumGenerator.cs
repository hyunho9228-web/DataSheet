using System.Text;

namespace DataSheet.Export;

// 공용 enum과 입력 키 변환만 생성합니다. 최신 도구 문법은 출력 문자열에 포함하지 않습니다.
public sealed class EnumGenerator
{
    public void Generate(ProjectData data, string folder)
    {
        var table = data.Tables.FirstOrDefault(table => table.Schema.Name == "EnumDefinition");
        if (table is null) return;
        Directory.CreateDirectory(folder);
        foreach (var group in table.Rows.GroupBy(row => (string)row.Cells["EnumName"].Value!))
        {
            string name = NamePolicy.Require(group.Key);
            var rows = group.OrderBy(row => (int)row.Cells["Value"].Value!).ToArray();
            var code = new StringBuilder("// 시트에서 자동 생성합니다. 매 배포 갱신하므로 직접 수정하지 마세요.\nnamespace HumanGhost.Data.Definitions\n{\n");
            code.Append($"    public enum {name}\n    {{\n");
            foreach (var row in rows) code.Append($"        {NamePolicy.Require((string)row.Cells["Key"].Value!)} = {row.Cells["Value"].Value},\n");
            code.Append($"    }}\n    public static class {name}Keys\n    {{\n        public static {name} Parse(string key)\n        {{\n            switch (key)\n            {{\n");
            foreach (var row in rows)
                code.Append($"                case \"{(string)row.Cells["SheetKey"].Value!}\": return {name}.{row.Cells["Key"].Value};\n");
            code.Append("                default: throw new System.InvalidOperationException(\"열거형 입력 키를 해석하지 못했습니다: \" + key);\n            }\n        }\n    }\n}\n");
            File.WriteAllText(Path.Combine(folder, name + ".cs"), code.ToString(), new UTF8Encoding(false));
        }
    }
}
