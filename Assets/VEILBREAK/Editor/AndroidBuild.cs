using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Veilbreak.Editor
{
    // An internal development candidate, not a signed store release. Run tests separately before distribution.
    public static class AndroidBuild
    {
        [MenuItem("VEILBREAK/Build Android Development APK", true)]
        static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("VEILBREAK/Build Android Development APK")]
        public static void BuildDevelopment()
        {
            if (!CanBuild())
                throw new InvalidOperationException("Exit Play Mode before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support, SDK/NDK and OpenJDK for this Unity version through Hub.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Switch to Android in File > Build Profiles first. For batch builds, launch Unity with -buildTarget Android.");
            if (!Application.isBatchMode && !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            VeilArenaGenerator.GenerateBatch();
            Directory.CreateDirectory("Builds/Android");
            // Build an explicit single scene so per-profile scene-list overrides cannot silently omit the encounter.
            bool oldBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.buildAppBundle = false;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/VEILBREAK/Scenes/VeilArena.unity" },
                    locationPathName = "Builds/Android/VEILBREAK-development.apk",
                    targetGroup = BuildTargetGroup.Android,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Android build failed: {report.summary.result}; {report.summary.totalErrors} errors. Inspect the Editor log.");
                Debug.Log($"Android development APK built: {report.summary.outputPath}. This is not device validation or store signing.");
            }
            finally { EditorUserBuildSettings.buildAppBundle = oldBundle; }
        }
    }
}
