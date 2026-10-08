using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DungeonGuardians.Editor
{
    // One-click builds for the two versions of the game, from the "Dungeon Guardians" menu or from the command line:
    //   Unity.exe -batchmode -quit -projectPath . -executeMethod DungeonGuardians.Editor.BuildTool.BuildWindows
    //   Unity.exe -batchmode -quit -projectPath . -executeMethod DungeonGuardians.Editor.BuildTool.BuildAndroid
    // Each build first applies its platform's player settings, so the two never mix:
    //   Windows: a borderless full-screen window that can be resized or switched to windowed (Alt+Enter), keyboard.
    //   Android: landscape only, IL2CPP for 64-bit ARM, drawn into the display cutout (the HUD keeps to the safe area).
    // Output goes to Builds/ (ignored by git).
    public static class BuildTool
    {
        private const string ProductName = "Хранители подземелья";
        private const string CompanyName = "Woocommander";
        private const string Version = "0.1.0";
        private const string AndroidPackage = "com.woocommander.dungeonguardians";
        private const string WindowsOutput = "Builds/Windows/DungeonGuardians.exe";
        private const string AndroidApkOutput = "Builds/Android/DungeonGuardians.apk";
        private const string AndroidBundleOutput = "Builds/Android/DungeonGuardians.aab";
        // The app icons, made by tools/make_icons.cs from the guardian statue of the start screen.
        private const string IconFolder = "Assets/Art/Icons/";

        [MenuItem("Dungeon Guardians/Собрать для Windows")]
        public static void BuildWindows()
        {
            ApplyCommonSettings();
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, WindowsOutput);
        }

        // An APK to install straight on a phone for testing.
        [MenuItem("Dungeon Guardians/Собрать для Android (APK)")]
        public static void BuildAndroid()
        {
            BuildAndroid(false);
        }

        // An Android App Bundle for Google Play; it must be signed with an upload key (see ApplyAndroidSettings).
        [MenuItem("Dungeon Guardians/Собрать для Android (AAB, Google Play)")]
        public static void BuildAndroidBundle()
        {
            BuildAndroid(true);
        }

        private static void BuildAndroid(bool appBundle)
        {
            ApplyCommonSettings();
            ApplyAndroidSettings();
            EditorUserBuildSettings.buildAppBundle = appBundle;
            Build(BuildTarget.Android, BuildTargetGroup.Android, appBundle ? AndroidBundleOutput : AndroidApkOutput);
        }

        private static void ApplyCommonSettings()
        {
            SetupURP.EnsureURPConfigured();
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.bundleVersion = Version;

            // The default icon: Windows takes its sizes from it.
            Texture2D icon = LoadIcon("icon.png");
            if (icon != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            }
        }

        // Puts the app icons into the player settings now, for builds made from Unity's own Build window too.
        [MenuItem("Dungeon Guardians/Применить иконки приложения")]
        public static void ApplyIcons()
        {
            Texture2D icon = LoadIcon("icon.png");
            if (icon != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            }

            ApplyAndroidIcons();
            AssetDatabase.SaveAssets();
        }

        private static Texture2D LoadIcon(string file)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconFolder + file);
            if (texture == null)
            {
                Debug.LogWarning($"App icon {IconFolder + file} is missing; run tools/make_icons.cs.");
            }

            return texture;
        }

        // Android's three kinds of icon: adaptive (a background layer and a foreground one, masked by the launcher),
        // round and legacy. The kinds are found by name, so this compiles without the Android editor extensions.
        private static void ApplyAndroidIcons()
        {
            Texture2D square = LoadIcon("icon.png");
            Texture2D round = LoadIcon("icon_round.png");
            Texture2D background = LoadIcon("icon_adaptive_background.png");
            Texture2D foreground = LoadIcon("icon_adaptive_foreground.png");
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (PlatformIcon icon in icons)
                {
                    switch (kind.ToString())
                    {
                        case "Adaptive":
                            icon.SetTextures(background, foreground);
                            break;
                        case "Round":
                            icon.SetTexture(round);
                            break;
                        default:
                            icon.SetTexture(square);
                            break;
                    }
                }

                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
        }

        private static void ApplyAndroidSettings()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackage);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            // Google Play requires 64-bit; current phones are all ARM64.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Minimum Android 8.0 (API level 26) is the baseline for Unity 6.
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            ApplyAndroidIcons();

            // Landscape only (TZ section 9), turning between the two landscape sides.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // A release key can be given through environment variables; without them the build is signed with the
            // debug key, which is fine for testing on a phone but not for Google Play.
            string keystore = Environment.GetEnvironmentVariable("DG_KEYSTORE");
            if (!string.IsNullOrEmpty(keystore) && File.Exists(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("DG_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("DG_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("DG_KEY_PASS");
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
            }
        }

        private static void Build(BuildTarget target, BuildTargetGroup group, string output)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            }

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                targetGroup = group,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildTool] {target}: {output} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime:mm\\:ss})");
                if (!Application.isBatchMode)
                {
                    EditorUtility.RevealInFinder(output);
                }
            }
            else
            {
                Debug.LogError($"[BuildTool] {target} build {summary.result}: {summary.totalErrors} errors. See the Console.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }
    }
}
