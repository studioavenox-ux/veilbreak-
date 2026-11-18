// VEILBREAK offline-compile stub: the UnityEditor API surface used by Assets/VEILBREAK/Editor.
//
// The runtime assemblies are compiled against the REAL Unity 6.3 engine source (see
// Documentation/OfflineCompilation.md). UnityEditor cannot be built from Unity's C# reference
// repository at all -- the shipped editor lives in closed native modules -- so editor scripts are
// typechecked against this hand-written surface instead.
//
// Rules for this file:
//   * members are behavioural no-ops; only names, signatures and types matter here
//   * signatures are copied from the Unity 6.3 scripting API reference
//   * never reference this assembly from the game build; it exists only for typechecking
using System;
using System.Collections.Generic;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }

    public enum BuildTarget
    {
        NoTarget = -2,
        StandaloneOSX = 2,
        StandaloneWindows = 5,
        iOS = 9,
        Android = 13,
        StandaloneWindows64 = 19,
        WebGL = 20,
        StandaloneLinux64 = 24,
    }

    public enum BuildTargetGroup
    {
        Unknown = 0,
        Standalone = 1,
        iOS = 4,
        Android = 7,
        WebGL = 13,
    }

    [Flags]
    public enum BuildOptions
    {
        None = 0,
        Development = 1,
        AutoRunPlayer = 4,
        ShowBuiltPlayer = 8,
        BuildAdditionalStreamedScenes = 16,
        StrictMode = 512,
        DetailedBuildReport = 2048,
    }

    public enum MobileTextureSubtarget { Generic = 0, DXT = 1, PVRTC = 2, ETC = 4, ETC2 = 8, ASTC = 16 }

    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1, WinRTDotNET = 2, CoreCLR = 3 }

    public enum AndroidSdkVersions
    {
        AndroidApiLevelAuto = 0,
        AndroidApiLevel22 = 22,
        AndroidApiLevel23 = 23,
        AndroidApiLevel24 = 24,
        AndroidApiLevel25 = 25,
        AndroidApiLevel26 = 26,
        AndroidApiLevel27 = 27,
        AndroidApiLevel28 = 28,
        AndroidApiLevel29 = 29,
        AndroidApiLevel30 = 30,
        AndroidApiLevel31 = 31,
        AndroidApiLevel32 = 32,
        AndroidApiLevel33 = 33,
        AndroidApiLevel34 = 34,
        AndroidApiLevel35 = 35,
    }

    [Flags]
    public enum AndroidArchitecture
    {
        None = 0,
        ARMv7 = 1,
        ARM64 = 2,
        X86 = 4,
        X86_64 = 8,
        // Unity defines All as -1 so that it covers every currently known and future architecture bit.
        All = -1,
    }

    public enum AndroidSplashScreenScale { Center = 0, ScaleToFit = 1, ScaleToFill = 2 }

    public enum UIOrientation { Portrait = 0, PortraitUpsideDown = 1, LandscapeRight = 2, LandscapeLeft = 3, AutoRotation = 4 }

    public enum NewSceneSetup { EmptyScene = 0, DefaultGameObjects = 1 }

    public enum NewSceneMode { Single = 0, Additive = 1 }

    public sealed class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene() { }
        public EditorBuildSettingsScene(string path, bool enabled) { this.path = path; this.enabled = enabled; }
        public string path { get; set; }
        public bool enabled { get; set; }
        public Guid guid { get; set; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; } = Array.Empty<EditorBuildSettingsScene>();
    }

    public static class EditorApplication
    {
        public static bool isPlayingOrWillChangePlaymode => false;
        public static bool isPlaying => false;
        public static bool isCompiling => false;
        public static event Action update;
        public static event Action delayCall;
        internal static void RaiseForStub() { update?.Invoke(); delayCall?.Invoke(); }
    }

    public static class EditorUserBuildSettings
    {
        public static BuildTarget activeBuildTarget => BuildTarget.Android;
        public static BuildTargetGroup activeBuildTargetGroup => BuildTargetGroup.Android;
        public static bool buildAppBundle { get; set; }
        public static bool development { get; set; }
        public static bool exportAsGoogleAndroidProject { get; set; }
        public static AndroidArchitecture activeArchitecture { get; set; }
    }

    public static class BuildPipeline
    {
        public static bool IsBuildTargetSupported(BuildTargetGroup targetGroup, BuildTarget target) => true;
        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions options) =>
            new UnityEditor.Build.Reporting.BuildReport();
    }

    public struct BuildPlayerOptions
    {
        public string[] scenes { get; set; }
        public string locationPathName { get; set; }
        public string assetBundleManifestPath { get; set; }
        public BuildTargetGroup targetGroup { get; set; }
        public BuildTarget target { get; set; }
        public int subtarget { get; set; }
        public BuildOptions options { get; set; }
        public string[] extraScriptingDefines { get; set; }
    }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static string bundleVersion { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static void SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget buildTarget, string identifier) { }
        public static string GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget buildTarget) => string.Empty;
        public static void SetScriptingBackend(UnityEditor.Build.NamedBuildTarget buildTarget, ScriptingImplementation backend) { }

        public static class Android
        {
            public static AndroidSdkVersions minSdkVersion { get; set; }
            public static AndroidSdkVersions targetSdkVersion { get; set; }
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static string applicationIdentifier { get; set; }
            public static string appCategory { get; set; }
            public static bool useCustomKeystore { get; set; }
            public static MobileTextureSubtarget textureSubtarget { get; set; }
            public static int bundleVersionCode { get; set; }
        }
    }

    public static class AssetDatabase
    {
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string assetPath) => Array.Empty<UnityEngine.Object>();
        public static UnityEngine.Object LoadMainAssetAtPath(string assetPath) => null;
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object => null;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static string AssetPathToGUID(string path) => string.Empty;
    }

    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object obj) { targetObject = obj; }
        public UnityEngine.Object targetObject { get; }
        public SerializedProperty FindProperty(string propertyPath) => new SerializedProperty();
        public bool ApplyModifiedProperties() => true;
        public bool ApplyModifiedPropertiesWithoutUndo() => true;
        public void Update() { }
    }

    public class SerializedProperty
    {
        public string name => string.Empty;
        public int arraySize { get; set; }
        public int intValue { get; set; }
        public bool boolValue { get; set; }
        public float floatValue { get; set; }
        public string stringValue { get; set; }
        public UnityEngine.Object objectReferenceValue { get; set; }
        public SerializedProperty GetArrayElementAtIndex(int index) => new SerializedProperty();
        public SerializedProperty FindPropertyRelative(string relativePropertyPath) => new SerializedProperty();
        public bool Next(bool enterChildren) => false;
    }
}

