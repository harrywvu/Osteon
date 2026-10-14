$ErrorActionPreference = 'Stop'
$speechSource = Get-Content (Join-Path $PSScriptRoot '../Assets/Scripts/OnDeviceSpeech.cs') -Raw
$checks = @'
namespace UnityEngine
{
    public static class Debug
    {
        public static void LogWarning(object message) { System.Console.Error.WriteLine(message); }
    }
}

public static class SpeechCleanupCheck
{
    public static void Run()
    {
        var field = typeof(OnDeviceSpeech).GetField("speechProcess",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        foreach (bool waitForCompletion in new[] { false, true })
        {
            var start = new System.Diagnostics.ProcessStartInfo(
                System.IO.Path.Combine(System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.Windows), "System32", "cmd.exe"),
                "/c pause")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };
            using (var process = System.Diagnostics.Process.Start(start))
            {
                using (var observer = System.Diagnostics.Process.GetProcessById(process.Id))
                {
                    if (waitForCompletion)
                    {
                        process.StandardInput.WriteLine();
                        if (!process.WaitForExit(5000))
                            throw new System.Exception("Test process did not finish.");
                    }
                    field.SetValue(null, process);
                    try
                    {
                        OnDeviceSpeech.Stop();
                        if (!observer.WaitForExit(5000))
                            throw new System.Exception("Stopping speech left its process running.");
                        if (field.GetValue(null) != null)
                            throw new System.Exception("Stopping speech retained its process handle.");
                        OnDeviceSpeech.Stop();
                        OnDeviceSpeech.Shutdown();
                    }
                    finally
                    {
                        if (!observer.HasExited) observer.Kill();
                        OnDeviceSpeech.Shutdown();
                    }
                }
            }
        }
    }
}
'@
Add-Type -TypeDefinition ("#define UNITY_EDITOR_WIN`n" + $speechSource + "`n" + $checks)
[SpeechCleanupCheck]::Run()
Write-Output 'Speech cleanup passed: running process, completed process, repeated Stop/Shutdown.'
