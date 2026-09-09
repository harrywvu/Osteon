# FEATURE-STATUS — Anatomy exploration

**Date:** 2026-09-09

| Feature | Status | Evidence or next step |
|---|---|---|
| G0/G1 selection and vertebral G2/G3 navigation | Implemented | Automated selection and Back callbacks passed |
| Individual inspection of all 26 vertebral-column entries | Implemented | 6,604 assertions in the final automated Play Mode run |
| Single-bone hover and stationary reference column | Implemented | Two-ray hover, material restoration, and reference checks passed |
| Right-stick turning/tilting and independent A/B resets | Implemented | Automated axis, limits, and input-reservation checks passed |
| Comfortable placement and complete controller behavior on Quest | Awaiting headset verification | User reported expected output with substantial polish needed; complete acceptance was not recorded |
| Successful automated APK packaging | Unverified; last attempt failed | ARM64 compilation finished; Gradle loopback and generated Meta manifest errors followed |
| Stationary use, consistent display location, smooth viewpoint turning | Planned | PLAN-001; smooth turning selected by the user |
| Direct controller grabbing; stay where released | Planned | PLAN-001; controller grabbing and release behavior selected by the user |
| Reusable navigation for additional bone groups | Planned | PLAN-002; current navigation names the vertebral view directly |
| Individual-mesh coverage for the remaining skeleton | Awaiting asset inventory | Existing source files may be reusable; record separation, cleanup, and missing-asset needs |
| Bare-hand grabbing, assembly, quizzes, throwing | Deferred | Not part of the planned next implementation |

The 6,604 checks were automated desktop Play Mode assertions, not 6,604 headset
tests. User feedback confirms that the observed output matched expectations;
it does not certify every control, every device, or an automated APK build.

See [PLAN-001](2026-09-09_PLAN-001_Stationary-Viewing-and-Grabbing.md),
[PLAN-002](2026-09-09_PLAN-002_Bone-Group-Expansion.md), and the
[G3 verification record](../Docs/G3_INSPECTION.md).
