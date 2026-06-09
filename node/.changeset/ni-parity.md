---
'@jcamp/rig': minor
---

Add @antfu/ni-parity dependency verbs and a parity test suite.

New verbs map onto ni's: `uninstall` (`remove`/`rm`, ni's `nun`), `dlx` (`x`,
ni's `nlx`), `ci` (frozen/clean install, ni's `nci`), `upgrade` (`-i`
interactive, ni's `nu`), and `global` (`g`, global install, ni's `ni -g`). These
resolve through the same `package-manager-detector` command table ni uses, so
they emit byte-identical commands across npm / pnpm / yarn (classic *and*
Berry) / bun.

Fixes three command divergences from ni: `dlx` on classic yarn now uses `npx`
(Berry uses `yarn dlx`), `dlx` no longer injects an implicit `npx -y`, and
`add -D` on bun uses `-D` instead of `-d`. A new `test/ni-parity.test.ts`
asserts rig's generated commands match ni's and pins the few intentional
divergences.

Also renames the self-update verb `update` → `self-update` so it no longer
collides with the new `upgrade` dependency verb (renamed in the .NET rig too,
keeping the two tools in sync).
