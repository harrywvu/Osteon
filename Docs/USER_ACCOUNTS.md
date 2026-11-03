# Anonymous learner accounts

Local account credentials, when recorded on this workstation, are kept in
`Docs/LOCAL_LEARNER_ACCOUNT.md`. That file is excluded from Git; keep PINs out of
shared documentation and build assets.

In the enabled scene, open the quiz menu and choose **Accounts**. The account view
uses the Sydney HTTPS account service in Editor, Quest, and WebGL players,
including non-development builds. It is available even when the separate quiz API
is unconfigured. The API base URL is public configuration in `HttpLearnerAccounts`;
no login credential is bundled with the client.

The persistent Lambda backend and deployment ZIP were prepared on 2026-11-03 in
[`Backend/accounts-api`](../Backend/accounts-api/README.md). Fourteen local tests
passed using an AWS emulator. The user reported a successful deployed Sydney
Lambda connectivity test on 2026-11-03, verifying the table read and secret keys;
its code hash matches the local package. The HTTP adapter now connects the Unity
account view to that backend. The optimized WebGL player built successfully on
2026-11-03, and the user confirmed that Firefox loads the room. Browser account
interaction and physical Quest input still require acceptance.

New learner issues a six-digit code and requires acknowledgement of the recovery
notice. Enter code restores the persisted identity through the account API. Codes
keep leading zeros. Continue/Explore anatomy closes the view without signing out.
Sign out and Switch learner clear the device session. Account creation never
starts a quiz or calls an AI service.

Live accounts persist in DynamoDB. Client login tokens are memory-only and are
lost on app exit or browser reload; entering the saved code returns to the same
identity. The quiz HTTP client still uses its separate demo contract and is not
authenticated by these account sessions yet.

For isolated tests, the runtime component's context menu includes **Simulation/Use
simulated accounts (development only)**. This explicit option is blocked in
non-development players. Simulated profiles reset on process exit; their tokens
are never sent to the quiz client or the live account transport.

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

On 2026-11-03, live HTTPS checks passed for creation, replay, a second login to
the same learner, authenticated identity, logout, revoked-session rejection, and
independence of another device session. All responses carried the localhost CORS
header. One anonymous verification profile was created; both test sessions were
signed out without printing or saving credentials. Browser preflight returned 204
allowing http://localhost:8000, GET/POST, and authorization/content-type/x-unity-version
after the updated CORS configuration propagated.
These checks exercised the deployed API separately from Unity; Play Mode UI,
physical Quest input, and rebuilt browser acceptance must be recorded separately.
