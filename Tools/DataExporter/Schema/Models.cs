namespace DataSheet.Export;

public enum FieldType { Integer, Text, Flag }
public sealed record ColumnSchema(string Name, FieldType Type, bool Optional = false,
    int? Minimum = null, int? Maximum = null, string Unit = "", IReadOnlyList<string>? AllowedValues = null,
    string? ReferenceTable = null, bool ZeroMeansNone = false, bool AllowKorean = false,
    bool SharedCharacterStat = false, string? SharedStatKeysFrom = null);
public sealed record TableSchema(string Name, OutputFormat Format, IReadOnlyList<ColumnSchema> Columns,
    IReadOnlyList<ITableRule> Rules, string? JsonGroupColumn = null);
public sealed record DataIssue(string Code, SourceLocation Source, string Column, string Message)
{
    public override string ToString() => $"[{Code}] {Source.File} / {Source.Sheet} / {Source.Cell} / {Column}: {Message}";
}
public sealed record TypedCell(ColumnSchema Schema, object? Value, SourceLocation Source);
public sealed record TypedRow(int SourceRow, IReadOnlyDictionary<string, TypedCell> Cells);
public sealed record TypedTable(SheetTable Source, TableSchema Schema, IReadOnlyList<TypedRow> Rows);
public sealed record ModelBuildResult(IReadOnlyList<TypedTable> Tables, IReadOnlyList<DataIssue> Issues);
