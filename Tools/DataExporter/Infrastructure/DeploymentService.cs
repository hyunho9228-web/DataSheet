using System.Text.Json;

namespace DataSheet.Export;

public sealed class DeploymentService
{
    private const string _manifest = ".datasheet-generated.json";
    public void Deploy(IReadOnlyList<OutputFile> files, string client, TemporaryWorkspace workspace)
    {
        client = Path.GetFullPath(client);
        if (!Directory.Exists(Path.Combine(client, "Assets")) || !File.Exists(Path.Combine(client, "ProjectSettings", "ProjectVersion.txt"))) throw new IOException("형제 폴더의 Unity 프로젝트를 찾지 못했습니다.");
        string manifest = Path.Combine(client, _manifest);
        // 이전 배포 목록만 소유 대상으로 취급하여 직접 작성한 파일을 보호합니다.
        var old = ReadManifest(manifest);
        var current = files.Select(file => file.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (current.Count != files.Count || current.Concat(old).Any(path => !OutputCatalog.IsOwnedPath(path))) throw new IOException("배포 경로 또는 중복 파일 오류");
        string Target(string relative)
        {
            string path = Path.GetFullPath(Path.Combine(client, relative));
            if (!path.StartsWith(client + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("배포 경계 오류");
            for (string? part = path; part is not null && part.Length >= client.Length; part = Path.GetDirectoryName(part))
                if ((File.Exists(part) || Directory.Exists(part)) && File.GetAttributes(part).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("배포 경로에 링크가 있습니다.");
            return path;
        }
        // C#은 최초 생성만 수행하며 기존 파일은 배포·삭제·원복 대상에서 제외합니다.
        OutputFile[] pending = files.Where(file => !OutputCatalog.IsCode(file.RelativePath) || !File.Exists(Target(file.RelativePath))).ToArray();
        string[] removed = old.Except(current).Where(path => !OutputCatalog.IsCode(path)).ToArray();
        var retained = current.Union(old.Where(OutputCatalog.IsCode)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string relative in pending.Select(file => file.RelativePath))
            if (File.Exists(Target(relative)) && !old.Contains(relative)) throw new IOException($"생성기 소유 목록에 없는 기존 파일입니다: {relative}");
        string[] affected = pending.Select(file => file.RelativePath).Union(removed).Append(_manifest).ToArray();
        var snapshot = affected.ToDictionary(relative => relative, relative => File.Exists(Target(relative)) ? File.ReadAllBytes(Target(relative)) : null);
        string backup = workspace.Folder("Backup");
        File.WriteAllText(Path.Combine(backup, "recovery.json"), JsonSerializer.Serialize(new { Client = client, Files = snapshot }));
        try
        {
            foreach (OutputFile file in pending)
            {
                string path = Target(file.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.Copy(file.SourcePath, path, !OutputCatalog.IsCode(file.RelativePath));
            }
            foreach (string relative in removed) File.Delete(Target(relative));
            File.WriteAllText(manifest, JsonSerializer.Serialize(new { Files = retained.Order(StringComparer.Ordinal).ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception deployError)
        {
            try
            {
                foreach (var pair in snapshot)
                {
                    string path = Target(pair.Key);
                    if (pair.Value is null) { if (File.Exists(path)) File.Delete(path); }
                    else { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, pair.Value); }
                }
            }
            catch (Exception restoreError)
            {
                workspace.Preserve();
                throw new IOException($"배포·원복 실패. 복구 자료를 보존했습니다: {backup}", new AggregateException(deployError, restoreError));
            }
            throw new IOException("배포에 실패하여 기존 파일로 원복했습니다.", deployError);
        }
    }
    private static HashSet<string> ReadManifest(string path)
    {
        if (!File.Exists(path)) return new(StringComparer.OrdinalIgnoreCase);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = document.RootElement;
        JsonElement files = root.ValueKind == JsonValueKind.Array ? root : root.TryGetProperty("Files", out JsonElement value) ? value : root.GetProperty("files");
        return files.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()! : item.TryGetProperty("Path", out JsonElement property) ? property.GetString()! : item.GetProperty("relativePath").GetString()!)
            .Select(OutputCatalog.NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
