using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MiaCourt.Editor
{
    public static class MiaProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/MiaCourt.unity";
        const string PipelinePath = "Assets/Settings/MiaCourtURP.asset";
        const string RendererPath = "Assets/Settings/MiaCourtRenderer.asset";
        const string AssetsPath = "Assets/Resources/MiaCourtAssets.asset";

        /// <summary>True when the court assets already resolve every model and texture in the roster.</summary>
        public static bool RosterIsWired()
        {
            var assets = AssetDatabase.LoadAssetAtPath<MiaCourtAssets>(AssetsPath);
            if (assets == null) return false;
            if (assets.characterModels == null || assets.characterModels.Length != MiaCourtAssets.CharacterCount) return false;
            if (assets.characterTextures == null || assets.characterTextures.Length != MiaCourtAssets.CharacterCount) return false;
            for (int i = 0; i < MiaCourtAssets.CharacterCount; i++)
                if (assets.characterModels[i] == null || assets.characterTextures[i] == null) return false;
            return true;
        }

        [MenuItem("Mia Court/Configure URP")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/Settings");
            Directory.CreateDirectory("Assets/Resources");
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            MiaCourtSetup.ConfigureImporters();
            MiaCourtSetup.ConfigurePlayer();
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.renderingMode = RenderingMode.Forward;
                // URP's own PostProcessData.GetDefaultPostProcessData is internal, and ResourceReloader
                // skips the field because it carries no [Reload] attribute. Load the package asset the
                // same way URP does internally.
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(renderer, RendererPath);
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1;
            pipeline.supportsHDR = true;
            pipeline.shadowDistance = 80;
            pipeline.shadowCascadeCount = 4;
            pipeline.mainLightShadowmapResolution = 2048;
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int originalQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(originalQuality, false);
            EditorUtility.SetDirty(pipeline);

            var assets = AssetDatabase.LoadAssetAtPath<MiaCourtAssets>(AssetsPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<MiaCourtAssets>();
                AssetDatabase.CreateAsset(assets, AssetsPath);
            }
            assets.characterModels = new GameObject[MiaCourtAssets.CharacterCount];
            assets.characterTextures = new Texture2D[MiaCourtAssets.CharacterCount];
            for (int i = 0; i < MiaCourtAssets.CharacterCount; i++)
            {
                string id = MiaCourtAssets.CharacterIds[i];
                string path = "Assets/Art/Characters/" + id + "/" + id;
                assets.characterModels[i] = Require<GameObject>(path + ".fbx");
                assets.characterTextures[i] = Require<Texture2D>(path + "_BaseColor.png");
            }
            assets.miaByBy = assets.characterModels[0];
            assets.miaBuBu = assets.characterModels[1];
            assets.guguGaga = assets.characterModels[2];
            assets.byByTexture = assets.characterTextures[0];
            assets.buBuTexture = assets.characterTextures[1];
            assets.guguGagaTexture = assets.characterTextures[2];
            assets.propModels = new GameObject[MiaCourtAssets.PropIds.Length];
            assets.propTextures = new Texture2D[MiaCourtAssets.PropIds.Length];
            assets.propNormals = new Texture2D[MiaCourtAssets.PropIds.Length];
            for (int i = 0; i < MiaCourtAssets.PropIds.Length; i++)
            {
                string id = MiaCourtAssets.PropIds[i];
                string path = "Assets/Art/Environment/Props/" + id + "/" + id;
                // A missing prop is not fatal: CourtBuilder keeps a procedural stand-in for each one.
                assets.propModels[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".obj");
                assets.propTextures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_BaseColor.png");
                assets.propNormals[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_Normal.png");
            }
            assets.leftView = Require<Texture2D>("Assets/Art/Environment/Taipei_Left.png");
            assets.centerView = Require<Texture2D>("Assets/Art/Environment/Taipei_Center.png");
            assets.rightView = Require<Texture2D>("Assets/Art/Environment/Taipei_Right.png");
            assets.courtShader = Require<Shader>("Assets/Art/Environment/CourtSurface.shader");
            assets.backdropShader = Require<Shader>("Assets/Art/Environment/ReferenceBackdrop.shader");
            assets.trailShader = Require<Shader>("Assets/Art/Environment/BallTrail.shader");
            assets.litShader = Shader.Find("Universal Render Pipeline/Lit");
            EditorUtility.SetDirty(assets);
            EnsureBuildMaterials(assets);
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // MiaSmokeTest shares the controller object; it disables itself unless -miaSmokeTest is passed.
                var controller = new GameObject("Mia Basketball Game");
                controller.AddComponent<MiaBasketballGame>().assets = assets;
                controller.AddComponent<MiaSmokeTest>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[MiaURP] Configured URP 17.6.0, all quality levels, materials and playable scene.");
        }

        // Real material assets preserve the runtime-created glass and emission variants in builds.
        static void EnsureBuildMaterials(MiaCourtAssets assets)
        {
            const string path = "Assets/Resources/RenderMaterials";
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            SaveMaterial(path + "/Lit.mat", new Material(assets.litShader));
            var glass = new Material(assets.litShader);
            glass.SetFloat("_Surface", 1);
            glass.SetFloat("_ZWrite", 0);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue = 3000;
            SaveMaterial(path + "/Glass.mat", glass);
            var emission = new Material(assets.litShader);
            emission.EnableKeyword("_EMISSION");
            emission.SetColor("_EmissionColor", Color.white);
            SaveMaterial(path + "/ShotGuide.mat", emission);
        }

        static void SaveMaterial(string path, Material material)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null) AssetDatabase.CreateAsset(material, path);
            else UnityEngine.Object.DestroyImmediate(material);
        }

        static T Require<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required asset not found: " + path);
            return asset;
        }

        /// <summary>The single Windows build entry point: configure, validate, then produce the shipping player.</summary>
        [MenuItem("Mia Court/Build Windows game")]
        public static void BuildWindows()
        {
            Configure();
            ValidateSettings();
            MiaCourtSetup.ValidateRules();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/MiaBasketball.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            string summary = "Result: " + report.summary.result + "\nErrors: " + report.summary.totalErrors +
                "\nWarnings: " + report.summary.totalWarnings + "\nBytes: " + report.summary.totalSize +
                "\nTime: " + report.summary.totalTime + "\n";
            Directory.CreateDirectory("Documentation/Validation");
            File.WriteAllText("Documentation/Validation/build.txt", summary);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed. " + summary);
            Debug.Log("MIA_BUILD_PASS " + summary);
        }

        /// <summary>ARM64 IL2CPP APK signed with the debug keystore, for sideload and GitHub Releases.</summary>
        [MenuItem("Mia Court/Build Android APK")]
        public static void BuildAndroid()
        {
            Configure();
            ValidateSettings();
            MiaCourtSetup.ValidateRules();
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/MiaBasketball.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            string summary = "Result: " + report.summary.result + "\nErrors: " + report.summary.totalErrors +
                "\nWarnings: " + report.summary.totalWarnings + "\nBytes: " + report.summary.totalSize +
                "\nTime: " + report.summary.totalTime + "\n";
            Directory.CreateDirectory("Documentation/Validation");
            File.WriteAllText("Documentation/Validation/android-build.txt", summary);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Android build failed. " + summary);
            Debug.Log("MIA_ANDROID_BUILD_PASS " + summary);
        }

        /// <summary>Gzip WebGL player with decompression fallback, for GitHub Releases and static hosting.</summary>
        [MenuItem("Mia Court/Build WebGL")]
        public static void BuildWebGL()
        {
            Configure();
            ValidateSettings();
            MiaCourtSetup.ValidateRules();
            const string output = "Builds/WebGL";
            if (Directory.Exists(output)) Directory.Delete(output, true);
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            string summary = "Result: " + report.summary.result + "\nErrors: " + report.summary.totalErrors +
                "\nWarnings: " + report.summary.totalWarnings + "\nBytes: " + report.summary.totalSize +
                "\nTime: " + report.summary.totalTime + "\n";
            Directory.CreateDirectory("Documentation/Validation");
            File.WriteAllText("Documentation/Validation/webgl-build.txt", summary);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("WebGL build failed. " + summary);
            Debug.Log("MIA_WEBGL_BUILD_PASS " + summary);
        }

        [MenuItem("Mia Court/Validate URP")]
        public static void ValidateSettings()
        {
            var pipeline = Require<UniversalRenderPipelineAsset>(PipelinePath);
            if (GraphicsSettings.defaultRenderPipeline != pipeline) throw new InvalidOperationException("Default URP not assigned.");
            for (int i = 0; i < QualitySettings.names.Length; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) != pipeline) throw new InvalidOperationException("Quality level missing URP: " + i);
            var assets = Require<MiaCourtAssets>(AssetsPath);
            foreach (Shader shader in new[] { assets.courtShader, assets.backdropShader, assets.trailShader, assets.litShader })
            {
                if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Shader failed: " + shader);
            }
            if (pipeline.scriptableRenderer == null) throw new InvalidOperationException("URP renderer missing.");
            Debug.Log("[MiaURP] PASS: default pipeline, all quality levels, renderer, shader imports.");
        }
    }
}
