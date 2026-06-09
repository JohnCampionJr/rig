---
'@jcamp/rig': minor
---

`rig --version` (and `-v`) now names which rig answered — the Node tool prints
`1.4.0 (node)` and the .NET tool `1.4.0 (.NET)`. Because `rig` delegates across
ecosystems, the tag always reflects the implementation actually running, so a
stray shim or wrong-ecosystem hand-off is obvious at a glance. The .NET tool
also drops the noisy `+<git-sha>` build suffix from its version output.
