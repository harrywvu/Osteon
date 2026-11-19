# Panel narration

The anatomy, practice quiz, Settings, and Field Guide panels each have an outside-edge
**Read / Stop** tab. Narration is on demand. Only one panel speaks at a time,
and changing the main content or closing the panel stops speech. The tab moves
with its panel and follows the selected theme and text size.

**Read** first looks for a matching recording in the bundled narration catalog.
The recordings stay in `Assets/Ai Voice`; `Assets/Resources/NarrationClips.asset`
links them to the app's narration text. Matching by text prevents a changed
question or answer order from playing a stale recording. Settings phrases and
quiz feedback can mix individual recordings with device speech. The Field Guide
recordings cover controller instructions; the browser uses different prompts.

The [audio map](AI_VO_AUDIO_MAP.csv) accounts for all 315 supplied files.
The catalog uses 295 primary recordings plus 19 edits that match the current
quiz choice order. Alternate takes remain in the source folder. The current
bank has matching audio for 47 of 50 questions; eight individual bones and one
pectoral view text variant also lack a matching recording.

When a clip is missing or fails to load, Quest calls Android `TextToSpeech` through
`OfflineSpeech.java`. It prefers an installed offline English voice and falls back
to the device's English voice when no offline voice is available.
Speech is synthesized from the text currently displayed. Fetching a new quiz
question still needs the existing quiz service.

In Windows Editor Play Mode, fallback uses the installed Windows speech voice
through a hidden PowerShell process. Stop and content changes end that process.

If initialization, voice selection, or speech synthesis fails, the tab shows
**Audio unavailable**. On other Editor platforms, a speech preview is not yet
implemented. WebGL plays the bundled recordings and shows **Audio unavailable**
for text that needs device speech.

Run `./Tools/Test-SpeechCleanup.ps1` on Windows to check that stopping narration
releases running and completed processes and that repeated cleanup is safe.
In Unity, **Tools → Osteon → Validate Narration Clips** checks the catalog keys
and imported clips. Neither check plays audio.

## Quest verification

1. Build with the [Meta Quest profile](SETUP.md#7-build-and-run-on-quest) and
   check that all four tabs can be selected by controller ray without grabbing
   a Move bar or selecting another control.
2. Start, stop, and restart each panel. Switch panels while speaking and verify
   that only one speaks. Move each panel and verify its tab stays attached.
3. Change anatomy views, answer a quiz question, and change Settings choices
   while narration is active. Speech should stop when the narrated content
   changes or the panel closes.
4. With Wi-Fi off, read anatomy and Settings content. Fetch a quiz question
   before disconnecting and verify it remains readable. Listen for clear
   pronunciation of anatomical terms.
5. If the headset has no usable offline English voice, check the configured
   device English voice with Wi-Fi on and off. Expect **Audio unavailable**
   when neither path can speak the displayed text.

An offline English voice is needed for speech while disconnected. The target
Quest's voice availability and physical audio output still need verification.
