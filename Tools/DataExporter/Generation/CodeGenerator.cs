using System.Text;

namespace DataSheet.Export;

public sealed class CodeGenerator
{
    private const string _header = "// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.\nnamespace HumanGhost.Data.Definitions\n{\n";
    public void Generate(ProjectData data, string folder)
    {
        var baseTables = data.Instructions.Values.Where(value => value.BaseName is not null).ToDictionary(value => value.BaseName!, StringComparer.Ordinal);
        foreach (SheetInstructions value in baseTables.Values)
            Write(value.BaseName!, value.Schema.Columns.Where(column => column.SharedCharacterStat).ToArray(), null, [], true);
        foreach (TypedTable table in data.Tables)
        {
            SheetInstructions instruction = data.Instructions[table.Schema.Name];
            string? baseName = instruction.BaseName ?? instruction.InheritBase;
            ColumnSchema[] common = baseName is null ? [] : baseTables.TryGetValue(baseName, out SheetInstructions? source)
                ? source.Schema.Columns.Where(column => column.SharedCharacterStat).ToArray() : throw new InvalidDataException($"기본 스탯 정의가 없습니다: {baseName}");
            Write(table.Schema.Name, table.Schema.Columns.Where(column => !common.Any(shared => shared.Name == column.Name)).ToArray(), baseName, common, false);
            if (instruction.AggregateName is string aggregate)
            {
                SheetInstructions baseSource = baseTables.Values.Single();
                string code = _header + $"    public sealed class {aggregate} : {baseSource.BaseName}\n    {{\n        public int ID {{ get; }}\n        public System.Collections.Generic.IReadOnlyList<{table.Schema.Name}> {instruction.AggregateProperty} {{ get; }}\n"
                    + $"        public {aggregate}({baseSource.Schema.Name} common, System.Collections.Generic.IEnumerable<{table.Schema.Name}> states)\n            : base({string.Join(", ", baseSource.Schema.Columns.Where(column => column.SharedCharacterStat).Select(column => "common." + column.Name))})\n        {{\n            ID = common.ID;\n            {instruction.AggregateProperty} = System.Array.AsReadOnly(System.Linq.Enumerable.ToArray(states));\n        }}\n    }}\n}}\n";
                Save(aggregate, code);
            }
        }
        if (data.Tables.FirstOrDefault(table => table.Schema.Name == "State") is TypedTable state)
            Save("StateKey", _header + "    public enum StateKey\n    {\n" + string.Join("\n", state.Rows.Select(row => $"        {row.Cells["Key"].Value} = {row.Cells["ID"].Value},")) + "\n    }\n}\n");

        void Save(string name, string code)
        {
            string path = Path.Combine(folder, name + ".cs");
            using var stream = new FileStream(path, FileMode.CreateNew);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)); writer.Write(code);
        }
        void Write(string name, IReadOnlyList<ColumnSchema> fields, string? parent, IReadOnlyList<ColumnSchema> shared, bool abstractType)
        {
            // 하위 캐릭터는 공통 정의를 전달받고, 공통 컬럼은 기본 클래스에서만 선언합니다.
            bool inheritCommon = parent is not null && data.Instructions[name].InheritBase is not null;
            string commonType = inheritCommon ? baseTables[parent!].Schema.Name : "";
            IEnumerable<ColumnSchema> arguments = parent is not null && !inheritCommon ? data.Instructions[name].Schema.Columns : fields;
            string parameters = string.Join(", ", (inheritCommon ? new[] { commonType + " common" } : Array.Empty<string>())
                .Concat(arguments.Select(column => CellValueConverter.CodeType(column) + " " + NamePolicy.Parameter(column.Name))));
            string code = _header + $"    public {(abstractType ? "abstract" : "sealed")} class {name}" + (parent is null ? "" : " : " + parent) + "\n    {\n"
                + string.Join("", fields.Select(column => $"        public {CellValueConverter.CodeType(column)} {column.Name} {{ get; }}\n"))
                + $"        {(abstractType ? "protected" : "public")} {name}({parameters})";
            if (parent is not null) code += "\n            : base(" + string.Join(", ", shared.Select(column => inheritCommon ? "common." + column.Name : NamePolicy.Parameter(column.Name))) + ")";
            code += "\n        {\n" + string.Join("", fields.Select(column => $"            {column.Name} = {NamePolicy.Parameter(column.Name)};\n")) + "        }\n    }\n}\n";
            Save(name, code);
        }
    }
}
