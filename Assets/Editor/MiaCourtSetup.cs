using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiaCourt.Editor
{
    /// <summary>
    /// Asset import settings, player settings, basketball rule checks and the editor command bridge.
    /// Scene, URP and build ownership belongs to <see cref="MiaProjectSetup"/>; this class never creates a scene itself.
    /// </summary>
    [InitializeOnLoad]
    public static class MiaCourtSetup
    {
        const string ByByModel = "Assets/Art/Characters/MiaByBy3D/MiaByBy3D.fbx";

        static MiaCourtSetup()
        {
            EditorApplication.delayCall += AutoSetup;
            EditorApplication.update += CheckCommandFile;
        }

        static void AutoSetup()
        {
            // Batch runs drive setup explicitly through -executeMethod; racing them here only hides the real error.
            if (Application.isBatchMode) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists(ByByModel)) return;
            // A character swap changes both the roster length and the sub-asset ids inside each FBX,
            // so a scene on disk is not proof the court assets still point at real models.
            if (File.Exists(MiaProjectSetup.ScenePath) && MiaProjectSetup.RosterIsWired()) return;
            try { MiaProjectSetup.Configure(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        [MenuItem("Mia Court/Play local test")]
        public static void StartLocalTest()
        {
            if (EditorApplication.isPlaying) return;
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MiaProjectSetup.ScenePath);
            EditorApplication.isPlaying = true;
        }

        // Only these fixed, project-local commands are supported; this is not a shell bridge.
        static void CheckCommandFile()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            const string commandPath = "MiaEditor.command";
            if (!File.Exists(commandPath)) return;
            string command = File.ReadAllText(commandPath).Trim();
            File.Delete(commandPath);
            try
            {
                if (command == "setup") MiaProjectSetup.Configure();
                else if (command == "build") MiaProjectSetup.BuildWindows();
                else if (command == "android") MiaProjectSetup.BuildAndroid();
                else if (command == "webgl") MiaProjectSetup.BuildWebGL();
                else if (command == "validate") { MiaProjectSetup.ValidateSettings(); ValidateRules(); }
                else if (command == "play") StartLocalTest();
                else if (command == "stop") EditorApplication.isPlaying = false;
                else if (command == "smoke")
                {
                    SessionState.SetBool("MiaCourt.Smoke", true);
                    StartLocalTest();
                }
                else throw new InvalidOperationException("Unknown MiaEditor command.");
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Documentation/Validation");
                File.WriteAllText("Documentation/Validation/editor-error.txt", error.ToString());
                Debug.LogException(error);
            }
        }

        /// <summary>Reimports the supplied photographs and character meshes with the settings the game expects.</summary>
        public static void ConfigureImporters()
        {
            ConfigureTexture("Assets/Art/Environment/Taipei_Left.png");
            ConfigureTexture("Assets/Art/Environment/Taipei_Center.png");
            ConfigureTexture("Assets/Art/Environment/Taipei_Right.png");
            foreach (string character in MiaCourtAssets.CharacterIds)
            {
                string path = "Assets/Art/Characters/" + character + "/" + character;
                var importer = AssetImporter.GetAtPath(path + ".fbx") as ModelImporter;
                if (importer != null)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    // The supplied models carry a Mixamo skeleton. Building the humanoid avatar here
                    // costs nothing at runtime while no Animator is attached, and it is what any future
                    // retargeted clip needs. Motion stays procedural, so no clip is imported.
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.importAnimation = false;
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
                ConfigureTexture(path + "_BaseColor.png");
            }
            foreach (string prop in MiaCourtAssets.PropIds)
            {
                string path = "Assets/Art/Environment/Props/" + prop + "/" + prop;
                var importer = AssetImporter.GetAtPath(path + ".obj") as ModelImporter;
                if (importer != null)
                {
                    // The props are static meshes; CourtBuilder assigns their material in code.
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importNormals = ModelImporterNormals.Calculate;
                    importer.normalSmoothingAngle = 45;
                    importer.importAnimation = false;
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
                ConfigurePropTexture(path + "_BaseColor.png", false);
                ConfigurePropTexture(path + "_Normal.png", true);
            }
        }

        /// <summary>Prop maps wrap around a mesh, so unlike the backdrop photographs they keep mipmaps.</summary>
        static void ConfigurePropTexture(string path, bool normalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        static void ConfigureTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = !path.Contains("Environment");
            importer.SaveAndReimport();
        }

        /// <summary>Windowed 1600x900 Windows player. Linear colour space is what URP expects.</summary>
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Mia Court";
            PlayerSettings.productName = "喵喵街頭籃球";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.miacourt.basketball");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.miacourt.basketball");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            ConfigureWebGL();
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode3D;
        }

        /// <summary>
        /// Gzip plus decompression fallback so a GitHub Release zip works on any static host
        /// that does not send Content-Encoding. DiskSize (not LTO) keeps the first web ship tractable.
        /// </summary>
        public static void ConfigureWebGL()
        {
            var target = UnityEditor.Build.NamedBuildTarget.WebGL;
            PlayerSettings.SetApplicationIdentifier(target, "com.miacourt.basketball");
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCodeGeneration(target, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Low);
            // CreatePrimitive(Quad/Sphere) adds MeshCollider/SphereCollider by name. Engine stripping
            // drops those classes, CourtBuilder.Start throws, and no players are spawned.
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.stripUnusedMeshComponents = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.wasm2023 = true;
            PlayerSettings.WebGL.initialMemorySize = 512;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            // UnityEditor.WebGL lives in the Web module assembly, which Compile-Game.ps1 does not reference.
            var webSettings = Type.GetType("UnityEditor.WebGL.UserBuildSettings, UnityEditor.WebGL.Extensions");
            var wasmOpt = Type.GetType("UnityEditor.WebGL.WasmCodeOptimization, UnityEditor.WebGL.Extensions");
            if (webSettings != null && wasmOpt != null)
            {
                object diskSize = Enum.Parse(wasmOpt, "DiskSize");
                webSettings.GetProperty("codeOptimization").SetValue(null, diskSize);
            }
        }

        /// <summary>
        /// Reports each character exactly as CatPlayer.Initialize sees it: the renderer bounds it
        /// measures, and where the model actually lands once its scale and recentring are applied.
        /// </summary>
        [MenuItem("Mia Court/Validate characters")]
        public static void ValidateCharacters()
        {
            var lines = new System.Text.StringBuilder();
            foreach (string character in MiaCourtAssets.CharacterIds)
            {
                string path = "Assets/Art/Characters/" + character + "/" + character + ".fbx";
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null) throw new InvalidOperationException("Character model missing: " + path);

                var host = new GameObject("probe");
                var model = UnityEngine.Object.Instantiate(source, host.transform);
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                float scale = 2.35f / Mathf.Max(.1f, bounds.size.y);

                model.transform.localScale *= scale;
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                Bounds placed = renderers[0].bounds;
                foreach (Renderer r in renderers) placed.Encapsulate(r.bounds);

                // CatPlayer.Animate composes its facing with the model root's imported rotation, so
                // this reproduces the rest pose the game actually shows.
                model.transform.localRotation = Quaternion.Euler(0, 180, 0) * source.transform.localRotation;
                Bounds animated = renderers[0].bounds;
                foreach (Renderer r in renderers) animated.Encapsulate(r.bounds);

                // The models carry a Mixamo skeleton and import as Humanoid. Nothing plays a clip
                // yet, so a silently broken avatar would only surface the day one is retargeted.
                Avatar avatar = null;
                foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (sub is Avatar found) avatar = found;

                lines.AppendLine(character + ":");
                lines.AppendLine("  renderers      " + renderers.Length + "  " +
                    (renderers[0] is SkinnedMeshRenderer skinned ? "skinned, " + skinned.bones.Length + " bones" : "static"));
                lines.AppendLine("  avatar         " + (avatar == null ? "none"
                    : (avatar.isHuman ? "humanoid" : "generic") + (avatar.isValid ? ", valid" : ", INVALID")));
                lines.AppendLine("  prefab rot     " + source.transform.localRotation.eulerAngles);
                lines.AppendLine("  prefab scale   " + source.transform.localScale);
                lines.AppendLine("  measured size  " + bounds.size + "  centre " + bounds.center);
                lines.AppendLine("  applied scale  " + scale);
                lines.AppendLine("  placed size    " + placed.size + "  centre " + placed.center);
                lines.AppendLine("  placed feet Y  " + placed.min.y + "   (want 0)");
                lines.AppendLine("  placed height  " + placed.size.y + "   (want 2.35)");
                lines.AppendLine("  after Animate  " + animated.size + "  height " + animated.size.y + "   (want 2.35)");
                UnityEngine.Object.DestroyImmediate(host);
            }
            Directory.CreateDirectory("Documentation/Validation");
            File.WriteAllText("Documentation/Validation/characters.txt", lines.ToString());
            Debug.Log("MIA_CHARACTERS\n" + lines);
        }

        [MenuItem("Mia Court/Validate basketball rules")]
        public static void ValidateRules()
        {
            int passed = 0;
            Action<bool, string> check = (valid, name) => { if (!valid) throw new Exception("Rule validation failed: " + name); passed++; };
            Vector3 hoop = BasketballRules.HoopFor(0);
            check(BasketballRules.CrossedHoop(hoop + Vector3.up, hoop - Vector3.up, hoop), "downward center crossing");
            check(!BasketballRules.CrossedHoop(hoop - Vector3.up, hoop + Vector3.up, hoop), "upward crossing cannot score");
            check(!BasketballRules.CrossedHoop(hoop + Vector3.up + Vector3.forward, hoop - Vector3.up + Vector3.forward, hoop), "outside rim cannot score");
            check(!BasketballRules.CrossedHoop(hoop + Vector3.up, hoop + Vector3.up * .1f, hoop), "ball above rim cannot score");
            check(BasketballRules.ShotValue(new Vector3(8, 0, 0), hoop) == 2, "inside arc is two points");
            check(BasketballRules.ShotValue(Vector3.zero, hoop) == 3, "outside arc is three points");
            check(BasketballRules.ShotValue(new Vector3(-8, 0, 0), BasketballRules.HoopFor(1)) == 2, "left hoop point value");
            Vector3 start = new Vector3(-2, 2.4f, 1.5f);
            Vector3 velocity = BasketballRules.LaunchVelocity(start, hoop, 6f);
            float duration = (hoop.x - start.x) / velocity.x;
            Vector3 end = start + velocity * duration + Vector3.down * (BasketballRules.Gravity * .5f * duration * duration);
            check(Vector3.Distance(end, hoop) < .002f, "ballistic arc hits target");
            check(BasketballRules.ReleaseQuality(.68f) > .999f, "perfect release");
            check(BasketballRules.ReleaseQuality(0) < .01f, "early release penalty");
            check(BasketballRules.ReleaseQuality(1) < .2f, "late release penalty");
            check(BasketballRules.ShotWindow(Vector3.zero, hoop) == BasketballRules.LongShotWindow, "three point attempts aim for three seconds");
            check(BasketballRules.ShotWindow(new Vector3(8, 0, 0), hoop) == BasketballRules.CloseShotWindow, "close attempts aim for one and a half seconds");
            check(BasketballRules.SweepCharge(0) < .001f && BasketballRules.SweepCharge(BasketballRules.ChargeSweepSeconds) > .999f &&
                BasketballRules.SweepCharge(BasketballRules.ChargeSweepSeconds * 2f) < .001f, "shot meter sweeps out and back");
            check(BasketballRules.CloseShotWindow > BasketballRules.ChargeSweepSeconds, "the shortest window still crosses the green zone");
            Vector3 clamped = BasketballRules.ClampToCourt(new Vector3(100, 8, -100));
            check(clamped.x < BasketballRules.HalfLength && clamped.z > -BasketballRules.HalfWidth && clamped.y == 0, "court boundaries");
            Directory.CreateDirectory("Documentation/Validation");
            File.WriteAllText("Documentation/Validation/rules.txt", passed + " basketball rules checks passed.\n");
            Debug.Log("MIA_RULES_PASS " + passed);
        }
    }
}
