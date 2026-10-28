# Anonymous learner accounts

The first account milestone implements a **development-only simulator**, separate
from the existing shared-token quiz demo. In the enabled scene, open the quiz
menu and choose **Accounts (simulation)**. It is available even when the quiz API
is unconfigured. Editor and development players show this entry; release players
do not create the simulator UI.

New learner issues a six-digit code and requires acknowledgement of the recovery
notice. Enter code restores the same profile within the current process. Codes
keep leading zeros. Continue/Explore anatomy closes the view without signing out.
Sign out and Switch learner clear the device session. Account creation never
starts a quiz or calls an AI service.

Accounts, codes, sessions, and creation-retry results are memory-only fixtures.
They reset when the process exits (or the browser reloads). They do not synchronize
across real devices and are not production authentication. Simulator credentials
are never passed to the quiz HTTP client. Live account storage is not implemented.

The account view uses the station's existing Move bar, themes and text sizes.
In WebGL, typing digits/Enter/Backspace is reserved for the account view and mouse
capture is released. Hold the reveal button to see entered digits. No account
code or credential is registered with panel narration.

## Backend handoff

`ILearnerAccountService` exposes asynchronous Create, Login, Current and Logout.
`LearnerAccountState` exposes a login snapshot and Changed notification, and
rejects cancelled/expired responses using an operation generation. Future services
must use the login credential and derive identity server-side.

| Proposed HTTP route | Input | Response mapped to client types |
|---|---|---|
| POST /v1/auth/profiles | request_id (UUID, retained on retry) | learner_id, profile_label, access_code, access_token, expires_in |
| POST /v1/auth/login | access_code (six ASCII digits) | learner_id, profile_label, access_token, expires_in |
| GET /v1/auth/me | bearer credential | learner_id, profile_label, expires_in |
| POST /v1/auth/logout | bearer credential | idempotent success for this device session |

Map `expires_in` to the login snapshot's expiry; tokens remain opaque to Unity.
Use JSON over HTTPS, no-store responses, generic 401 rejection, 400 validation,
429 with retry_after_seconds, and 503 unavailability. The client times out after
20 seconds and discards cancelled/stale results. Local logout clears access even
when remote revocation fails. The full live-service requirements and acceptance
criteria are in [PLAN-006](../Plans%20and%20Features/2026-10-28_PLAN-006_Anonymous-User-Accounts.md).

The simulator uses one-hour sessions, a 24-hour creation replay window, and
five failed guesses across codes followed by a 60-second cooldown. It protects
code and token lookup with an ephemeral keyed digest; creation replay necessarily
retains the code in process memory. These are fixture defaults, not a deployed
security service or a specified production database schema.

## Verification

Run `./Tools/Test-LearnerAccounts.ps1` in a fresh PowerShell process. It compiles
the actual plain C# service/state source and checks account isolation, leading
zeros, collision handling, lost creation responses, replay, throttling, device
sessions, revocation, expiry, unavailability, and stale-result rejection.

For manual faults, select the runtime `LearnerAccountPanel` component and use its
Simulation context-menu commands to toggle unavailability, lose the next creation
response, or hang the next request. A hung operation should time out after 20
seconds; Explore anatomy should cancel immediately. Retry a lost creation response
and verify that the account/code is issued only once.

Play Mode UI interaction, physical Quest input, and rebuilt browser acceptance
must be recorded separately from service tests and C# compilation.
