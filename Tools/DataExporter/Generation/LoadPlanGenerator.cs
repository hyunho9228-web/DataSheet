using System.Text;

namespace DataSheet.Export;

// 시트의 형식·상속·묶음 규칙으로 초기화 순서를 구성합니다.
public sealed class LoadPlanGenerator
{
    public string Generate(ProjectData data, string folder)
    {
        var code = new StringBuilder("// 시트 규칙에서 자동 생성한 로딩 계획입니다. 직접 수정하지 마세요.\nusing System.Threading;\nusing System.Threading.Tasks;\nusing HumanGhost.Data.Definitions;\nusing HumanGhost.Data.Storage;\n\nnamespace HumanGhost.Data.Infrastructure\n{\n    internal static class GeneratedDataLoadPlan\n    {\n");
        foreach (OutputFormat format in new[] { OutputFormat.Csv, OutputFormat.Json })
        {
            string phase = format == OutputFormat.Csv ? "Csv" : "Json";
            code.Append($"        public static async Task<DataCatalog> Load{phase}Async(BootstrapTableLoader loader, DataCatalog catalog, CancellationToken token)\n        {{\n");
            if (format == OutputFormat.Json) code.Append("            var reader = new HumanGhost.Data.Mapping.JsonTableReader();\n");
            foreach (TypedTable table in data.Tables.Where(table => table.Schema.Format == format)
                .OrderBy(table => Rank(data.Instructions[table.Schema.Name])).ThenBy(table => table.Schema.Name, StringComparer.Ordinal))
            {
                SheetInstructions instruction = data.Instructions[table.Schema.Name];
                string name = table.Schema.Name;
                if (format == OutputFormat.Csv)
                    code.Append($"            await loader.LoadCsvAsync<{name}>(catalog, row => row.ID, token);\n");
                else if (table.Schema.JsonGroupColumn is string group)
                {
                    ColumnSchema key = table.Schema.Columns.Single(column => column.Name == group);
                    if (key.Type != FieldType.Integer || key.Optional) throw new InvalidDataException($"{name}: 그룹 식별자는 필수 정수 컬럼이어야 합니다.");
                    if (instruction.AggregateName is string aggregate)
                        code.Append($"            await loader.LoadCharactersAsync<{name}, {aggregate}>(catalog, (common, states) => new {aggregate}(common, states), row => row.ID, token, \"{group}\");\n");
                    else code.Append($"            await loader.LoadGroupsAsync<{name}>(catalog, token, \"{group}\");\n");
                }
                else code.Append($"            await loader.LoadJsonAsync(catalog, \"{name}\", reader.Read<{name}>, row => row.ID, token);\n");
            }
            code.Append("            await Task.CompletedTask;\n            return catalog;\n        }\n");
        }
        code.Append("    }\n}\n");
        string path = Path.Combine(folder, "GeneratedDataLoadPlan.cs");
        File.WriteAllText(path, code.ToString(), new UTF8Encoding(false));
        return path;
    }

    private static int Rank(SheetInstructions instruction)
        => instruction.BaseName is not null ? 0 : instruction.InheritBase is not null ? 2 : 1;
}
