---
'@jcamp/rig': patch
---

Further Windows hardening against hostile repositories. The shell (`cmd.exe`) is
now resolved to its absolute `%ComSpec%` path everywhere rig spawns it, so a
`cmd.exe` planted in the working directory can't be run ahead of the real one.
The .NET tool's delegation to a `rig-node.cmd` sibling now passes its arguments
through the same caret-escaped, fully-quoted `cmd.exe` invocation the Node tool
uses, closing the cmd re-parse injection path for forwarded arguments.
