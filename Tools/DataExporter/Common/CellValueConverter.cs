using System.Globalization;
using System.Text.RegularExpressions;

namespace DataSheet.Export;

internal static class CellValueConverter
{
    public static object? Parse(string text, ColumnSchema field)
    {
        if (text.Length == 0) return field.Optional ? null : throw new FormatException("필수 값이 비어 있습니다.");
        return field.Type switch
        {
            FieldType.Integer => Regex.IsMatch(text, @"\A-?[0-9]+\z") && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number)
                ? number : throw new FormatException("Int32 정수 형식·범위 오류입니다. 소수·지수·공백은 허용하지 않습니다."),
            FieldType.Flag => text switch { "Y" => true, "N" => false, _ => throw new FormatException("Y 또는 N이어야 합니다.") },
            FieldType.Text => text,
            _ => throw new InvalidDataException("지원하지 않는 타입입니다.")
        };
    }
    public static string Format(object? value) => value switch
    {
        null => "", bool flag => flag ? "Y" : "N", int number => number.ToString(CultureInfo.InvariantCulture),
        string text => text, _ => throw new InvalidDataException("지원하지 않는 값입니다.")
    };
    public static string CodeType(ColumnSchema field) => field.Type switch
    {
        FieldType.Integer => field.Optional ? "int?" : "int", FieldType.Flag => field.Optional ? "bool?" : "bool", _ => "string"
    };
}
