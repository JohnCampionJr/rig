# Changesets

This folder drives `@jcamp/rig`'s versioning + changelog. Add a changeset for any
user-facing change:

```sh
pnpm changeset          # pick a bump (patch/minor/major) + write a summary
```

Commit the generated `.md` alongside your change. At release time,
`pnpm release:version` consumes them (bumps the version, writes CHANGELOG.md, and
syncs the .NET tool's version). See [RELEASING.md](../../RELEASING.md).
