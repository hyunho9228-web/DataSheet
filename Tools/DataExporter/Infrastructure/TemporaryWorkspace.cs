namespace DataSheet.Export;

public sealed class TemporaryWorkspace : IDisposable
{
    private readonly string _root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "HumanGhostDataTools", "runs"));
    private bool _preserve;
    public string PathName { get; }
    public TemporaryWorkspace()
    {
        PathName = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(PathName);
    }
    public string Folder(string name) { NamePolicy.Require(name); return Directory.CreateDirectory(Path.Combine(PathName, name)).FullName; }
    public void Preserve() => _preserve = true;
    public void Dispose()
    {
        if (_preserve || !Directory.Exists(PathName)) return;
        if (!Path.GetFullPath(PathName).StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("임시 폴더 경계 오류");
        Directory.Delete(PathName, true);
    }
}
