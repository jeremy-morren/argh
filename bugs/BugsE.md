# Argh bugs, issue E: Completion and `__schema`

Proposed issues: [A: Parsing and binding](BugsA.md) · [B: Generator: uncompilable output and missing diagnostics](BugsB.md) · [C: Runtime, dispatch and hosting](BugsC.md) · [D: Help and XML docs](BugsD.md) · [E: Completion and `__schema`](BugsE.md)

The shell scripts, the `__complete` engine, and the cli-schema JSON that agents read.

| # | Bug | Severity | Scratch file |
|---|---|---|---|
| 33 | [Hidden commands, namespaces and flags are completed](#33-hidden-commands-namespaces-and-flags-are-completed) | Wrong result | `bug33.cs` |
| 34 | [`__schema` advertises command aliases that don't route or complete](#34-__schema-advertises-command-aliases-that-dont-route-or-complete) | Wrong result | `bug34.cs` |
| 35 | [`timeSpanRange` constraint values carry C# quotes in `__schema`](#35-timespanrange-constraint-values-carry-c-quotes-in-__schema) | Wrong result | `bug35.cs` |
| 36 | [`defaultValue: "default"` for nullable value types](#36-defaultvalue-default-for-nullable-value-types) | Wrong result | `bug36.cs` |
| 37 | [`__argh_root` leaks into schema usage strings](#37-__argh_root-leaks-into-schema-usage-strings) | Minor | `bug37.cs` |
| 38 | [Root and namespace default handlers are missing from the schema when registered by alias](#38-root-and-namespace-default-handlers-are-missing-from-the-schema-when-registered-by-alias) | Wrong result | `bug38.cs` (build break, `#if BUG38`) |
| 39 | [`[Hidden(Schema = true)]` on a namespace is silently dropped](#39-hiddenschema--true-on-a-namespace-is-silently-dropped) | Minor | `bug39.cs` |
| 40 | [Global and namespace options are duplicated into every command's `parameters`](#40-global-and-namespace-options-are-duplicated-into-every-commands-parameters) | Design | `bug40.cs` |
| 41 | [A committed `--flag=value` breaks bash completion](#41-a-committed---flagvalue-breaks-bash-completion) | Minor | `bug41.cs` |
| 62 | [Completion lists flags inconsistently after a value flag, `--`, an unknown flag or a positional](#62-completion-lists-flags-inconsistently-after-a-value-flag----an-unknown-flag-or-a-positional) | Wrong result | `bug62.cs` |
| 63 | [Enum collections lose their member names in `__schema`](#63-enum-collections-lose-their-member-names-in-__schema) | Wrong result | `bug63.cs` |

## Background

Found by reviewing `main` at `45954b6` (0.21.0) in a fresh clone, on Windows 11 with .NET 10. Every bug below was reproduced: by running the test fixture host `Nullean.Argh.Tests.CliHost` (built in Release), by building small probe projects that reference the framework, or both. Paths are relative to the repository root; line numbers are from `45954b6`.

The expected behaviour for each bug is what **clap** (Rust, v4.6.7) and **cobra** (Go, v1.10.2, with pflag v1.0.10) do, checked against their source. Where they differ, both are given. Source links are pinned to those release tags.

Repro commands use `EXE` for `.artifacts/bin/Nullean.Argh.Tests.CliHost/release/Nullean.Argh.Tests.CliHost.exe` with `NO_COLOR=1` set. The fixture's middleware output (`[tests:middleware:...]`) is left out.

Bugs fixed on the open branches are removed: 53 (en dash in help, and no Windows CI) and 65 (non-ASCII text in `__schema`) on `windows-encoding` ([#85](https://github.com/nullean/argh/pull/85)), 13 (parameters named like generated locals), 15 (`fooBar` and `foo_bar` sharing a local) and 66 (`Uri` and `Uri?` in one method) on `timeonly` ([#83](https://github.com/nullean/argh/pull/83)), the `allowed`/`denied` part of 35 on `enum-completion` ([#82](https://github.com/nullean/argh/pull/82)), and 31 (fish), 32 (zsh), 86 (bash 3.2 `mapfile`) and 87 (CRLF candidates) on `completion-test` ([#86](https://github.com/nullean/argh/pull/86), every real-shell matrix entry passing in [run 39530303045](https://github.com/jeremy-morren/argh/actions/runs/39530303045)). On `parse-culture` ([#88](https://github.com/nullean/argh/pull/88), stacked on [#87](https://github.com/nullean/argh/pull/87)): 72 (the culture-sensitive trailing re-parse; every numeric parse now uses the configured `ParserOptions`, invariant by default, with `Nullable<T>` lifted to `T`), and an unlisted bug where `[ArgumentParser]` on a `T?` parameter generated `Point??` and didn't compile (it now uses `IArgumentParser<T?>` if implemented, else `IArgumentParser<T>`). The remaining bugs keep their numbers, because entries refer to each other by number.

This is the bug report for `main` at `45954b6` (0.21.0), kept with a scratch project that exercises each bug. The branch sits on the open PR branch that contains every other one, currently `parsable-types` ([#89](https://github.com/nullean/argh/pull/89), which carries #81 to #88), so the scratch project runs against all pending fixes and every bug listed here still reproduces with them. Whenever a fix lands it is rebased again, onto a local merge of the PR tips if the stack has more than one. Line numbers in the entries are still from `45954b6`. **New bugs go here, on the `bugs` branch:** add a numbered section to the file for its proposed issue (the next free number across all five files; numbers are never reused) and a `bugNN.cs` that exercises it; a bug fixed on a branch is removed from this file and its scratch file deleted, and the note under Background lists it. The five groups below are the proposed issues; each bug keeps the number the report gave it, because entries refer to each other by number.

## How to run the scratch project

```bash
dotnet build bugs -c Release
.artifacts/bin/Bugs/release/Bugs.exe bug01 --help          # one command per bug
dotnet build bugs/hosted -c Release                        # bug22 and bug25 need the hosting package
.artifacts/bin/Bugs.Hosted/release/Bugs.Hosted.exe bug22
```

Every `bugNN.cs` starts with a comment giving the exact command, the actual output and the expected output. Bugs whose generated code doesn't compile are wrapped in `#if BUGNN`, so the default build is green; enable one at a time:

```bash
dotnet build bugs -c Release -p:DefineConstants=BUG10
```

`lib/` holds the types that must live in another assembly (bug16, bug56), and `hosted/` the bugs that need the hosting package (22, 25, 78, 79). `Program.cs` reads `BUGS_RUN_TWICE` (bug77) and `BUGS_CULTURE`. `bug40` and `bug41` are comment-only, because the bug is in a generated shell script or in the schema shape rather than in a handler.

## Bugs

### 33. Hidden commands, namespaces and flags are completed

```
EXE __complete bash -- hidden          → hidden-cmd
EXE __complete bash -- schema-hidden-s → schema-hidden-schema-ns
EXE __complete bash -- -               → includes --api-url ([Hidden])
EXE __complete bash -- schema-hidden-param -   → includes --internal-id
```

`Attributes.cs:202` documents `[Hidden]` as "hidden from user-facing help and autocomplete".

Cause: `EmitNextSegmentsArray` (`CliParserGenerator.Completion.cs:448`) and `CollectCompletionFlagStrings` (`:459`) never check `IsHidden` or `IsHiddenInSchema`.

**Expected (clap/cobra):** cobra leaves hidden subcommands out (`IsAvailableCommand()` is false for `Hidden`, [command.go#L1607-L1610](https://github.com/spf13/cobra/blob/v1.10.2/command.go#L1607-L1610), used at [completions.go#L518](https://github.com/spf13/cobra/blob/v1.10.2/completions.go#L518)) and hidden flags too ([bash_completions.go#L696-L698](https://github.com/spf13/cobra/blob/v1.10.2/bash_completions.go#L696-L698)). clap's dynamic engine marks hidden candidates and drops them whenever a visible candidate remains ([complete.rs#L217-L219](https://github.com/clap-rs/clap/blob/v4.6.7/clap_complete/src/engine/complete.rs#L217-L219)); its older static scripts don't filter. Follow cobra and the attribute's own contract.

### 34. `__schema` advertises command aliases that don't route or complete

`[CommandName("renamed-cmd", "rc")]` → schema `"aliases": ["rc"]` (`Schema.cs:160`), but:

```
EXE rc                      → Error: unknown command or namespace 'rc'.   exit 2
EXE __complete bash -- r    → renamed-cmd only
```

`CommandAliases` is used nowhere except `Schema.cs`. Non-ASCII aliases behave the same: `[CommandName("café", "kafé")]` routes `café` (and `CAFÉ`), but `kafé` is an unknown command.

**Expected (clap/cobra):** aliases route: clap's `aliases_to` matches the name or any alias ([command.rs#L4985-L4988](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/builder/command.rs#L4985-L4988)); cobra's `findNext` uses `HasAlias` ([command.go#L798-L801](https://github.com/spf13/cobra/blob/v1.10.2/command.go#L798-L801)). Completion of aliases is optional: clap offers visible ones, cobra offers only `Name()` ([completions.go#L518-L520](https://github.com/spf13/cobra/blob/v1.10.2/completions.go#L518-L520)). Either route them or don't advertise them.

### 35. `timeSpanRange` constraint values carry C# quotes in `__schema`

```
validate-timespan-range      → "min": "\"5m\"", "max": "\"2h\""
```

`Validation.cs:287` stores `ToCSharpString()`. Help renders the values correctly. The cli-schema spec also wants `timeSpanRange` in ISO 8601 (`PT5M`). `fileExtensions` writes `zip` without the leading dot the spec's example uses (`Validation.cs:366`).

### 36. `defaultValue: "default"` for nullable value types

`dry-run-cmd --dry-run`, `build --prefetch-regression` and `schema-output-formats --format` have `"defaultValue": "default"` in the schema, and help shows `[default: default]` for `int? n = null`, `Mode? m = null` and `TimeSpan ts = default`.

Cause: `ParameterModel.cs:987` turns a `null` default on a value type into the literal `"default"`; `GetSchemaDefaultValue` (`Schema.cs:486`) filters only `"null"`, and `Emit.Help.cs:909` prints whatever it gets.

**Expected (clap/cobra):** clap prints `[default: X]` only when the argument takes a value and has a non-empty default ([help_template.rs#L789-L807](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/output/help_template.rs#L789-L807)); pflag omits `(default ...)` for zero values such as `false`, `0`, `""` and `<nil>` ([flag.go#L557-L585](https://github.com/spf13/pflag/blob/v1.0.10/flag.go#L557-L585)). Omit it.

### 37. `__argh_root` leaks into schema usage strings

A namespace default handler's usage is `"Nullean.Argh.Tests.CliHost storage __argh_root"` (`Schema.cs:110` via `BuildCommandUsageSynopsisTail`). Related to [bug 27](BugsC.md#27-commandcontextcommandpath-and-commandname-leak-__argh_root).

### 38. Root and namespace default handlers are missing from the schema when registered by alias

`EXE` with no arguments runs the root default (`marker:root-default`), and `alias-scope --output x` runs `marker:alias-build`, but the schema has no `rootDefault` and `alias-scope` has no `defaultCommand`.

Cause: `MapAndRootAlias` targets live in `RegistryNode.RootAlias`, but `EmitSchemaRootDefaultExpression` (`Schema.cs:68`) and `EmitSchemaDefaultHandlerForNamespace` (`:94`) read only `RootCommand`.

### 39. `[Hidden(Schema = true)]` on a namespace is silently dropped

The attribute docs promise `hidden: true` in the schema, but `CliNamespaceSchema` has no `hidden` field (cli-schema issue [#7](https://github.com/cli-schema/cli-schema/issues/7)), and `EmitCliNamespaceSchemaBody` (`Schema.cs:243`) ignores `ch.IsHiddenInSchema`. `schema-hidden-schema-ns` is indistinguishable from `schema-hidden-ns`. Until the spec allows it, omit the namespace or mark its commands hidden.

### 40. Global and namespace options are duplicated into every command's `parameters`

`hello` lists `verbose`, `severity`, `mode` and `api-url`, which are also in `globalOptions`; `storage list` repeats `prefix`. The spec defines `globalOptions` as "available on every command", so consumers see every flag twice. A design choice, but worth a decision.

### 41. A committed `--flag=value` breaks bash completion

Bash splits `--mode=x` into three words (`=` is in `COMP_WORDBREAKS`): `EXE __complete bash -- --mode = x hel` returns nothing, because the engine consumes `=` as the value and then sees `x` as unknown. The template would need `_get_comp_words_by_ref -n =`.

**Expected (cobra):** cobra's bash script reassembles words split at `=`.

### 62. Completion lists flags inconsistently after a value flag, `--`, an unknown flag or a positional

What `__complete bash -- <context> ... -` returns:

| Context | After a value-taking flag | After `--` | After an unknown flag | After a positional |
|---|---|---|---|---|
| root | flags | nothing | nothing | nothing |
| namespace | **nothing** | nothing | nothing | nothing |
| command | flags | **flags** | **flags** | **flags** |

Cause: `Completion.cs:110-113` assigns `pos = ConsumeCommandTail(...)` and then lists the command's flags unconditionally, never comparing `pos` with `committed.Length`; the root and namespace paths return at `:95-96` as soon as a committed token starts with `-`. Separately, `ns --config v -` returns nothing because `ConsumeForComplete_Namespace_*` is built from namespace members only (`:336`) while `__c_flags_ns_*` merges the global options (`:45-46`).

**Expected (clap/cobra):** after `--` nothing but positional values is offered; after a value-taking flag the flag's values are offered; otherwise flags are offered the same way at every level. Both clap's dynamic engine and cobra's `__complete` follow the parser state rather than a per-level special case.

### 63. Enum collections lose their member names in `__schema`

`color-set --colors` (a `List<TestColor>`) is `"type": "array", "elementType": "string"` with no `enumValues`; `color-one` (a single `TestColor`) has `"type": "enum", "enumValues": [...]`.

Cause: `Schema.cs:450` emits `enumValues` only when `type == "enum"`, and `:460` forces `elementType` to `string` because cli-schema v1 only allows `string|integer|number|boolean` there. The spec-legal shape keeps `elementType: "string"` and adds `"validations": [{"kind": "allowed", "values": ["red", "deep-blue", "green"]}]`, which `BuildConstraintsExpression` can emit.
