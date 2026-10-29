using System.Text.RegularExpressions;

namespace DataSheet.Export;

public sealed record ProjectData(IReadOnlyList<TypedTable> Tables, IReadOnlyDictionary<string, SheetInstructions> Instructions)
{
    public static ProjectData Load(string root)
    {
        var result = new List<TypedTable>();
        var instructions = new Dictionary<string, SheetInstructions>(StringComparer.Ordinal);
        var issues = new List<DataIssue>();
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "tables"), "*.xlsx", SearchOption.AllDirectories).Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            RawWorkbook workbook = new ExcelTableReader().Read(path);
            foreach (SheetTable table in new SheetRuleReader().Read(workbook))
            {
                SheetInstructions instruction = new InlineSchemaReader().Read(workbook, table);
                if (!instructions.TryAdd(table.Name, instruction)) throw new InvalidDataException($"중복 탭 이름: {table.Name}");
                var rows = new List<TypedRow>();
                foreach (TableRow row in table.Rows)
                {
                    var cells = new Dictionary<string, TypedCell>(StringComparer.Ordinal);
                    for (int index = 0; index < table.Columns.Count; index++)
                    {
                        ColumnSchema field = instruction.Schema.Columns[index];
                        RawCell source = row.Cells[index];
                        try { cells.Add(field.Name, new TypedCell(field, CellValueConverter.Parse(source.Text, field), source.Source)); }
                        catch (FormatException error) { issues.Add(new DataIssue("TYPE", source.Source, field.Name, error.Message)); }
                    }
                    if (cells.Count == table.Columns.Count) rows.Add(new TypedRow(row.SourceRow, cells));
                }
                result.Add(new TypedTable(table, instruction.Schema, rows));
            }
        }
        if (result.Count == 0) throw new InvalidDataException("추출할 데이터 시트가 없습니다.");
        issues.AddRange(new DataValidator().Validate(result));
        if (issues.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, issues));
        return new(result.AsReadOnly(), instructions);
    }
}

public sealed class DataValidator
{
    public IEnumerable<DataIssue> Validate(IReadOnlyList<TypedTable> sources)
    {
        var tables = sources.ToDictionary(table => table.Schema.Name, StringComparer.Ordinal);
        foreach (TypedTable table in sources)
        {
            foreach (TypedRow row in table.Rows)
                foreach (TypedCell cell in row.Cells.Values)
                {
                    ColumnSchema field = cell.Schema;
                    if (field.ReferenceTable is string reference && !tables.ContainsKey(reference)) throw new InvalidDataException($"{table.Schema.Name}.{field.Name}: 참조 테이블 없음 {reference}");
                    if (cell.Value is null) continue;
                    string? error = cell.Value switch
                    {
                        int value when field.Minimum is int min && value < min || field.Maximum is int max && value > max => "정수 범위 오류",
                        string text when !field.AllowKorean && Regex.IsMatch(text, "[가-힣ㄱ-ㅎㅏ-ㅣ]") => "한글 허용 규칙이 없습니다.",
                        _ => null
                    };
                    if (field.AllowedValues is { } allowed && !allowed.Contains(CellValueConverter.Format(cell.Value), StringComparer.Ordinal)) error = "허용 값 목록에 없습니다.";
                    if (field.SharedStatKeysFrom is string statSource && !tables[statSource].Schema.Columns.Any(column => column.SharedCharacterStat && column.Name == (string)cell.Value)) error = "공통 스탯 키가 아닙니다.";
                    if (field.ReferenceTable is string target && !(field.ZeroMeansNone && cell.Value is int zero && zero == 0)
                        && !tables[target].Rows.Any(other => Equals(other.Cells["ID"].Value, cell.Value))) error = "참조 ID가 없습니다.";
                    if (error is not null) yield return new DataIssue("VALUE", cell.Source, field.Name, error);
                }
            foreach (ITableRule rule in table.Schema.Rules)
                foreach (DataIssue issue in rule.Validate(table, tables)) yield return issue;
        }
    }
}
