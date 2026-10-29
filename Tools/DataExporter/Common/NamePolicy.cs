using System.Text.RegularExpressions;

namespace DataSheet.Export;

internal static class NamePolicy
{
    private static readonly HashSet<string> _keywords = new(("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while").Split(' '), StringComparer.Ordinal);
    public static bool IsStem(string name) => !string.IsNullOrEmpty(name) && Regex.IsMatch(name, @"\A[A-Za-z_][A-Za-z0-9_]*\z");
    public static string Require(string name) => IsStem(name) && !_keywords.Contains(name) ? name : throw new InvalidDataException($"C# 이름으로 사용할 수 없습니다: {name}");
    public static string Parameter(string name)
    {
        string value = name.All(character => !char.IsLetter(character) || char.IsUpper(character)) ? name.ToLowerInvariant() : char.ToLowerInvariant(name[0]) + name[1..];
        return _keywords.Contains(value) ? "@" + value : value;
    }
}
