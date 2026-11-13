using DataSheet.Export;

internal static class EnumDeploymentChecks
{
    public static void Run(ProjectData data, Action<bool, string> assert, Action<Action, string> fails, TemporaryWorkspace workspace)
    {
        var table = data.Tables.Single(table => table.Schema.Name == "EnumDefinition");
        var validator = new EnumDataValidator(); validator.Validate(data);
        var duplicate = table with { Rows = table.Rows.Append(table.Rows[0]).ToArray() };
        fails(() => validator.Validate(data with { Tables = data.Tables.Select(item => item == table ? duplicate : item).ToArray() }), "enum 키·정수값 중복 허용");
        string output = workspace.Folder("EnumOutput"); new EnumGenerator().Generate(data, output);
        assert(Directory.GetFiles(output, "*.cs").Length == table.Rows.Select(row => row.Cells["EnumName"].Value).Distinct().Count(), "enum 그룹별 파일 생성 누락");
        var client = workspace.Folder("EnumClient"); Directory.CreateDirectory(Path.Combine(client, "Assets"));
        Directory.CreateDirectory(Path.Combine(client, "ProjectSettings")); File.WriteAllText(Path.Combine(client, "ProjectSettings", "ProjectVersion.txt"), "검사 전용");
        string file = Directory.GetFiles(output, "*.cs")[0];
        var source = new OutputFile(OutputCatalog.EnumsFolder + "/" + Path.GetFileName(file), file);
        var deploy = new DeploymentService(); deploy.Deploy([source], client, workspace);
        string target = Path.Combine(client, source.RelativePath); File.WriteAllText(target + ".meta", "검사 메타");
        File.WriteAllText(target, "수동 수정은 다음 배포에 제거되어야 합니다.");
        deploy.Deploy([source], client, workspace);
        assert(File.ReadAllText(target) == File.ReadAllText(file) && File.ReadAllText(target + ".meta") == "검사 메타", "enum 갱신 또는 메타 보존 실패");
        deploy.Deploy([], client, workspace); assert(!File.Exists(target), "삭제된 enum 파일이 남았습니다.");
        string scripts = Path.Combine(client, "Assets", "3.Script"); Directory.CreateDirectory(scripts);
        var first = table.Rows[0]; string enumName = (string)first.Cells["EnumName"].Value!;
        File.WriteAllText(Path.Combine(scripts, "Consumer.cs"), $"class Consumer {{ object Read() => {enumName}.MissingEntry; }}");
        fails(() => new EnumReferenceValidator().Validate(data, client), "클라이언트의 삭제된 enum 항목 참조를 허용했습니다.");
    }
}