namespace UnityEngine.Rendering.Universal
{
    // Minimal URP surface needed to typecheck Assets/VEILBREAK/Editor. Real URP source is not
    // reachable from this sandbox (packages.unity.com is blocked); these are the documented
    // Unity 6.3 URP 17.x members that VeilArenaGenerator touches, with matching types.
    public class UniversalRendererData : UnityEngine.ScriptableObject
    {
        public UniversalRendererData() { }
    }

    public class UniversalRenderPipelineAsset : UnityEngine.Rendering.RenderPipelineAsset
    {
        public static UniversalRenderPipelineAsset Create(UniversalRendererData rendererData) => null;
        public float renderScale { get; set; }
        public int msaaSampleCount { get; set; }
        public bool supportsHDR { get; set; }
        public float shadowDistance { get; set; }
        public int mainLightShadowmapResolution { get; set; }
        public bool supportsMainLightShadows { get; set; }
        public float shadowCascadeCount { get; set; }
        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline() => null;
    }
}

namespace UnityEditor.Build
{
    public struct NamedBuildTarget : IEquatable<NamedBuildTarget>
    {
        public static NamedBuildTarget Android => default;
        public static NamedBuildTarget Standalone => default;
        public static NamedBuildTarget iOS => default;
        public string TargetName => string.Empty;
        public bool Equals(NamedBuildTarget other) => true;
        public override bool Equals(object obj) => obj is NamedBuildTarget;
        public override int GetHashCode() => 0;
    }
}

namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown = 0, Succeeded = 1, Failed = 2, Cancelled = 3 }

    public struct BuildSummary
    {
        public BuildResult result => BuildResult.Succeeded;
        public string outputPath => string.Empty;
        public ulong totalSize => 0;
        public int totalErrors => 0;
        public int totalWarnings => 0;
        public TimeSpan totalTime => TimeSpan.Zero;
    }

    public sealed class BuildReport
    {
        public BuildSummary summary => default;
        public BuildStep[] steps => Array.Empty<BuildStep>();
    }

    public sealed class BuildStep
    {
        public string name => string.Empty;
        public TimeSpan duration => TimeSpan.Zero;
    }
}

namespace UnityEditor.SceneManagement
{
    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string dstScenePath) => true;
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string dstScenePath, bool saveAsCopy) => true;
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true;
        public static bool SaveOpenScenes() => true;
        public static UnityEngine.SceneManagement.Scene OpenScene(string scenePath) => default;
    }
}
