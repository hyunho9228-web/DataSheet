namespace DataSheet.Export;

public sealed record SourceLocation(string File, string Sheet, string Cell);
public sealed record RawCell(string Text, SourceLocation Source);
public sealed record RawSheet(string Name, IReadOnlyDictionary<int, IReadOnlyDictionary<int, RawCell>> Rows);
public sealed record RawWorkbook(string File, IReadOnlyList<RawSheet> Sheets);
public enum OutputFormat { Csv, Json }
public sealed record TableColumn(string Name, int SourceColumn, SourceLocation Source);
public sealed record TableRow(int SourceRow, IReadOnlyList<RawCell> Cells);
public sealed record SheetTable(string Name, OutputFormat Format, IReadOnlyList<TableColumn> Columns, IReadOnlyList<TableRow> Rows);

public sealed class SheetReadException : Exception
{
    public SourceLocation SourceLocation { get; }
    public SheetReadException(SourceLocation source, string message)
        : base($"{source.File} / {source.Sheet} / {source.Cell}: {message}") => SourceLocation = source;
}

public interface IWorkbookReader { RawWorkbook Read(string filePath); }

internal static class RawSheetAccess
{
    public static RawCell Get(string file, RawSheet sheet, int row, int column)
    {
        if (sheet.Rows.TryGetValue(row, out var cells) && cells.TryGetValue(column, out var cell)) return cell;
        string letters = "";
        for (int index = column; index > 0; index = (index - 1) / 26) letters = (char)('A' + (index - 1) % 26) + letters;
        return new RawCell("", new SourceLocation(file, sheet.Name, letters + row));
    }
}
