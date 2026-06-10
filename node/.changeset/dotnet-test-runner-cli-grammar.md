---
'@jcamp/rig': patch
---

**.NET tool:** `rig test` / `rig coverage` now pick the correct `dotnet test`
CLI grammar per runner. The SDK chooses between its two `dotnet test` parsers
*solely* by `global.json`'s `test.runner`, but rig always passed the project with
`--project` — a switch the classic VSTest parser doesn't know, so it failed with
`MSB1001: Unknown switch` on any non-MTP project. rig now detects the runner from
`global.json` and uses the positional project form for VSTest and `--project` for
Microsoft.Testing.Platform. The `--filter` expression (`FullyQualifiedName~…`) is
shared by both runners, so `rig test <name>` / `--filter` works across them.
