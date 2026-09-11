using System;
using System.IO;
using UnityEditor;
using UnityEngine;

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
            if (File.Exists(MiaProjectSetup.ScenePath) || !File.Exists(ByByModel)) return;
            try { MiaProjectSetup.Configure(); }
            catch (Exception error) { Debug.LogException(error); }
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
                else if (command == "validate") { MiaProjectSetup.ValidateSettings(); ValidateRules(); }
                else if (command == "play") EditorApplication.isPlaying = true;
                else if (command == "stop") EditorApplication.isPlaying = false;
                else if (command == "smoke")
                {
                    SessionState.SetBool("MiaCourt.Smoke", true);
                    EditorApplication.isPlaying = true;
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
                    importer.importAnimation = false;
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
                ConfigureTexture(path + "_BaseColor.png");
            }
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
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode3D;
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

                // CatPlayer.Animate assigns visual.localRotation outright every frame, so anything the
                // importer baked into the model root's rotation is lost the moment the game runs.
                model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                Bounds animated = renderers[0].bounds;
                foreach (Renderer r in renderers) animated.Encapsulate(r.bounds);

                lines.AppendLine(character + ":");
                lines.AppendLine("  renderers      " + renderers.Length);
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
            Vector3 clamped = BasketballRules.ClampToCourt(new Vector3(100, 8, -100));
            check(clamped.x < BasketballRules.HalfLength && clamped.z > -BasketballRules.HalfWidth && clamped.y == 0, "court boundaries");
            Directory.CreateDirectory("Documentation/Validation");
            File.WriteAllText("Documentation/Validation/rules.txt", passed + " basketball rules checks passed.\n");
            Debug.Log("MIA_RULES_PASS " + passed);
        }
    }
}
