// VEILBREAK offline C# typecheck harness (developer tooling, NOT part of the game).
//
// Compiles source against a reference assembly set and reports real compiler diagnostics.
// It exists because no Unity Editor is available in the source sandbox; this does not replace
// Unity compilation or Play Mode tests. See Documentation/OfflineCompilation.md.
//
// Usage:
//   OfflineCompile --sources <rootDir|-> <outName> <defines> <comma,separated,refs> [extraSource.cs,...]
//   OfflineCompile --project <path.csproj> <outName> <comma,separated,refs> <extraDefines>
//
// Environment:
//   OC_REFDIR  directory with the runtime reference assemblies (required)
//   OC_CORELIB optional explicit System.Private.CoreLib.dll to reference first
//   OC_OUT     output directory (default: ./obj/offline)
//   OC_SKIP    comma list of assembly file names in OC_REFDIR to ignore
//   OC_DROP    comma list of assembly file names to ignore inside OC_REFDIR
//   OC_EXCLUDE comma list of path suffixes to skip in --project mode
//   OC_MODULES comma list of Modules/<name>/ directories to skip in --project mode
//   OC_QUIET   when set, do not print each source path
//   OC_PATCHDIR directory of <FileName>.patch files applied to reference source before parsing
//   OC_LANGVER  C# language version (default: Latest)
//   OC_KIND     output kind: library (default) or exe
//   OC_PDB      when set, also emit a portable PDB so stack traces carry line numbers
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class OfflineCompile
{
    static string Env(string name) => Environment.GetEnvironmentVariable(name);

    static List<string> Split(string value) =>
        string.IsNullOrEmpty(value)
            ? new List<string>()
            : value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    // Reference-source patches are tiny, reviewable text edits for code that cannot be parsed by
    // this host compiler (it predates some C# 13 syntax). Format per line:
    //   SKIP                       -> drop the file from the build
    //   REPLACE<TAB>old<TAB>new    -> text replacement (\n in <new> becomes a newline)
    static string ApplyPatch(string path, string text)
    {
        var dir = Env("OC_PATCHDIR");
        if (string.IsNullOrEmpty(dir)) return text;
        var patch = Path.Combine(dir, Path.GetFileName(path) + ".patch");
        if (!File.Exists(patch)) return text;
        var tab = (char)9;
        foreach (var line in File.ReadAllLines(patch))
        {
            if (line.StartsWith("SKIP", StringComparison.Ordinal)) return null;
            if (!line.StartsWith("REPLACE" + tab, StringComparison.Ordinal)) continue;
            var parts = line.Split(new[] { tab }, 3);
            if (parts.Length < 3) continue;
            if (!text.Contains(parts[1])) Console.WriteLine("PATCH MISS " + Path.GetFileName(path));
            text = text.Replace(parts[1], parts[2].Replace("\\n", "\n"));
        }
        return text;
    }

    static List<string> ReferenceFiles(string refDir)
    {
        var files = Directory.GetFiles(refDir, "*.dll").OrderBy(x => x, StringComparer.Ordinal).ToList();
        var skip = Split(Env("OC_SKIP"));
        var drop = Split(Env("OC_DROP"));
        files = files.Where(f => !skip.Contains(Path.GetFileName(f)) && !drop.Contains(Path.GetFileName(f))).ToList();

        var core = Env("OC_CORELIB");
        if (!string.IsNullOrEmpty(core) && File.Exists(core))
        {
            // A real corelib is referenced explicitly and the facades around it are kept.
            files.Insert(0, core);
        }
        return files;
    }

    static IEnumerable<MetadataReference> CreateReferences(IEnumerable<string> files)
    {
        foreach (var file in files.Distinct())
        {
            MetadataReference reference = null;
            try { reference = MetadataReference.CreateFromFile(file); }
            catch (Exception error) { Console.WriteLine("SKIP REF " + Path.GetFileName(file) + ": " + error.Message); }
            if (reference != null) yield return reference;
        }
    }

    static List<string> ProjectSources(string csproj, int[] counters)
    {
        var wanted = new List<string>();
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(csproj));
        var excludes = Split(Env("OC_EXCLUDE"));
        var modules = Split(Env("OC_MODULES"));
        foreach (var element in XDocument.Load(csproj).Descendants().Where(e => e.Name.LocalName == "Compile"))
        {
            var include = element.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include)) continue;
            var full = Path.GetFullPath(Path.Combine(baseDir, include.Replace('\\', '/')));
            if (!File.Exists(full)) { counters[0]++; continue; }
            var normalized = full.Replace('\\', '/');
            if (excludes.Any(e => normalized.EndsWith(e, StringComparison.Ordinal))) { counters[1]++; continue; }
            var moduleIndex = normalized.IndexOf("/Modules/", StringComparison.Ordinal);
            if (moduleIndex >= 0)
            {
                var module = normalized.Substring(moduleIndex + 9).Split('/')[0];
                if (modules.Contains(module)) { counters[1]++; continue; }
            }
            wanted.Add(full);
        }
        return wanted;
    }

    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("OfflineCompile: missing mode"); return 2; }

        var mode = args[0];
        var outDir = Env("OC_OUT") ?? Path.Combine(Directory.GetCurrentDirectory(), "obj", "offline");
        var quiet = Env("OC_QUIET") != null;
        string outName;
        string defines;
        List<string> refs = new List<string>();
        List<string> sources = new List<string>();

        if (mode == "--sources")
        {
            if (args.Length < 5) { Console.Error.WriteLine("--sources <root|-> <outName> <defines> <refs>"); return 2; }
            var root = args[1];
            outName = args[2];
            defines = args[3];
            refs = Split(args[4]);
            if (root != "-")
            {
                if (!Directory.Exists(root)) { Console.Error.WriteLine("missing source root " + root); return 2; }
                sources.AddRange(Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories));
            }
            for (var i = 5; i < args.Length; i++)
                foreach (var extra in Split(args[i])) if (File.Exists(extra)) sources.Add(extra);
        }
        else if (mode == "--project")
        {
            if (args.Length < 5) { Console.Error.WriteLine("--project <csproj> <outName> <refs> <extraDefines>"); return 2; }
            var counters = new int[2];
            sources = ProjectSources(args[1], counters);
            outName = args[2];
            refs = Split(args[3]);
            defines = string.Join(";", XDocument.Load(args[1]).Descendants()
                .Where(e => e.Name.LocalName == "DefineConstants")
                .SelectMany(e => (e.Value ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim())
                .Where(t => t.Length > 0 && !t.StartsWith("$("))
                .Concat(Split(args[4])));
            Console.WriteLine("project: " + sources.Count + " sources, " + counters[0] + " missing, " + counters[1] + " excluded");
        }
        else
        {
            Console.Error.WriteLine("unknown mode " + mode);
            return 2;
        }

        foreach (var extra in Split(Env("OC_EXTRA_SOURCES")))
            if (File.Exists(extra) && !sources.Contains(extra)) sources.Add(extra);

        var definesList = defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
        // Unity 6.x player compilation uses C# 9. The numeric cast keeps this file buildable with
        // the bootstrap Roslyn (which has no CSharp9 member) while runtime Roslyn maps 900 -> C# 9.
        var langVersion = LanguageVersion.Latest;
        var langOverride = Env("OC_LANGVER");
        if (!string.IsNullOrEmpty(langOverride))
        {
            // Accept "10", "10.0" and "CSharp10" alike.
            var digits = new string(langOverride.Where(char.IsDigit).ToArray());
            if (langOverride.Equals("Latest", StringComparison.OrdinalIgnoreCase))
                langVersion = LanguageVersion.Latest;
            else if (digits.Length > 0 && int.TryParse(digits.Substring(0, Math.Min(2, digits.Length)), out var number))
                langVersion = (LanguageVersion)(number * 100);
            else
            {
                Console.Error.WriteLine("bad OC_LANGVER " + langOverride);
                return 2;
            }
        }
        var parseOptions = new CSharpParseOptions(langVersion, preprocessorSymbols: definesList);
        var trees = new List<SyntaxTree>();
        foreach (var path in sources)
        {
            string text;
            using (var reader = new StreamReader(path, true)) text = reader.ReadToEnd();
            text = ApplyPatch(path, text);
            if (text == null) { Console.WriteLine("patch skip " + Path.GetFileName(path)); continue; }
            if (!quiet) Console.WriteLine("source " + Path.GetFileName(path));
            // Encoding must be supplied or portable PDB emission fails with CS8055.
            trees.Add(CSharpSyntaxTree.ParseText(
                Microsoft.CodeAnalysis.Text.SourceText.From(text, System.Text.Encoding.UTF8),
                parseOptions, path));
        }

        var referenceList = ReferenceFiles(Env("OC_REFDIR") ?? throw new InvalidOperationException("OC_REFDIR is required"));
        referenceList.AddRange(refs);

        var outputKind = (Env("OC_KIND") ?? "library").Equals("exe", StringComparison.OrdinalIgnoreCase)
            ? OutputKind.ConsoleApplication
            : OutputKind.DynamicallyLinkedLibrary;
        var options = new CSharpCompilationOptions(outputKind, allowUnsafe: true, optimizationLevel: OptimizationLevel.Debug)
            .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                { "CS0436", ReportDiagnostic.Suppress },
                { "CS1701", ReportDiagnostic.Suppress },
                { "CS1702", ReportDiagnostic.Suppress }
            });

        var compilation = CSharpCompilation.Create(outName, trees, CreateReferences(referenceList), options);
        var diagnostics = compilation.GetDiagnostics().ToList();
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();

        Directory.CreateDirectory(outDir);
        var errorFile = Path.Combine(outDir, outName + ".errors.txt");
        using (var writer = new StreamWriter(errorFile))
            foreach (var diagnostic in errors)
            {
                var position = diagnostic.Location.GetLineSpan();
                writer.WriteLine(diagnostic.Id + "\t" + position.Path + ":" + (position.StartLinePosition.Line + 1) + "\t" + diagnostic.GetMessage());
            }

        Console.WriteLine(outName + ": " + sources.Count + " sources, " + errors.Count + " errors, " + warnings.Count + " warnings (refs " + referenceList.Count + ")");
        Console.WriteLine(errors.Count == 0
            ? "RESULT " + outName + " COMPILES"
            : "RESULT " + outName + " FAILED - diagnostics: " + errorFile);

        if (errors.Count > 0) return 1;

        var dll = Path.Combine(outDir, outName + ".dll");
        if (Env("OC_PDB") != null)
        {
            using (var stream = File.Create(dll))
            using (var pdb = File.Create(Path.Combine(outDir, outName + ".pdb")))
            {
                var emit = compilation.Emit(stream, pdb, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(
                    debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb));
                if (!emit.Success)
                {
                    Console.WriteLine("RESULT " + outName + " EMIT FAILED");
                    foreach (var diagnostic in emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(10))
                        Console.WriteLine("  " + diagnostic);
                    return 1;
                }
            }
        }
        else
        using (var stream = File.Create(dll))
        {
            var emit = compilation.Emit(stream);
            if (!emit.Success)
            {
                Console.WriteLine("RESULT " + outName + " EMIT FAILED");
                return 1;
            }
        }
        return 0;
    }
}
