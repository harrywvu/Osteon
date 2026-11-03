using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.WebGL;
using UnityEngine;

/// <summary>Release-size WebGL recipe; also accepts a local request from an already open Editor.</summary>
[InitializeOnLoad]
public static class WebDesktopBuild
{
    public const string RequestPath = "Logs/web-desktop-build.request";
    public const string OutputPath = "Builds/Web Desktop Optimized";
    private static bool building;

    static WebDesktopBuild() { EditorApplication.update += CheckRequest; }
    private static void CheckRequest()
    {
        if (building || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        BuildOptimized();
    }

    [MenuItem("Tools/Osteon/Build Optimized WebGL")]
    public static void BuildOptimized()
    {
        if (building) return;
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new InvalidOperationException("Switch to the Web build target before running this command.");
        building = true;
        Directory.CreateDirectory("Logs");
        var target = NamedBuildTarget.WebGL;
        var profile = BuildProfile.GetActiveBuildProfile();
        string profileSnapshot = profile != null ? EditorJsonUtility.ToJson(profile) : null;
        bool development = EditorUserBuildSettings.development;
        bool debugging = EditorUserBuildSettings.allowDebugging;
        bool profiler = EditorUserBuildSettings.connectProfiler;
        var optimization = UserBuildSettings.codeOptimization;
        var compression = PlayerSettings.WebGL.compressionFormat;
        var symbols = PlayerSettings.WebGL.debugSymbolMode;
        var exceptions = PlayerSettings.WebGL.exceptionSupport;
        var initial = PlayerSettings.WebGL.initialMemorySize;
        var maximum = PlayerSettings.WebGL.maximumMemorySize;
        var generation = PlayerSettings.GetIl2CppCodeGeneration(target);
        var stripping = PlayerSettings.GetManagedStrippingLevel(target);
        const string privateConfig = "Assets/Resources/QuizDemoConfig.json";
        string heldConfig = "Logs/quiz-config-during-web-build-" + Guid.NewGuid().ToString("N");
        bool heldJson = false, heldMeta = false;
        try
        {
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            UserBuildSettings.codeOptimization = WasmCodeOptimization.DiskSizeLTO;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Low);
            // Resources are always packaged. Hold the private demo credential outside Assets,
            // preserving its bytes and meta GUID; the live account client has no bundled token.
            if (File.Exists(privateConfig)) { File.Move(privateConfig, heldConfig); heldJson = true; }
            if (File.Exists(privateConfig + ".meta")) { File.Move(privateConfig + ".meta", heldConfig + ".meta"); heldMeta = true; }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Starting optimized WebGL build (non-development, size LTO, no embedded debug symbols).");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { AnatomySceneSetup.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            File.WriteAllText("Logs/WebDesktop-build.txt", $"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\n" +
                $"Warnings: {report.summary.totalWarnings}\nSize: {report.summary.totalSize}\nTime: {report.summary.totalTime}\n" +
                $"Options: {report.summary.options}\nOutput: {OutputPath}\nAccount endpoint: {HttpLearnerAccounts.DefaultBaseUrl}\n");
            if (report.summary.result != BuildResult.Succeeded || (report.summary.options & BuildOptions.Development) != 0)
                throw new InvalidOperationException("Optimized WebGL build failed; see Logs/WebDesktop-build.txt and the Unity Console.");
            Debug.Log("Optimized WebGL build succeeded: " + OutputPath);
        }
        catch (Exception error)
        {
            File.WriteAllText("Logs/WebDesktop-build-failed.txt", error.ToString());
            Debug.LogException(error);
        }
        finally
        {
            if (heldJson) File.Move(heldConfig, privateConfig);
            if (heldMeta) File.Move(heldConfig + ".meta", privateConfig + ".meta");
            EditorUserBuildSettings.development = development;
            EditorUserBuildSettings.allowDebugging = debugging;
            EditorUserBuildSettings.connectProfiler = profiler;
            UserBuildSettings.codeOptimization = optimization;
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.debugSymbolMode = symbols;
            PlayerSettings.WebGL.exceptionSupport = exceptions;
            PlayerSettings.WebGL.initialMemorySize = initial;
            PlayerSettings.WebGL.maximumMemorySize = maximum;
            PlayerSettings.SetIl2CppCodeGeneration(target, generation);
            PlayerSettings.SetManagedStrippingLevel(target, stripping);
            if (profile != null)
            {
                EditorJsonUtility.FromJsonOverwrite(profileSnapshot, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            building = false;
        }
    }
}
