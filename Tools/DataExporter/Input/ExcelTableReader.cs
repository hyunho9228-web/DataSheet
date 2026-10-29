using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace DataSheet.Export;

// 값만 저장한 원본을 읽는 어댑터입니다. 원본 파일을 저장하거나 수정하지 않습니다.
public sealed class ExcelTableReader : IWorkbookReader
{
    private static readonly XNamespace _sheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace _relationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public RawWorkbook Read(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        using var archive = ZipFile.OpenRead(fullPath);
        XDocument workbook = ReadXml(archive, "xl/workbook.xml");
        var relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels").Root!.Elements()
            .ToDictionary(item => (string)item.Attribute("Id")!);
        string[] strings = archive.GetEntry("xl/sharedStrings.xml") is null ? [] :
            ReadXml(archive, "xl/sharedStrings.xml").Root!.Elements(_sheetNs + "si").Select(ReadText).ToArray();
        var sheets = new List<RawSheet>();
        foreach (XElement sheet in workbook.Descendants(_sheetNs + "sheet"))
        {
            string name = (string)sheet.Attribute("name")!;
            XElement relation = relationships[(string)sheet.Attribute(_relationshipNs + "id")!];
            if ((string?)relation.Attribute("TargetMode") == "External") throw new InvalidDataException("외부 워크시트는 지원하지 않습니다.");
            string target = (string)relation.Attribute("Target")!;
            string entry = new Uri(new Uri("https://workbook.local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
            var rows = new Dictionary<int, IReadOnlyDictionary<int, RawCell>>();
            foreach (XElement row in ReadXml(archive, entry).Descendants(_sheetNs + "sheetData").Elements(_sheetNs + "row"))
            {
                var cells = new Dictionary<int, RawCell>();
                foreach (XElement cell in row.Elements(_sheetNs + "c"))
                {
                    string address = (string)cell.Attribute("r")!;
                    var source = new SourceLocation(fullPath, name, address);
                    if (cell.Element(_sheetNs + "f") is not null) throw new SheetReadException(source, "수식을 값으로 저장한 엑셀이 필요합니다.");
                    string value = (string?)cell.Element(_sheetNs + "v") ?? "";
                    string text = (string?)cell.Attribute("t") switch
                    {
                        "s" => strings[int.Parse(value, CultureInfo.InvariantCulture)],
                        "inlineStr" => ReadText(cell.Element(_sheetNs + "is")!),
                        null or "n" or "str" or "b" => value,
                        _ => throw new SheetReadException(source, "지원하지 않는 셀 타입입니다.")
                    };
                    int column = 0;
                    foreach (char letter in address.TakeWhile(char.IsAsciiLetterUpper)) column = checked(column * 26 + letter - 'A' + 1);
                    if (column == 0) throw new SheetReadException(source, "잘못된 셀 주소입니다.");
                    cells.Add(column, new RawCell(text, source));
                }
                rows.Add((int)row.Attribute("r")!, cells);
            }
            sheets.Add(new RawSheet(name, rows));
        }
        return new RawWorkbook(fullPath, sheets.AsReadOnly());
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        using Stream stream = (archive.GetEntry(path) ?? throw new InvalidDataException($"엑셀 내부 파일 누락: {path}")).Open();
        return XDocument.Load(stream);
    }
    private static string ReadText(XElement element) => string.Concat(element.Descendants(_sheetNs + "t").Select(item => item.Value));
}
