using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DataSheet.Export;

// 생성 enum을 바꾸기 전에 클라이언트의 명시적인 타입·멤버 참조를 확인합니다. 엔진 전체 컴파일은 별도입니다.
public sealed class EnumReferenceValidator
{
    public void Validate(ProjectData data, string client)
    {
        var allowed = data.Tables.Where(table => table.Schema.Name == "EnumDefinition").SelectMany(table => table.Rows)
            .GroupBy(row => (string)row.Cells["EnumName"].Value!).ToDictionary(group => group.Key,
                group => group.Select(row => (string)row.Cells["Key"].Value!).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        if (data.Tables.FirstOrDefault(table => table.Schema.Name == "State") is { } state)
            allowed.Add("StateKey", state.Rows.Select(row => (string)row.Cells["Key"].Value!).ToHashSet(StringComparer.Ordinal));
        string folder = Path.GetFullPath(Path.Combine(client, OutputCatalog.EnumsFolder));
        var known = new HashSet<string>(allowed.Keys, StringComparer.Ordinal);
        if (Directory.Exists(folder)) foreach (var file in Directory.EnumerateFiles(folder, "*.cs")) known.Add(Path.GetFileNameWithoutExtension(file));
        string scripts = Path.Combine(client, "Assets", "3.Script");
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            if (file.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || file == Path.Combine(scripts, "Data", "StateKey.cs")) continue;
            var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            foreach (var name in syntax.DescendantNodes().OfType<IdentifierNameSyntax>())
                if (known.Contains(name.Identifier.ValueText) && !allowed.ContainsKey(name.Identifier.ValueText))
                    throw new InvalidDataException($"{file}: 사용 중인 열거형을 시트에서 삭제할 수 없습니다: {name.Identifier.ValueText}");
            foreach (var member in syntax.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                string? name = member.Expression switch { IdentifierNameSyntax type => type.Identifier.ValueText,
                    MemberAccessExpressionSyntax type => type.Name.Identifier.ValueText, _ => null };
                if (name is not null && allowed.TryGetValue(name, out var keys) && !keys.Contains(member.Name.Identifier.ValueText))
                    throw new InvalidDataException($"{file}: 시트에 없는 enum 항목을 참조합니다: {name}.{member.Name}. 원본과 사용 코드를 함께 수정해 주세요.");
            }
        }
    }
}
