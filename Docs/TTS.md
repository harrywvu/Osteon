# Panel narration

The anatomy, practice quiz, and Settings panels each have an outside-edge
**Read / Stop** tab. Narration is on demand. Only one panel speaks at a time,
and changing the main content or closing the panel stops speech. The tab moves
with its panel and follows the selected theme and text size.

On Quest, `OnDeviceSpeech` calls the Android `TextToSpeech` service through
`OfflineSpeech.java`. It chooses an installed English voice that reports no
network requirement and is not marked as awaiting a voice-data download.
Speech is synthesized from the text currently displayed, so quiz questions do
not need to match a pre-generated audio corpus. This path does not use Wit.ai,
Meta Voice SDK, network TTS, or bundled speech clips. Fetching a new quiz
question still needs the existing quiz service.

In Windows Editor Play Mode, the same tabs use the installed Windows speech
voice through a hidden PowerShell process. This previews narration without
Android or Wit.ai. Stop and content changes end that process. The Editor voice
may sound different from the Quest voice.

If initialization, voice selection, or speech synthesis fails, the tab shows
**Audio unavailable**. On other Editor platforms, a speech preview is not yet
implemented. WebGL has no speech implementation in `OnDeviceSpeech`, so its
**Read / Stop** tab reports **Audio unavailable**.

Run `./Tools/Test-SpeechCleanup.ps1` on Windows to check that stopping narration
releases running and completed processes and that repeated cleanup is safe.
The check does not play audio.

## Quest verification

1. Build the enabled controller-migration scene and check that all three tabs
   can be selected by controller ray without grabbing a Move bar or selecting
   another control.
2. Start, stop, and restart each panel. Switch panels while speaking and verify
   that only one speaks. Move each panel and verify its tab stays attached.
3. Change anatomy views, answer a quiz question, and change Settings choices
   while narration is active. Speech should stop when the narrated content
   changes or the panel closes.
4. With Wi-Fi off, read anatomy and Settings content. Fetch a quiz question
   before disconnecting and verify it remains readable. Listen for clear
   pronunciation of anatomical terms.
5. Verify an unavailable or removed offline voice produces **Audio unavailable**
   without a network request.

The device's installed English TTS voice is an assumption, not a verified
property of the target Quest. Unity Play Mode, Android build, and physical
headset verification are still pending.
