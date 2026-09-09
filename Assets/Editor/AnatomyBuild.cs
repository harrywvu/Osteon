using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AnatomyBuild
{
    public static void BuildQuest()
    {
        if (PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP ||
            PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64 ||
            (int)PlayerSettings.Android.minSdkVersion != 32)
            throw new InvalidOperationException("Quest build requires the committed IL2CPP / ARM64 / minimum SDK 32 configuration.");
        Directory.CreateDirectory("Builds");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { AnatomySceneSetup.ScenePath },
            locationPathName = "Builds/Anatomy-G3-development.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development
        });
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/G3-build.txt", $"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\n" +
            $"Warnings: {report.summary.totalWarnings}\nSize: {report.summary.totalSize}\nTime: {report.summary.totalTime}\n");
        Debug.Log($"G3 Android build: {report.summary.result}");
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
