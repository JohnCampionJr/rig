# Releasing

The two tools ship **in lockstep** at the same version: `@jcamp/rig` (npm) and
`rig` (NuGet). [Changesets] drives the npm side; the .NET tool's version is kept
in sync by a script, and both registries are published **locally** (you stay in
control — no CI publish step).

## While you work

Add a changeset for any user-facing change:

```sh
cd node
pnpm changeset          # pick the bump (patch/minor/major) + write a summary
```

Commit the generated `.changeset/*.md` file alongside your change.

## Cutting a release

```sh
cd node

# 1. Version + changelog + .NET sync.
#    Consumes the changesets: bumps @jcamp/rig, writes node/CHANGELOG.md, and
#    mirrors the new version into dotnet/src/Rig/Rig.csproj.
pnpm release:version
#    → then update the root CHANGELOG.md by hand if the .NET tool changed
#      (Changesets only manages the npm changelog), and commit + push.

# 2. Publish npm (local).
npm login               # once
pnpm release:publish    # changeset publish → @jcamp/rig (prepublishOnly builds dist)

# 3. Publish .NET (local).
cd ..
dotnet pack dotnet/src/Rig/Rig.csproj -c Release -o artifacts \
  --include-symbols -p:SymbolPackageFormat=snupkg
dotnet nuget push artifacts/rig.<version>.nupkg \
  --api-key <YOUR_NUGET_KEY> \
  --source https://api.nuget.org/v3/index.json --skip-duplicate
```

(For .NET you can alternatively push a `v<version>` tag — `publish.yml` publishes
to NuGet via trusted publishing — but the local `dotnet nuget push` keeps it in
your hands like npm.)

## How the lockstep works

- **Changesets is npm-only.** It bumps `node/package.json` and writes
  `node/CHANGELOG.md`.
- **`node/scripts/sync-dotnet-version.mjs`** (run by `release:version`) copies
  the new version into `dotnet/src/Rig/Rig.csproj`, so both tools stay on the
  same number.
- The **root `CHANGELOG.md`** (the .NET / umbrella changelog) is maintained by
  hand — update it when the .NET tool changes.

[Changesets]: https://github.com/changesets/changesets
