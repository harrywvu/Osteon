# Accounts, Device Access, and Release Ownership

> Last reviewed on 2026-11-06. Do not commit passwords, recovery codes,
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

Ordinary anatomy Play Mode does not need a backend login. The optional accounts
and quiz services run in a maintainer's **personal AWS account** in
`ap-southeast-2`. Learner login tokens stay in memory; no account credential is
bundled with the client. Move these services to a school-owned account before any
wider release. See [User accounts](USER_ACCOUNTS.md) and
[Quiz integration](QUIZ_INTEGRATION.md).

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
