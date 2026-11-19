# Learner accounts

Open the quiz menu and choose **Accounts**. The view uses
the Sydney HTTPS account service in Editor, Quest, and WebGL builds, including
non-development builds, and works even when the quiz API is unconfigured. The
base URL is public configuration in `HttpLearnerAccounts`; no login credential
is bundled with the client.

## Flow

- **New learner** collects first and last names, an optional middle name, MMSU
  program, and year. It issues a six-digit code and requires acknowledgement of
  the recovery notice. On Quest, selecting a text field opens the in-world
  keyboard. The code uses its own keypad.
- **Enter code** restores the persisted identity through the account API. Codes
  keep leading zeros.
- Existing learners with incomplete data can complete their profile after login.
  Signed-in learners can select **Edit profile** to correct it.
- **Continue / Explore anatomy** closes the view without signing out.
- **Sign out** and **Switch learner** clear the device session.
- Creation never starts a quiz or calls an AI service.

Accounts persist in DynamoDB. Client login tokens are memory-only and are lost on
app exit or browser reload; entering the saved code restores the identity. The
quiz sends the account token to the quiz API, which verifies it through
`/v1/auth/me` on every protected request.

For isolated tests, the runtime component's context menu includes
**Simulation/Use simulated accounts (development only)**. The option is blocked
in non-development players. Simulated profiles reset on process exit and their
tokens are never sent to the quiz client or live transport.

The account view uses the station's Move bar, themes, and text sizes. In WebGL,
typing digits/Enter/Backspace is reserved for the view and mouse capture is
released. Hold the reveal button to see entered digits. Account codes are not
registered with panel narration.

## Backend contract

`ILearnerAccountService` exposes asynchronous account and profile operations.
`LearnerAccountState` exposes a login snapshot and Changed notification and
rejects cancelled or expired responses using an operation generation. Future
services must derive identity server-side from the login credential.

| Route | Input | Response mapped to client types |
|---|---|---|
| `POST /v1/auth/profiles` | `request_id` (UUID, retained on retry), profile fields | `learner_id`, `profile_label`, `access_code`, `access_token`, `expires_in` |
| `POST /v1/auth/login` | `access_code` (six ASCII digits) | `learner_id`, `profile_label`, `access_token`, `expires_in` |
| `GET /v1/auth/me` | bearer credential | `learner_id`, `profile_label`, `expires_in` |
| `POST /v1/auth/logout` | bearer credential | idempotent success for this device session |
| `GET /v1/catalog/programs` | none | program IDs, display names, colleges, and year options |
| `PUT /v1/auth/me/profile` | bearer credential, profile fields | updated fields for the authenticated learner |

Profile fields are `first_name`, `middle_name`, `last_name`, `program_id`, and
`year_level`. See the [account API reference](../Backend/accounts-api/README.md#account-api)
for response and compatibility details.

Map `expires_in` to the snapshot expiry; tokens remain opaque to Unity. Use JSON
over HTTPS, no-store responses, generic 401 rejection, 400 validation, 429 with
`retry_after_seconds`, and 503 for unavailability. The client times out after 20
seconds and discards stale results. Local logout clears access even if remote
revocation fails.

The simulator uses one-hour sessions, a 24-hour creation replay window, and five
failed guesses across codes followed by a 60-second cooldown. These are fixture
defaults, not a production security model.

## Verification

Run `./Tools/Test-LearnerAccounts.ps1` in a fresh PowerShell process. It checks
account isolation, leading zeros, collisions, lost creation responses, replay,
throttling, device sessions, revocation, expiry, unavailability, and stale-result
rejection.

For manual faults, select the runtime `LearnerAccountPanel` component and use its
Simulation context-menu commands to toggle unavailability, lose the next creation
response, or hang the next request. A hung operation should time out after 20
seconds; Continue/Explore anatomy should cancel immediately.

Earlier live HTTPS checks covered creation, replay, second login, authenticated
identity, logout, revoked-session rejection, and independent device sessions.
The 2026-11-10 [deployment record](../Backend/educator-portal/README.md#deployment-record-and-remaining-work)
also records a 401 check for unauthenticated profile writes. The refreshed
profile UI still needs [Quest](VR_1_2_FOLLOWUP.md#test-the-installed-build) and
[WebGL](WEB_DESKTOP.md#browser-acceptance-pass) acceptance.
