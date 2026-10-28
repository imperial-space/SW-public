using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var root = Path.GetFullPath(args[0]);
var directories = new[]
{
    "Content.Server/Imperial/Medieval/Boss",
    "Content.Shared/Imperial/Medieval/Boss",
    "Content.Client/Imperial/Medieval/Boss",
    "Content.Shared/Imperial/Medieval/Weapons",
    "Content.Client/Imperial/Medieval/Weapons",
    "Content.Shared/Imperial/Medieval/Items",
    "Content.Client/Imperial/Medieval/Items"
};
var errors = new List<string>();
var components = 0;
var files = 0;
var originalNamespaces = new Dictionary<string, string>
{
    ["Content.Shared/Imperial/Medieval/Weapons/Melee/MeleeDamageDealtEvent.cs"] = "Content.Shared.Weapons.Melee",
    ["Content.Shared/Imperial/Medieval/Boss/Components/CursedMarkComponent.cs"] = "Content.Server.Imperial.Medieval.Boss",
    ["Content.Shared/Imperial/Medieval/Boss/Components/FightingBossComponent.cs"] = "Content.Server.Imperial.Medieval.Boss",
    ["Content.Server/Imperial/Medieval/Boss/StartTestBossfightCommand.cs"] = "Content.Server.Administration.Commands"
};
foreach (var directory in directories)
foreach (var path in Directory.GetFiles(Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories))
{
    files++;
    var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.Preview)).GetCompilationUnitRoot();
    if (syntax.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        errors.Add("Parse error: " + path);
    var ns = syntax.Members.OfType<FileScopedNamespaceDeclarationSyntax>().SingleOrDefault();
    var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
    var expectedNamespace = originalNamespaces.GetValueOrDefault(relative, string.Join('.', directory.Split('/')));
    if (ns == null || ns.Name.ToString() != expectedNamespace && !ns.Name.ToString().StartsWith(expectedNamespace + "."))
        errors.Add("Unexpected namespace: " + path);
    var declarations = ns?.Members.OfType<ClassDeclarationSyntax>()
        .Where(declaration => declaration.BaseList?.Types.Any(type => type.Type.ToString() == "Component") == true)
        .ToArray() ?? [];
    if (declarations.Length == 0 && Path.GetFileName(Path.GetDirectoryName(path)) != "Components")
        continue;
    if ((ns?.Members.Count != 1 && relative != "Content.Server/Imperial/Medieval/Boss/Components/SpikedGridComponent.cs") || declarations.Length != 1)
        errors.Add("Component file must have one top-level component: " + path);
    foreach (var declaration in declarations)
    {
        components++;
        var name = declaration.Identifier.ValueText;
        if (name != Path.GetFileNameWithoutExtension(path) || !name.EndsWith("Component") ||
            declaration.BaseList?.Types.Any(type => type.Type.ToString() == "Component") != true)
            errors.Add("Unexpected component name or base: " + path);
    }
}
var report = new { files, components, errors };
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);
if (args.Length > 1)
    File.WriteAllText(args[1], json + "\n");
Environment.ExitCode = errors.Count == 0 ? 0 : 1;
