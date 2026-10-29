namespace DataSheet.Export;

public interface ITableRule
{
    IEnumerable<DataIssue> Validate(TypedTable table, IReadOnlyDictionary<string, TypedTable> tables);
}

public sealed class TableRule : ITableRule
{
    private readonly string _kind, _code;
    private readonly string[] _columns;
    private readonly string? _other, _value, _target, _targetColumn;
    public TableRule(RuleValues values)
    {
        _kind = values.Required("Kind");
        _code = values.Get("Code") ?? _kind;
        _columns = _kind switch
        {
            "Unique" => values.Required("Columns").Split('|'),
            "LessOrEqual" => [values.Required("MinimumColumn"), values.Required("MaximumColumn")],
            "ConditionalValue" => [values.Required("WhenColumn"), values.Required("Column")],
            "ReferenceValue" or "Identifier" => [values.Required("Column")],
            _ => throw new InvalidDataException($"지원하지 않는 검사: {_kind}")
        };
        if (_kind == "ConditionalValue") { _other = values.Required("WhenValue"); _value = values.Required("Value"); }
        if (_kind == "ReferenceValue") { _target = values.Required("Table"); _targetColumn = values.Required("TargetColumn"); _value = values.Required("Value"); }
        values.RequireConsumed();
    }
    public IEnumerable<DataIssue> Validate(TypedTable table, IReadOnlyDictionary<string, TypedTable> tables)
    {
        foreach (string column in _columns)
            if (!table.Schema.Columns.Any(field => field.Name == column)) throw new InvalidDataException($"{table.Schema.Name}: 검사 컬럼 없음 {column}");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (TypedRow row in table.Rows)
        {
            TypedCell cell = row.Cells[_columns[0]];
            object? value = cell.Value;
            string text = CellValueConverter.Format(value);
            bool valid = _kind switch
            {
                "Unique" => seen.Add(System.Text.Json.JsonSerializer.Serialize(_columns.Select(column => row.Cells[column].Value))),
                "Identifier" => value is string key && NamePolicy.IsStem(key) && key != "value__",
                "LessOrEqual" => value is int minimum && row.Cells[_columns[1]].Value is int maximum && minimum <= maximum,
                "ConditionalValue" => text != _other || CellValueConverter.Format(row.Cells[_columns[1]].Value) == _value,
                "ReferenceValue" => value is null || value is int number && number == 0 || tables.TryGetValue(_target!, out TypedTable? target)
                    && target.Rows.Any(other => Equals(other.Cells["ID"].Value, value) && CellValueConverter.Format(other.Cells[_targetColumn!].Value) == _value),
                _ => false
            };
            if (!valid) yield return new DataIssue(_code, cell.Source, cell.Schema.Name, "시트의 지정 검사 규칙을 만족하지 않습니다.");
        }
    }
}
