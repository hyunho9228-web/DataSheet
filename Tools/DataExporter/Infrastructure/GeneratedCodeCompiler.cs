using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DataSheet.Export;

public sealed class GeneratedCodeCompiler
{
    public void Compile(string classes, string folder)
    {
        Assembly assembly = typeof(GeneratedCodeCompiler).Assembly;
        var references = new List<MetadataReference>();
        foreach (string name in assembly.GetManifestResourceNames().Where(name => name.StartsWith("CompilerReferences.", StringComparison.Ordinal)))
        {
            using Stream stream = assembly.GetManifestResourceStream(name)!;
            references.Add(MetadataReference.CreateFromStream(stream));
        }
        if (references.Count == 0) throw new InvalidDataException("내장 C# 검사 참조가 없습니다. 실행파일을 다시 빌드해 주세요.");
        var options = new CSharpParseOptions(LanguageVersion.CSharp9);
        SyntaxTree[] sources = Directory.EnumerateFiles(classes, "*.cs").Order(StringComparer.Ordinal)
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, path)).ToArray();
        var compilation = CSharpCompilation.Create("GeneratedCheck", sources, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, generalDiagnosticOption: ReportDiagnostic.Error));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new InvalidDataException("생성 C# 컴파일 실패\n" + string.Join("\n", result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
    }
}
