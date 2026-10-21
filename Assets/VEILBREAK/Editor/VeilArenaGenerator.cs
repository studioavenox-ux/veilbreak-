using System;
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
        const string RendererPath = "Assets/VEILBREAK/Materials/VeilMobileRenderer.asset";
        [MenuItem("VEILBREAK/Generate Veil Arena", true)]
        static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("VEILBREAK/Generate Veil Arena")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GenerateBatch();
            Debug.Log("VeilArena.unity saved. Enter Play Mode to construct the procedural arena and native UI. File > Build Profiles > Android to build.");
        }
        // Suitable for: Unity -batchmode -projectPath <path> -executeMethod Veilbreak.Editor.VeilArenaGenerator.GenerateBatch -quit
        public static void GenerateBatch()
        {
            if (!CanGenerate()) throw new InvalidOperationException("Exit Play Mode before generating Veil Arena.");
            Configure();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("VEILBREAK - runtime entry point").AddComponent<VeilbreakBootstrap>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException($"Could not save {ScenePath}. Check folder permissions; build scenes were not changed.");
            // Preserve unrelated build scenes, replace only our entry, and make the arena the launch scene.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
                { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var entry in EditorBuildSettings.scenes)
                if (entry.path != ScenePath) scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
        static void Configure()
        {
            Directory.CreateDirectory("Assets/VEILBREAK/Scenes");
            Directory.CreateDirectory("Assets/VEILBREAK/Materials");
            // Fail before creating a scene if packages/settings are unavailable or a path belongs to another asset.
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (!lit) throw new InvalidOperationException("URP Lit shader unavailable. Finish importing URP 17.0.3, then generate again.");
            var settings = LoadSettings("ProjectSettings/ProjectSettings.asset");
            var tags = LoadSettings("ProjectSettings/TagManager.asset");
            var graphics = LoadSettings("ProjectSettings/GraphicsSettings.asset");
            var backend = RequireProperty(settings, "activeInputHandler");
            var layers = RequireProperty(tags, "layers");
            if (layers.arraySize <= 8) throw new InvalidOperationException("Environment layer 8 is unavailable.");
            var environment = layers.GetArrayElementAtIndex(8);
            if (!string.IsNullOrEmpty(environment.stringValue) && environment.stringValue != "Environment")
                throw new InvalidOperationException("Layer 8 is already in use. Veil Arena requires Environment on layer 8; no layer was overwritten.");
            var shaders = RequireProperty(graphics, "m_AlwaysIncludedShaders");
            var pipeline = LoadGeneratedAsset<UniversalRenderPipelineAsset>(PipelinePath);
            var renderer = LoadGeneratedAsset<UniversalRendererData>(RendererPath);
            if (!renderer)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            if (!pipeline)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "Veil Mobile Pipeline";
                pipeline.renderScale = 1; pipeline.msaaSampleCount = 2;
                pipeline.supportsHDR = false; pipeline.shadowDistance = 30;
                pipeline.mainLightShadowmapResolution = 1024;
                var pipelineSettings = new SerializedObject(pipeline);
                RequireProperty(pipelineSettings, "m_AdditionalLightShadowsSupported").boolValue = false;
                pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            // Recover a deleted renderer without replacing an existing pipeline or its GUID/tuning.
            var rendererSettings = new SerializedObject(pipeline);
            var renderers = RequireProperty(rendererSettings, "m_RendererDataList");
            if (renderers.arraySize == 0) renderers.arraySize = 1;
            if (!renderers.GetArrayElementAtIndex(0).objectReferenceValue)
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            RequireProperty(rendererSettings, "m_DefaultRendererIndex").intValue = 0;
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int originalQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.vSyncCount = 0;
            }
            QualitySettings.SetQualityLevel(originalQuality, false);
            PlayerSettings.companyName = "AVENOX Games"; PlayerSettings.productName = "VEILBREAK";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.avenoxgames.veilbreak");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // StandaloneInputModule and desktop fallback intentionally share the built-in legacy input backend.
            backend.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo();
            environment.stringValue = "Environment";
            tags.ApplyModifiedPropertiesWithoutUndo();
            // Keep the runtime-created Lit material shader in player builds (no serialized material references exist yet).
            bool exists = false;
            for (int i = 0; i < shaders.arraySize; i++) if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == lit) exists = true;
            if (!exists) { int index = shaders.arraySize++; shaders.GetArrayElementAtIndex(index).objectReferenceValue = lit; graphics.ApplyModifiedPropertiesWithoutUndo(); }
        }
        static SerializedObject LoadSettings(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets.Length == 0 || !assets[0])
                throw new InvalidOperationException($"Unity settings unavailable: {path}. Finish project import and reopen the Editor.");
            return new SerializedObject(assets[0]);
        }
        static SerializedProperty RequireProperty(SerializedObject asset, string name)
        {
            var property = asset.FindProperty(name);
            if (property == null) throw new InvalidOperationException($"Required Unity 6000.0 setting missing: {name}.");
            return property;
        }
        static T LoadGeneratedAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset && !(asset is T)) throw new InvalidOperationException($"Asset at {path} is not a {typeof(T).Name}; it was not overwritten.");
            return asset as T;
        }
    }
}
