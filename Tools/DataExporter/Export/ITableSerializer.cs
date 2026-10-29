namespace DataSheet.Export;

public interface ITableSerializer
{
    string Extension { get; }
    void Write(TypedTable table, string path);
    IReadOnlyList<object?[]> Read(TableSchema schema, string path);
}

public static class TableSerializers
{
    public static ITableSerializer For(OutputFormat format) => format == OutputFormat.Csv ? new CsvExporter() : new JsonExporter();
    public static void Verify(TypedTable table, string path)
    {
        IReadOnlyList<object?[]> rows = For(table.Schema.Format).Read(table.Schema, path);
        if (rows.Count != table.Rows.Count) throw new InvalidDataException($"{table.Schema.Name}: 추출 행 수 불일치");
        var expected = table.Rows.ToDictionary(row => (int)row.Cells["ID"].Value!);
        var seen = new HashSet<int>();
        int idIndex = table.Schema.Columns.ToList().FindIndex(column => column.Name == "ID");
        foreach (object?[] row in rows)
        {
            int id = (int)row[idIndex]!;
            if (!seen.Add(id) || !expected.TryGetValue(id, out TypedRow? source) || !row.SequenceEqual(table.Schema.Columns.Select(column => source.Cells[column.Name].Value)))
                throw new InvalidDataException($"{table.Schema.Name}: 추출 값 불일치 ID={id}");
        }
    }
}
