using UnityEngine;

/// <summary>Android TTS on Quest, with a Windows voice for Editor Play Mode.</summary>
internal static class OnDeviceSpeech
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject engine;
    private static bool unavailable;

    public static void Initialize()
    {
        if (engine != null || unavailable) return;
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                engine = new AndroidJavaObject("com.osteon.tts.OfflineSpeech", activity);
        }
        catch (AndroidJavaException exception)
        {
            Debug.LogWarning("Android TTS initialization failed: " + exception.Message);
            unavailable = true;
        }
    }

    // 0 = initializing, 1 = ready, -1 = no usable local English voice.
    public static int State
    {
        get
        {
            if (unavailable) return -1;
            if (engine == null) return 0;
            try { return engine.Call<int>("getState"); }
            catch (AndroidJavaException exception)
            {
                Debug.LogWarning("Android TTS voice check failed: " + exception.Message);
                unavailable = true;
                return -1;
            }
        }
    }

    public static bool Speak(string text)
    {
        try { return engine != null && engine.Call<bool>("speak", text); }
        catch (AndroidJavaException exception)
        {
            Debug.LogWarning("Android TTS speech failed: " + exception.Message);
            return false;
        }
    }

    // 0 = queued, 1 = speaking, 2 = finished, -1 = error.
    public static int PlaybackState
    {
        get
        {
            try { return engine == null ? -1 : engine.Call<int>("getPlaybackState"); }
            catch (AndroidJavaException exception)
            {
                Debug.LogWarning("Android TTS playback check failed: " + exception.Message);
                return -1;
            }
        }
    }

    public static void Stop()
    {
        if (engine == null) return;
        try { engine.Call("stop"); }
        catch (AndroidJavaException exception)
        { Debug.LogWarning("Android TTS stop failed: " + exception.Message); }
    }

    public static void Shutdown()
    {
        if (engine == null) return;
        try { engine.Call("shutdown"); }
        catch (AndroidJavaException exception)
        { Debug.LogWarning("Android TTS shutdown failed: " + exception.Message); }
        engine.Dispose();
        engine = null;
    }
#elif UNITY_EDITOR_WIN
    private static System.Diagnostics.Process speechProcess;

    public static void Initialize() { }
    public static int State => 1;

    public static bool Speak(string text)
    {
        Stop();
        try
        {
            // Encode the text before passing it to PowerShell. Panel content is never
            // interpreted as a command, including quotes and punctuation in quiz text.
            string speechText = System.Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(text));
            string command = "$ErrorActionPreference='Stop'; " +
                "try { Add-Type -AssemblyName System.Speech; " +
                "$voice=New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                "$voice.Speak([Text.Encoding]::UTF8.GetString(" +
                "[Convert]::FromBase64String('" + speechText + "'))) " +
                "} catch { exit 1 }";
            string encodedCommand = System.Convert.ToBase64String(
                System.Text.Encoding.Unicode.GetBytes(command));
            string executable = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows),
                @"System32\WindowsPowerShell\v1.0\powershell.exe");
            var start = new System.Diagnostics.ProcessStartInfo(executable,
                "-NoProfile -NonInteractive -EncodedCommand " + encodedCommand)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };
            speechProcess = System.Diagnostics.Process.Start(start);
            return speechProcess != null;
        }
        catch (System.Exception exception)
        {
            UnityEngine.Debug.LogWarning("Editor TTS failed: " + exception.Message);
            Stop();
            return false;
        }
    }

    public static int PlaybackState
    {
        get
        {
            if (speechProcess == null) return -1;
            try { return speechProcess.HasExited ?
                (speechProcess.ExitCode == 0 ? 2 : -1) : 1; }
            catch (System.InvalidOperationException) { return -1; }
        }
    }

    public static void Stop()
    {
        if (speechProcess == null) return;
        try { if (!speechProcess.HasExited) speechProcess.Kill(); }
        catch (System.InvalidOperationException) { }
        finally
        {
            speechProcess.Dispose();
            speechProcess = null;
        }
    }

    public static void Shutdown() { Stop(); }
#else
    public static void Initialize() { }
    public static int State => -1;
    public static bool Speak(string text) => false;
    public static int PlaybackState => -1;
    public static void Stop() { }
    public static void Shutdown() { }
#endif
}
