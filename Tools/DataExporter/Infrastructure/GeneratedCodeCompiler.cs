using System.Diagnostics;
using System.Security;

namespace DataSheet.Export;

public sealed class GeneratedCodeCompiler
{
    public void Compile(string classes, string folder)
    {
        string project = Path.Combine(folder, "GeneratedCheck.csproj");
        File.WriteAllText(Path.Combine(folder, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>9.0</LangVersion><Nullable>disable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Compile Include=\""
            + SecurityElement.Escape(Path.Combine(classes, "*.cs")) + "\" /></ItemGroup></Project>");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = folder };
        foreach (string argument in new[] { "build", project, "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new IOException("C# 컴파일러 실행 실패");
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { process.Kill(true); process.WaitForExit(); throw new IOException("C# 컴파일 시간 초과"); }
        Task.WaitAll(output, error);
        if (process.ExitCode != 0) throw new InvalidDataException("생성 C# 컴파일 실패\n" + output.Result + error.Result);
    }
}
