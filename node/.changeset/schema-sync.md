---
'@jcamp/rig': patch
---

Fix and sync the config JSON schemas. `node/rig.schema.json` now mirrors the
authoritative root `rig.schema.json` (rich `dotnet.*` namespace, deprecated-key
annotations) so the two can't drift, and a stray `</content>` tag that left the
root `rig.schema.json` as invalid JSON has been removed.
