namespace DataSheet.Export;

public sealed class SheetRuleReader
{
    private static readonly HashSet<string> _excluded = new(["README", "Reference", "StatCurve", "Balance", "Formula"], StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<SheetTable> Read(RawWorkbook workbook)
    {
        var tables = new List<SheetTable>();
        foreach (RawSheet sheet in workbook.Sheets.Where(sheet => !_excluded.Contains(sheet.Name)))
        {
            RawCell Cell(int row, int column) => RawSheetAccess.Get(workbook.File, sheet, row, column);
            OutputFormat format = Cell(2, 2).Text switch
            {
                var value when value.StartsWith("CSV,", StringComparison.Ordinal) => OutputFormat.Csv,
                var value when value.StartsWith("JSON,", StringComparison.Ordinal) => OutputFormat.Json,
                _ => throw new SheetReadException(Cell(2, 2).Source, "B2는 CSV, 또는 JSON,으로 시작해야 합니다.")
            };
            var excluded = Cell(5, 2).Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
            if (excluded.Contains("ID")) throw new SheetReadException(Cell(5, 2).Source, "ID는 제외할 수 없습니다.");
            int last = sheet.Rows.TryGetValue(6, out var headers) ? headers.Where(pair => pair.Value.Text.Trim().Length > 0).Select(pair => pair.Key).DefaultIfEmpty().Max() : 0;
            var names = new HashSet<string>(StringComparer.Ordinal);
            var columns = new List<TableColumn>();
            for (int index = 1; index <= last; index++)
            {
                RawCell header = Cell(6, index);
                string name = header.Text.Trim();
                if (name.Length == 0 || !names.Add(name)) throw new SheetReadException(header.Source, "빈 헤더 또는 중복 헤더입니다.");
                if (!excluded.Contains(name)) columns.Add(new TableColumn(name, index, header.Source));
            }
            if (Cell(6, 1).Text.Trim() != "ID") throw new SheetReadException(Cell(6, 1).Source, "A6는 ID여야 합니다.");
            foreach (string name in excluded)
                if (!names.Contains(name)) throw new SheetReadException(Cell(5, 2).Source, $"제외 컬럼이 없습니다: {name}");
            var rows = new List<TableRow>();
            for (int index = 7; index <= sheet.Rows.Keys.DefaultIfEmpty(6).Max(); index++)
            {
                if (string.IsNullOrWhiteSpace(Cell(index, 1).Text)) break;
                rows.Add(new TableRow(index, columns.Select(column => Cell(index, column.SourceColumn)).ToArray()));
            }
            tables.Add(new SheetTable(sheet.Name, format, columns.AsReadOnly(), rows.AsReadOnly()));
        }
        return tables.AsReadOnly();
    }
}
