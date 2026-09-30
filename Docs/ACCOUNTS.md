# Accounts, Device Access, and Release Ownership

> Last reviewed on 2026-09-30. Do not commit passwords, recovery codes,
> keystores, or signing secrets to this repository.

## Project organization

- Maintainer / organization name: **MMSU - CCIS**
- Unity product name in `ProjectSettings`: `VRSKULL`
- Unity cloud project name: `VRSKULL (1)`

The committed Unity company name is still `DefaultCompany`, so the project
settings do not yet match the maintainer name.

## Local development

A developer needs:

- a Unity Hub account with access to a Unity 6000.3.2f1 license;
- repository access, including permission to download Git LFS objects; and
- Blender 4.5.x for importing the live `.blend` assets.

Ordinary anatomy Play Mode does not need a backend login. The optional quiz
demo calls an API deployed in a maintainer's **personal AWS account** in
`ap-southeast-2`. Its one-learner bearer token belongs only in the ignored
`Assets/Resources/QuizDemoConfig.json` and the matching Lambda environment
variable, never in a commit. The token is embedded in any APK made with that
file; do not distribute such an APK beyond the controlled demo. See
[`QUIZ_INTEGRATION.md`](QUIZ_INTEGRATION.md).

## Quest device testing

To deploy directly to a headset, a tester needs:

- a Meta account associated with a Meta developer organization;
- Developer Mode enabled for the headset through the Meta Horizon mobile app;
- USB debugging authorization accepted inside the headset; and
- an authorized Windows development machine with ADB access.

## Release information that still needs an owner

- Android application identifier (currently
  `com.DefaultCompany.VRTemplate`)
- Android release keystore, alias, password custody, and backup procedure
- Meta developer organization and app record
- Store signing and release-channel access
- Privacy policy and store-listing ownership, if distribution is planned
- School-owned AWS account, billing/cost-alert owner, deployment permissions,
  quiz identity provider, token rotation, and learner-data retention owner

Record the responsible person or team and the secure storage location—not the
secret itself—when these are established.
