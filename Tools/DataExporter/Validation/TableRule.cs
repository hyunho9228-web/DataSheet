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
    private readonly bool _optional;
    public TableRule(RuleValues values)
    {
        _kind = values.Required("Kind");
        _code = values.Get("Code") ?? _kind;
        _columns = _kind switch
        {
            "Unique" => values.Required("Columns").Split('|'),
            "LessOrEqual" => [values.Required("MinimumColumn"), values.Required("MaximumColumn")],
            "ConditionalValue" => [values.Required("WhenColumn"), values.Required("Column")],
            "ConditionalReference" => [values.Required("WhenColumn"), values.Required("Column")],
            "ReferenceValue" or "Identifier" => [values.Required("Column")],
            _ => throw new InvalidDataException($"지원하지 않는 검사: {_kind}")
        };
        if (_kind == "ConditionalValue")
        {
            _other = values.Required("WhenValue");
            _value = values.Get("Value") ?? throw new InvalidDataException("조건 값 검사에는 Value가 필요합니다. 빈칸은 Value=로 지정합니다.");
        }
        if (_kind == "ReferenceValue") { _target = values.Required("Table"); _targetColumn = values.Required("TargetColumn"); _value = values.Required("Value"); }
        if (_kind == "ConditionalReference")
        {
            _other = values.Required("WhenValue");
            _target = values.Required("Table");
            _optional = values.Flag("Optional");
            _targetColumn = values.Get("TargetColumn");
            _value = values.Get("Value");
            if ((_targetColumn is null) != (_value is null))
                throw new InvalidDataException("조건 참조의 TargetColumn과 Value는 함께 지정해야 합니다.");
        }
        values.RequireConsumed();
    }
    public IEnumerable<DataIssue> Validate(TypedTable table, IReadOnlyDictionary<string, TypedTable> tables)
    {
        foreach (string column in _columns)
            if (!table.Schema.Columns.Any(field => field.Name == column)) throw new InvalidDataException($"{table.Schema.Name}: 검사 컬럼 없음 {column}");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        HashSet<int>? references = null;
        if (_kind == "ConditionalReference")
        {
            if (!tables.TryGetValue(_target!, out var target))
                throw new InvalidDataException($"{table.Schema.Name}: 조건 참조 테이블이 없습니다: {_target}");
            if (_targetColumn is not null && !target.Schema.Columns.Any(field => field.Name == _targetColumn))
                throw new InvalidDataException($"{_target}: 조건 참조 대상 컬럼이 없습니다: {_targetColumn}");
            references = target.Rows.Where(row => _targetColumn is null || CellValueConverter.Format(row.Cells[_targetColumn].Value) == _value)
                .Select(row => (int)row.Cells["ID"].Value!).ToHashSet();
        }
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
                "ConditionalReference" => text != _other || (row.Cells[_columns[1]].Value switch
                {
                    null => _optional,
                    int id => id > 0 && references!.Contains(id),
                    _ => false
                }),
                "ReferenceValue" => value is null || value is int number && number == 0 || tables.TryGetValue(_target!, out TypedTable? target)
                    && target.Rows.Any(other => Equals(other.Cells["ID"].Value, value) && CellValueConverter.Format(other.Cells[_targetColumn!].Value) == _value),
                _ => false
            };
            if (valid) continue;
            if (_kind is "ConditionalReference" or "ConditionalValue")
            {
                TypedCell checkedCell = row.Cells[_columns[1]];
                yield return new DataIssue(_code, checkedCell.Source, checkedCell.Schema.Name, ConditionalMessage(text, checkedCell.Value));
            }
            else yield return new DataIssue(_code, cell.Source, cell.Schema.Name, "시트의 지정 검사 규칙을 만족하지 않습니다.");
        }
    }

    private string ConditionalMessage(string when, object? value)
    {
        string condition = $"{_columns[0]} 칸 값이 {when}이면";
        string current = CellValueConverter.Format(value);
        if (_kind == "ConditionalValue")
            return _value == ""
                ? $"{_columns[1]} 칸은 비워야 합니다. {condition} 이 칸에 아무것도 적지 않습니다. 지금 적힌 값: {current}"
                : $"{_columns[1]} 칸 값이 잘못됐습니다. {condition} 이 칸에 {_value}을(를) 적습니다. 지금 적힌 값: {current}";
        string target = _targetColumn is null
            ? $"{_target} 표에 있는 번호"
            : $"{_target} 표에서 {_targetColumn} 칸이 {_value}인 줄의 번호";
        return value is null
            ? $"{_columns[1]} 칸이 비어 있습니다. {condition} {target}를 적어야 합니다."
            : $"{_columns[1]} 칸 값이 잘못됐습니다. {current}은(는) 없는 번호입니다. {condition} {target}를 적어야 합니다.";
    }
}
