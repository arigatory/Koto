# Publishing Koto packages to nuget.org (maintainers)

Repo: github.com/arigatory/Koto — the canonical source; work from a clone of it.

## Release flow

1. Ensure per-package READMEs are current (each `.csproj` packs its own `README.md` via `PackageReadmeFile` — what's in the file at tag time is what nuget.org shows forever for that version).
2. If the public API changed: write the consumer migration guide `docs/migrations/<from>-to-<to>.md` (English, before/after code for every change), list the changes in `PLAN.md` (release history, Russian), point `PackageReleaseNotes` in `src/Directory.Build.props` and an «Upgrading from …» section in each affected package README at the guide, and update the agent skill in `skills/koto/` (conventions, migration checklist, `koto-version` in its frontmatter).
3. Bump `<Version>` in `src/Directory.Build.props` (e.g. `0.4.0`, or `0.5.0-preview.1` for a preview line) — keeps local builds consistent, though CI overrides it.
4. Commit, then tag and push:
   ```bash
   git tag v0.5.0-preview.1
   git push origin main --tags
   ```
5. GitHub Actions `publish.yml` triggers on `v*.*.*` tags: restore → build → test → `dotnet pack /p:Version=<tag without v>` → `NuGet/login` (OIDC → one-hour API key) → `dotnet nuget push` all packages in Koto.slnx (+ snupkg symbols) with `--skip-duplicate`. Authentication is nuget.org **Trusted Publishing**: a policy on nuget.org (owner `arigatory`, repository `arigatory/Koto`, workflow `publish.yml`, no environment, glob `Koto.*`) trusts the workflow's OIDC token, so no API key exists that could expire. A 403 on the push step means the policy is missing or inactive — check https://www.nuget.org/account/apikeys (Trusted Publishing section).
6. Verify: `gh run watch` in the Koto repo; then check a package page on nuget.org shows the new version.

A published version cannot be unpublished (only unlisted) — tag and push only after the full test suite and a Release `dotnet pack` are green locally.

## Consumers

Consumers reference via `<PackageReference Include="Koto.*" Version="X.Y.Z" />` (never ProjectReference across repos). After a release, bump versions in consumers only when they need the changes, following the guide in `docs/migrations/`.

## Rules for changes to Koto itself

- Changes must be **universal** — useful to any microservice, no consumer-specific logic.
- License check for any new dependency (MIT/Apache 2.0 only; see traps in SKILL.md).
- Version is driven by the tag; `Directory.Build.props` version is the fallback for local packs.
