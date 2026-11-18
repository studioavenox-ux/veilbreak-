// Syntax-checks the acceptance PowerShell scripts with PowerShell's OWN parser.
//
// System.Management.Automation.Language.Parser is the exact code path PowerShell uses before it
// runs a script, so a file that parses here has no syntax error on the Windows host either. It is
// reached through reflection so this tool still compiles (and explains itself) on machines where
// the PowerShell assemblies are not staged.
//
// Deliberately NOT a compile-time reference: the sandbox stages a Windows-RID PowerShell assembly
// that may not load here, and a missing optional tool must never break the typecheck build.
//
// Usage: PowerShellSyntax <pwshAssemblyDir> <file.ps1|file.psm1> [...]
// Exit:  0 = every file parsed with no errors
//        1 = at least one parse error (details printed)
//        2 = PowerShell assemblies unavailable (SKIPPED, not passed)
//        3 = usage error
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class PowerShellSyntax
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: PowerShellSyntax <pwshAssemblyDir> <file.ps1|file.psm1> [...]");
            return 3;
        }

        string assemblyDirectory = args[0];
        var files = args.Skip(1).ToArray();

        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            try
            {
                // System.Management.Automation pulls in many sibling assemblies; resolve them all from
                // the staged directory so a partial load does not look like a syntax error.
                string simpleName = new AssemblyName(eventArgs.Name).Name + ".dll";
                string candidate = Path.Combine(assemblyDirectory, simpleName);
                return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
            }
            catch { return null; }
        };

        Assembly automation;
        string assemblyPath = Path.Combine(assemblyDirectory, "System.Management.Automation.dll");
        if (!File.Exists(assemblyPath))
        {
            Console.WriteLine("POWERSHELL SYNTAX SKIPPED - System.Management.Automation.dll not staged at " + assemblyDirectory);
            Console.WriteLine("run: python3 Tools/UnityValidation/setup_pssyntax.py");
            return 2;
        }
        try
        {
            automation = Assembly.LoadFrom(assemblyPath);
        }
        catch (Exception error)
        {
            Console.WriteLine("POWERSHELL SYNTAX SKIPPED - could not load the PowerShell parser here: "
                + error.GetType().Name + ": " + error.Message);
            Console.WriteLine("(a Windows-RID PowerShell assembly cannot always load on this host; syntax is still");
            Console.WriteLine(" checked by the other checks, but the real parser must be run on Windows.)");
            return 2;
        }

        Type parserType = automation.GetType("System.Management.Automation.Language.Parser");
        Type parseErrorType = automation.GetType("System.Management.Automation.Language.ParseError");
        Type tokenType = automation.GetType("System.Management.Automation.Language.Token");
        if (parserType == null || parseErrorType == null || tokenType == null)
        {
            Console.WriteLine("POWERSHELL SYNTAX SKIPPED - the loaded assembly has no Language.Parser");
            return 2;
        }

        MethodInfo parseFile = parserType.GetMethod("ParseFile", new[]
        {
            typeof(string), tokenType.MakeArrayType().MakeByRefType(), parseErrorType.MakeArrayType().MakeByRefType()
        });
        if (parseFile == null)
        {
            Console.WriteLine("POWERSHELL SYNTAX SKIPPED - Parser.ParseFile(string, out Token[], out ParseError[]) not found");
            return 2;
        }

        PropertyInfo errorMessage = parseErrorType.GetProperty("Message");
        PropertyInfo errorId = parseErrorType.GetProperty("ErrorId");
        PropertyInfo errorExtent = parseErrorType.GetProperty("Extent");
        Type extentType = automation.GetType("System.Management.Automation.Language.IScriptExtent");
        PropertyInfo startLine = extentType?.GetProperty("StartLineNumber");
        PropertyInfo startColumn = extentType?.GetProperty("StartColumnNumber");

        int failures = 0, checkedFiles = 0, unparsable = 0;
        foreach (string path in files)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("MISSING  " + path);
                failures++;
                continue;
            }
            checkedFiles++;
            object[] arguments = { path, null, null };
            try
            {
                parseFile.Invoke(null, arguments);
            }
            catch (TargetInvocationException error)
            {
                // Parser.ParseFile throws only for unreadable input; a syntax problem is reported in
                // the error array, which is why it is not caught as a "failure to parse" here.
                unparsable++;
                failures++;
                Console.WriteLine("FAILED   " + Path.GetFileName(path) + " :: could not read: " + error.InnerException?.Message);
                continue;
            }

            var errors = (arguments[2] as IEnumerable)?.Cast<object>().ToArray() ?? Array.Empty<object>();
            var tokens = (arguments[1] as IEnumerable)?.Cast<object>().ToArray() ?? Array.Empty<object>();
            if (errors.Length == 0)
            {
                Console.WriteLine("PARSED   " + Path.GetFileName(path) + " (" + tokens.Length + " tokens)");
                continue;
            }

            failures++;
            Console.WriteLine("FAILED   " + Path.GetFileName(path) + " - " + errors.Length + " syntax error(s)");
            foreach (var error in errors)
            {
                string id = errorId?.GetValue(error)?.ToString() ?? "?";
                string message = errorMessage?.GetValue(error)?.ToString() ?? "(no message)";
                object extent = errorExtent?.GetValue(error);
                string position = extent == null
                    ? "?"
                    : startLine?.GetValue(extent) + ":" + startColumn?.GetValue(extent);
                Console.WriteLine("         " + position + " " + id + ": " + message);
            }
        }

        Console.WriteLine();
        var version = automation.GetName().Version;
        Console.WriteLine("POWERSHELL SYNTAX " + (failures == 0 ? "PASSED" : "FAILED")
            + " - " + checkedFiles + " file(s) parsed by System.Management.Automation " + version
            + (unparsable > 0 ? " (" + unparsable + " unreadable)" : ""));
        return failures == 0 ? 0 : 1;
    }
}
