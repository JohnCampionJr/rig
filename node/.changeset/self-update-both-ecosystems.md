---
'@jcamp/rig': minor
---

`rig self-update` now updates **both** ecosystems by default. The .NET and Node
tools ship in lockstep at the same version, so updating only one left a
version-mismatched pair. `self-update` now updates the tool you invoked and then,
when the sibling rig is installed, hands off to *its* `self-update` — always with
`--self-only`, so the two can't bounce back and forth. Use `--self-only` to update
just the current ecosystem; `--check` reports both. A missing sibling is a
friendly no-op. Mirrored in the .NET tool.
