# rig — design & architecture

The rationale behind the key decisions, and the shape of the code. (rig began as
an in-tree dev launcher and was extracted to its own repo; the host-specific
extraction history lives in that original repo.)

## Principles

- **Convention-first, config-optional.** Auto-discover everything inferable
  (solution, test project, coverage runsettings/collector, kill target, rebuild
  targets); `.rig.json` supplies only the non-inferable. Zero config for the 80%.
- **Less typing.** Bare `rig` → menu; unambiguous prefixes resolve; curated short
  aliases; sticky `defaultProject`; shell completion of real project/test names.
- **rig manages its own config.** `rig default` / `rig run --remember` persist
  `defaultProject` without hand-editing — and writes are comment/format-preserving.

## CLI stack: System.CommandLine + Spectre.Console

Two libraries at **different layers** — deliberately *not* Spectre.Console.Cli.

- **System.CommandLine 2.0 (GA)** — parsing, dispatch, and **standardized shell
  completion via `dotnet-suggest`** (dynamic values through `CompletionSources`).
  The redesign discontinued the `Hosting`/`NamingConventionBinder`/`Rendering`
  companion packages, which *validates* bringing our own renderer.
- **Spectre.Console** (the rendering library, not `.Cli`) — menu, prompts, tables,
  the Figlet banner.

The seam is "call `AnsiConsole` inside each `SetAction`." The interactive menu
re-enters the same parser (`root.Parse([verb]).Invoke()`) so there's never a
second dispatch path. Verb-prefix resolution is a small pre-parse rewrite; curated
short forms are real `Command.Aliases` (resolved + shown in help natively).

## Code organization: class-per-verb, wiring/logic split

- **`Verbs/*Command.cs`** — thin System.CommandLine wiring (name, options,
  completion, action), one class per verb.
- **`Verbs/*Verb.cs`** — the work, as plain methods with **no System.CommandLine
  types in their signatures** — unit-testable in isolation; the parser layer never
  leaks into the work layer.
- Supporting types: `RigSession` (resolved root + config + layered env),
  `RootResolver` (precedence walk-up: `.rig.json` > solution > `.git`),
  `RigConfig` (+ JSONC-tolerant loader), `ProjectDiscovery`, `TestEnumeration`
  (multi-framework reflection picker), `Capabilities`, `Exec`/`EnvStack`,
  `DotEnv`, `ConfigWriter`/`JsoncEditor`, `PrefixResolver`, `Completions`, `Ui`.

```
src/Rig/
  Program.cs            # RootCommand + flat registry + prefix rewrite
  Verbs/*Command.cs     # wiring          *Verb.cs # logic (no CLI types)
  ...core helpers...    rig.schema.json
tests/Rig.Tests/        # MSTest (Microsoft.Testing.Platform) + AwesomeAssertions
```

## Coverage: bundle ReportGenerator.Core in-process

Rather than shell out to an installed tool, `rig` references **`ReportGenerator.Core`**
(Apache-2.0) and renders in-process — always available, zero install (~2.8 MB of
managed deps, no native assets). Collection is runner-aware (MTP `--coverage` vs
VSTest `--collect:"XPlat Code Coverage"`), normalizing to Cobertura. **Pro** is the
same engine with features unlocked by the `REPORTGENERATOR_LICENSE` env var, so
`rig` bundles only the free engine and a user's license lights up Pro with zero
`rig` code.

## Comment-preserving config writes

System.CommandLine's DOM can't round-trip JSON comments, and we avoid a Newtonsoft
dependency. `JsoncEditor` instead **locates the value's byte span with
`Utf8JsonReader` and splices the raw UTF-8** — comments, formatting, key order, and
unicode all survive; only a brand-new file is written fresh.

## Target framework

`net8.0` + `<RollForward>Major</RollForward>` so the published tool installs and
runs on .NET 8/9/10. The tool shells out to `dotnet`, and `MetadataLoadContext`
only *reads* metadata — so an older host can inspect a newer (e.g. net10) test DLL.

## Verification gates

1. **MetadataLoadContext on net8 reading a higher-TFM DLL** — proven (a net8 host
   enumerates test classes from a net10 assembly; host runtime dir + the test's
   bin dir is enough, name-level resolution suffices for metadata).
2. **Coverage → Cobertura → in-process report** — confirmed end-to-end.
3. **`dotnet-suggest` completes a *globally-installed* `rig`** — *pending*:
   `CompletionSources` are wired and enumerate live, but end-to-end shell
   completion through `dotnet-suggest` against an installed apphost isn't exercised
   yet. Prove before relying on it.
