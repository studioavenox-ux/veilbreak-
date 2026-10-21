using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Veilbreak.Editor
{
    public static class VeilArenaGenerator
    {
        const string ScenePath = "Assets/VEILBREAK/Scenes/VeilArena.unity";
        const string PipelinePath = "Assets/VEILBREAK/Materials/VeilMobilePipeline.asset";
        [MenuItem("VEILBREAK/Generate Veil Arena")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Configure();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("VEILBREAK - runtime entry point").AddComponent<VeilbreakBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("VeilArena.unity saved. Enter Play Mode to construct the procedural arena and native UI. File > Build Profiles > Android to build.");
        }
        // Suitable for: Unity -batchmode -projectPath <path> -executeMethod Veilbreak.Editor.VeilArenaGenerator.GenerateBatch -quit
        public static void GenerateBatch()
        {
            Configure();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("VEILBREAK - runtime entry point").AddComponent<VeilbreakBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
        static void Configure()
        {
            Directory.CreateDirectory("Assets/VEILBREAK/Scenes");
            Directory.CreateDirectory("Assets/VEILBREAK/Materials");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (!pipeline)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/VEILBREAK/Materials/VeilMobileRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "Veil Mobile Pipeline";
                pipeline.renderScale = 1; pipeline.msaaSampleCount = 2;
                pipeline.supportsHDR = false; pipeline.shadowDistance = 30;
                pipeline.mainLightShadowmapResolution = 1024;
                var pipelineSettings = new SerializedObject(pipeline);
                pipelineSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
                pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.vSyncCount = 0;
            PlayerSettings.companyName = "AVENOX Games"; PlayerSettings.productName = "VEILBREAK";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.avenoxgames.veilbreak");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // StandaloneInputModule and desktop fallback intentionally share the built-in legacy input backend.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var backend = settings.FindProperty("activeInputHandler");
            if (backend != null) { backend.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue = "Environment";
            tags.ApplyModifiedPropertiesWithoutUndo();
            // Keep the runtime-created Lit material shader in player builds (no serialized material references exist yet).
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (shaders != null && lit)
            {
                bool exists = false;
                for (int i = 0; i < shaders.arraySize; i++) if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == lit) exists = true;
                if (!exists) { int index = shaders.arraySize++; shaders.GetArrayElementAtIndex(index).objectReferenceValue = lit; graphics.ApplyModifiedPropertiesWithoutUndo(); }
            }
        }
    }
}
