// Runs the project's EditMode tests that do not need a Unity Editor host.
//
// This is a developer tool, not a Unity Test Runner replacement: it reflects over the real
// Assets/VEILBREAK/Tests/Editor sources, instantiates each [Test] class, and invokes the methods
// whose signatures only touch plain data (ArenaLayout and similar). Tests that need a render
// pipeline, an Editor menu or a running frame are skipped and reported as such.
//
// What it proves: the layout invariants actually hold when executed.
// What it does not prove: Unity Test Runner execution, Play Mode, rendering, device behaviour.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Veilbreak.Sim
{
    internal static class EditModeRunner
    {
        public static int Run()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("Veilbreak.Tests", StringComparison.Ordinal))
                .OrderBy(t => t.Name).ToArray();

            int run = 0, failed = 0, skipped = 0;
            foreach (var type in types)
            {
                var tests = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute"))
                    .OrderBy(m => m.Name).ToArray();
                if (tests.Length == 0) continue;

                // Only classes whose constructor is parameterless can be driven here.
                var ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor == null) { skipped += tests.Length; continue; }

                foreach (var method in tests)
                {
                    if (!CanRun(type, method)) { skipped++; continue; }
                    object instance = null;
                    try
                    {
                        instance = ctor.Invoke(null);
                        method.Invoke(instance, null);
                        run++;
                        Console.WriteLine("EDITMODE PASS  " + type.Name + "." + method.Name);
                    }
                    catch (Exception error)
                    {
                        var real = error is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : error;
                        run++; failed++;
                        Console.WriteLine("EDITMODE FAIL  " + type.Name + "." + method.Name + " :: " + real.Message
                            + (Environment.GetEnvironmentVariable("VEILSIM_TRACE") != null ? "\n" + real.StackTrace : ""));
                    }
                    finally { RunTearDown(instance); }
                }
            }
            Console.WriteLine("EDITMODE RESULT executed=" + run + " failed=" + failed + " skipped=" + skipped);
            return failed;
        }

        static bool CanRun(Type type, MethodInfo method)
        {
            // Skip anything that needs the Unity Editor, a frame loop or a render pipeline.
            if (method.GetCustomAttributes().Any(a => a.GetType().Name.StartsWith("Unity", StringComparison.Ordinal)))
                return false;
            if (method.GetParameters().Length != 0) return false;
            if (type.GetCustomAttributes().Any(a => a.GetType().Name == "UnityPlatformAttribute")) return false;
            // A test touching these needs engine services the mock cannot honestly provide.
            var body = method.GetMethodBody();
            if (body == null) return false;
            foreach (var token in body.LocalVariables.Where(v => v.LocalType.IsClass))
                foreach (var name in new[] { "PrimitiveFactory", "PrototypeSceneArt", "ArenaGameManager" })
                    if (token.LocalType.FullName.Contains(name)) return false;
            return true;
        }

        static void RunTearDown(object instance)
        {
            if (instance == null) return;
            var teardown = instance.GetType().GetMethods()
                .FirstOrDefault(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TearDownAttribute") && m.GetParameters().Length == 0);
            try { teardown?.Invoke(instance, null); } catch { /* teardown failures are not test failures here */ }
        }
    }
}
