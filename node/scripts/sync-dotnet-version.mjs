// Keep the .NET tool's version in lockstep with the npm package. Changesets is
// npm-only, so after `changeset version` bumps package.json this mirrors the new
// version into dotnet/src/Rig/Rig.csproj. Wired into the `release:version` script.
import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const { version } = JSON.parse(readFileSync(join(here, '..', 'package.json'), 'utf8'))
const csproj = join(here, '..', '..', 'dotnet', 'src', 'Rig', 'Rig.csproj')

const before = readFileSync(csproj, 'utf8')
const after = before.replace(/<Version>[^<]*<\/Version>/, `<Version>${version}</Version>`)
if (after === before && !before.includes(`<Version>${version}</Version>`)) {
  console.error(`sync-dotnet-version: no <Version> element found in ${csproj}`)
  process.exit(1)
}
writeFileSync(csproj, after)
console.log(`synced Rig.csproj → ${version}`)
