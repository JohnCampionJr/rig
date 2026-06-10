---
'@jcamp/rig': minor
---

Rename the force-an-ecosystem escape-hatch bins: `rignode` → `rig-node` and
`rigdotnet` → `rig-net`. The everyday delegate-aware `rig` command is unchanged —
only the explicit "always run Node" / "always run .NET" entry points were renamed
for naming consistency. Shell completion registers the new names. The .NET rig
locates the Node tool by the new `rig-node` name and keeps `rignode` as a
transitional fallback, so delegation still works while both tools roll forward.
