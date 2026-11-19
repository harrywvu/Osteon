# Quest 1.2 follow-up build

The `Meta Quest` profile builds `Assets/Scenes/Osteon-VR.unity`. The global
scene list and WebGL builds use `Osteon.unity`. See the
[scene and build map](SETUP.md#scene-and-build-map) for the current mapping.

For Quest, select **Meta Quest** in File > Build Profiles and confirm its scene
list shows `Osteon-VR` before Build and Run. The learning profile fields now
show a world-space keyboard on the right of the account panel when selected.
Use Shift for lowercase, Delete to erase, and Done to hide it. The six-digit
account code continues to use the existing on-panel keypad.

The Read tab still prefers bundled audio. For generated quiz explanation text
without a matching recording, Android speech now tries an installed offline
English voice first, then the device English voice. The latter may require
network access or a voice-data download on some headsets.

## Test the installed build

The Unity Editor log records a successful `Builds/Osteon-VR-1.2.apk` build and
installation on a Quest 3S on 2026-11-10. Headset interaction is still open.

- [ ] Launch the APK and follow the [Play Mode smoke test](SETUP.md#5-play-mode-smoke-test)
      with physical controllers. Record targeting, panel movement, and comfort.
- [ ] Open Accounts and create or edit a profile with the controller ray. Check
      name entry, Shift, Space, Delete, Done, program search, and the code keypad.
- [ ] Answer a question and select **Read** on generated feedback. Check the
      device voice, then check **Read / Stop** with a bundled anatomy recording.
      See [narration verification](TTS.md#quest-verification).
- [ ] Record the APK, scene, headset model, and result in the
      [project handoff](RELEASE_HANDOFF.md) or attach the test record there.
