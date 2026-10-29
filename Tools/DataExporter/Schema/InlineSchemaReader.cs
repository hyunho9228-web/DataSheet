namespace DataSheet.Export;

public sealed record SheetInstructions(TableSchema Schema, string? BaseName, string? InheritBase, string? AggregateName, string AggregateProperty);

public sealed class InlineSchemaReader
{
    public SheetInstructions Read(RawWorkbook workbook, SheetTable table)
    {
        RawSheet raw = workbook.Sheets.Single(sheet => sheet.Name == table.Name);
        var columns = new Dictionary<string, ColumnSchema>(StringComparer.Ordinal);
        var rules = new List<ITableRule>();
        string? group = null, baseName = null, inherit = null, aggregate = null;
        string property = "States";
        foreach (string line in RawSheetAccess.Get(workbook.File, raw, 1, 2).Text.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith('@')))
        {
            int split = line.IndexOf(' ');
            if (split < 0) throw new InvalidDataException($"{table.Name}: 지정 규칙에 내용이 없습니다: {line}");
            string tag = line[..split];
            var values = new RuleValues(line[(split + 1)..]);
            switch (tag)
            {
                case "@column":
                    string name = values.Required("Name");
                    if (!Enum.TryParse(values.Required("Type"), out FieldType type) || !Enum.IsDefined(type)) throw new InvalidDataException($"{name}: 타입 오류");
                    columns.Add(name, new ColumnSchema(name, type, values.Flag("Optional"), values.Number("Minimum"), values.Number("Maximum"),
                        values.Get("Unit") ?? "", values.Get("AllowedValues")?.Split('|'), values.Get("ReferenceTable"), values.Flag("ZeroMeansNone"),
                        values.Flag("AllowKorean"), values.Flag("SharedCharacterStat"), values.Get("SharedStatKeysFrom")));
                    break;
                case "@check": rules.Add(new TableRule(values)); break;
                case "@group": group = values.Required("Column"); break;
                case "@base": baseName = values.Required("Name"); break;
                case "@inherit": inherit = values.Required("Base"); break;
                case "@aggregate": aggregate = values.Required("Name"); property = values.Get("Property") ?? "States"; break;
                default: throw new InvalidDataException($"{table.Name}: 알 수 없는 규칙 {tag}");
            }
            values.RequireConsumed();
        }
        columns.TryAdd("ID", new ColumnSchema("ID", FieldType.Integer, Minimum: 1));
        foreach (string name in columns.Keys)
            if (!table.Columns.Any(column => column.Name == name)) throw new InvalidDataException($"{table.Name}: 없는 컬럼 규칙 {name}");
        foreach (TableColumn column in table.Columns)
        {
            NamePolicy.Require(column.Name);
            if (!columns.ContainsKey(column.Name)) throw new InvalidDataException($"{table.Name}.{column.Name}: B1에 @column 타입 규칙이 필요합니다.");
        }
        NamePolicy.Require(table.Name);
        rules.Insert(0, new TableRule(new RuleValues("Kind=Unique;Columns=ID;Code=DUPLICATE_ID")));
        if (group is not null && !columns.ContainsKey(group)) throw new InvalidDataException($"{table.Name}: 그룹 컬럼 없음");
        foreach (string name in new[] { baseName, inherit, aggregate, property }.OfType<string>()) NamePolicy.Require(name);
        return new(new TableSchema(table.Name, table.Format, table.Columns.Select(column => columns[column.Name]).ToArray(), rules, group), baseName, inherit, aggregate, property);
    }
}

public sealed class RuleValues
{
    private readonly Dictionary<string, string> _values;
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    public RuleValues(string text) => _values = text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries)).ToDictionary(parts => parts[0], parts => parts.Length == 2 ? parts[1] : throw new InvalidDataException("규칙은 이름=값 형식입니다."), StringComparer.Ordinal);
    public string? Get(string key) { _used.Add(key); return _values.GetValueOrDefault(key); }
    public string Required(string key) => Get(key) is { Length: > 0 } value ? value : throw new InvalidDataException($"필수 규칙 누락: {key}");
    public bool Flag(string key) => Get(key) switch { null or "N" => false, "Y" => true, _ => throw new InvalidDataException($"{key}: Y/N만 허용합니다.") };
    public int? Number(string key) => Get(key) is string value ? (int?)CellValueConverter.Parse(value, new ColumnSchema(key, FieldType.Integer)) : null;
    public void RequireConsumed() { foreach (string key in _values.Keys.Except(_used)) throw new InvalidDataException($"알 수 없는 규칙 항목: {key}"); }
}
