using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DataSheet.Export;

public sealed class ClientLoadPlanCompiler
{
    public byte[] Compile(string clientRoot, string definitions, string plan)
    {
        string scripts = Path.Combine(clientRoot, "Assets", "3.Script");
        if (!Directory.Exists(scripts)) throw new DirectoryNotFoundException("자동 로딩 계획 검사에 필요한 형제 클라이언트의 스크립트 폴더가 없습니다.");
        var sources = new List<string> { plan };
        foreach (string folder in new[] { "Common/Csv", "Common/Text", "Common/Json", "Data/Mapping", "Data/Storage" })
            sources.AddRange(Directory.EnumerateFiles(Path.Combine(scripts, folder.Replace('/', Path.DirectorySeparatorChar)), "*.cs"));
        foreach (string name in new[] { "DataBootstrapper", "BootstrapTableLoader", "DataLoadSequence", "Utf8CsvFileSource" })
            sources.Add(Path.Combine(scripts, "Data", "Infrastructure", name + ".cs"));
        // 실제로 보존할 클라이언트 정의를 우선하여 생성 계획과의 호환성도 검사합니다.
        var models = Directory.EnumerateFiles(Path.Combine(scripts, "Data"), "*.cs").ToDictionary(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(definitions, "*.cs"))
        {
            string name = Path.GetFileName(path);
            if (models.TryGetValue(name, out string? existing)) ValidateConstructor(path, existing);
            else models.Add(name, path);
        }
        sources.AddRange(models.Values);
        return new GeneratedCodeCompiler().CompileSources(sources);
    }

    private static void ValidateConstructor(string generated, string existing)
    {
        // 보존되는 정의가 삭제된 열을 계속 요구하거나 신규 열을 누락하지 않도록 배포 전에 확인합니다.
        string[] Parameters(string path) => CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot()
            .DescendantNodes().OfType<ConstructorDeclarationSyntax>()
            .Where(node => node.Modifiers.Any(SyntaxKind.PublicKeyword))
            .SelectMany(node => node.ParameterList.Parameters)
            .Select(parameter => parameter.Identifier.ValueText).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!Parameters(generated).SequenceEqual(Parameters(existing), StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException($"시트와 보존된 정의의 생성자 컬럼이 다릅니다: {existing}. 기존 C#는 덮어쓰지 않습니다. 데이터 담당과 스키마를 맞춰 주세요.");
    }
}
