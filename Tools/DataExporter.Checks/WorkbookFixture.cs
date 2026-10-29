using System.IO.Compression;
using System.Xml.Linq;

internal static class WorkbookFixture
{
    public static void Write(string path, string[] sheets)
    {
        XNamespace sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Entry(string name, XElement value) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(value); }
        Entry("xl/workbook.xml", new XElement(sheet + "workbook", new XElement(sheet + "sheets", sheets.Select((name, index) => new XElement(sheet + "sheet", new XAttribute("name", name), new XAttribute("sheetId", index + 1), new XAttribute(relationship + "id", "rId" + index))))));
        XNamespace package = "http://schemas.openxmlformats.org/package/2006/relationships";
        Entry("xl/_rels/workbook.xml.rels", new XElement(package + "Relationships", sheets.Select((_, index) => new XElement(package + "Relationship", new XAttribute("Id", "rId" + index), new XAttribute("Target", "worksheets/sheet" + index + ".xml")))));
        XElement Cell(string address, string value) => new(sheet + "c", new XAttribute("r", address), new XAttribute("t", "inlineStr"), new XElement(sheet + "is", new XElement(sheet + "t", value)));
        foreach (int index in Enumerable.Range(0, sheets.Length))
            Entry("xl/worksheets/sheet" + index + ".xml", new XElement(sheet + "worksheet", new XElement(sheet + "sheetData",
                new XElement(sheet + "row", new XAttribute("r", 1), Cell("B1", "새 탭\n@column Name=Value;Type=Integer;Minimum=0")),
                new XElement(sheet + "row", new XAttribute("r", 2), Cell("B2", "CSV,새 테이블")),
                new XElement(sheet + "row", new XAttribute("r", 6), Cell("A6", "ID"), Cell("B6", "Value")),
                new XElement(sheet + "row", new XAttribute("r", 7), Cell("A7", "1"), Cell("B7", "42")))));
    }
}
