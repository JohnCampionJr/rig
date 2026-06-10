---
'@jcamp/rig': patch
---

Security/data-loss hardening. On Windows, `.cmd`/`.bat` shims (pnpm/npm/yarn) are
now invoked through `cmd.exe` with fully quoted, caret-escaped arguments instead
of `{ shell: true }`, which left args unescaped — closing a command-injection hole
where a metacharacter (or a `"` breakout) in a package/script name or forwarded
arg could run a second command. `rig clean`/`rig rebuild` now refuse to delete
anything that resolves outside the workspace and skip symlinks/junctions, so a
hostile workspace layout can't redirect the recursive delete. Added a `SECURITY.md`
documenting the trust model. Mirrored in the .NET tool (delete guard).
