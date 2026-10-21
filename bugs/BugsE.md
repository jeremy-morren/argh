# Argh bugs, issue E: Completion and `__schema`

Proposed issues: [A: Parsing and binding](BugsA.md) · [B: Generator: uncompilable output and missing diagnostics](BugsB.md) · [C: Runtime, dispatch and hosting](BugsC.md) · [D: Help and XML docs](BugsD.md) · [E: Completion and `__schema`](BugsE.md)

The shell scripts, the `__complete` engine, and the cli-schema JSON that agents read.

| # | Bug | Severity | Scratch file |
|---|---|---|---|
| 33 | [Hidden commands, namespaces and flags are completed](#33-hidden-commands-namespaces-and-flags-are-completed) | Wrong result | `bug33.cs` |
| 34 | [`__schema` advertises command aliases that don't route or complete](#34-__schema-advertises-command-aliases-that-dont-route-or-complete) | Wrong result | `bug34.cs` |
| 35 | [`timeSpanRange` constraint values carry C# quotes in `__schema`](#35-timespanrange-constraint-values-carry-c-quotes-in-__schema) | Wrong result | `bug35.cs` |
| 37 | [`__argh_root` leaks into schema usage strings](#37-__argh_root-leaks-into-schema-usage-strings) | Minor | `bug37.cs` |
| 38 | [Root and namespace default handlers are missing from the schema when registered by alias](#38-root-and-namespace-default-handlers-are-missing-from-the-schema-when-registered-by-alias) | Wrong result | `bug38.cs` (build break, `#if BUG38`) |
| 39 | [`[Hidden(Schema = true)]` on a namespace is silently dropped](#39-hiddenschema--true-on-a-namespace-is-silently-dropped) | Minor | `bug39.cs` |
| 40 | [Global and namespace options are duplicated into every command's `parameters`](#40-global-and-namespace-options-are-duplicated-into-every-commands-parameters) | Wrong result | `bug40.cs` |
| 41 | [A committed `--flag=value` breaks bash completion](#41-a-committed---flagvalue-breaks-bash-completion) | Minor | `bug41.cs` |
| 62 | [Completion lists flags inconsistently after a value flag, `--`, an unknown flag or a positional](#62-completion-lists-flags-inconsistently-after-a-value-flag----an-unknown-flag-or-a-positional) | Wrong result | `bug62.cs` |
| 63 | [Enum collections lose their member names in `__schema`](#63-enum-collections-lose-their-member-names-in-__schema) | Wrong result | `bug63.cs` |

## Background

Found by reviewing `main` at `45954b6` (0.21.0) in a fresh clone, on Windows 11 with .NET 10. Every bug below was reproduced: by running the test fixture host `Nullean.Argh.Tests.CliHost` (built in Release), by building small probe projects that reference the framework, or both. Paths are relative to the repository root; line numbers are from `45954b6`.

The expected behaviour for each bug is what **clap** (Rust, v4.6.7) and **cobra** (Go, v1.10.2, with pflag v1.0.10) do, checked against their source. Where they differ, both are given. Source links are pinned to those release tags.

Repro commands use `EXE` for `.artifacts/bin/Nullean.Argh.Tests.CliHost/release/Nullean.Argh.Tests.CliHost.exe` with `NO_COLOR=1` set. The fixture's middleware output (`[tests:middleware:...]`) is left out.

Bugs fixed on the open branches are removed: 53 (en dash in help, and no Windows CI) and 65 (non-ASCII text in `__schema`) on `windows-encoding` ([#85](https://github.com/nullean/argh/pull/85)), 13 (parameters named like generated locals), 15 (`fooBar` and `foo_bar` sharing a local) and 66 (`Uri` and `Uri?` in one method) on `timeonly` ([#83](https://github.com/nullean/argh/pull/83)), the `allowed`/`denied` part of 35 on `enum-completion` ([#82](https://github.com/nullean/argh/pull/82)), and 31 (fish), 32 (zsh), 86 (bash 3.2 `mapfile`) and 87 (CRLF candidates) on `completion-test` ([#86](https://github.com/nullean/argh/pull/86), every real-shell matrix entry passing in [run 39530303045](https://github.com/jeremy-morren/argh/actions/runs/39530303045)). On `parse-culture` ([#88](https://github.com/nullean/argh/pull/88), stacked on [#87](https://github.com/nullean/argh/pull/87)): 71 (`float`, `double` and `decimal` no longer accept thousands separators: `ParserOptions.NumberStyles` now defaults to `NumberStyles.Float` for every floating-point and decimal parse), 72 (the culture-sensitive trailing re-parse; every numeric parse now uses the configured `ParserOptions`, invariant by default, with `Nullable<T>` lifted to `T`), and an unlisted bug where `[ArgumentParser]` on a `T?` parameter generated `Point??` and didn't compile (it now uses `IArgumentParser<T?>` if implemented, else `IArgumentParser<T>`). On `range-unbounded` ([#90](https://github.com/nullean/argh/pull/90)): 10 and 11 (every number, enum and char the generator emits now carries its type: `1.5m`, `1.5f`, `(byte)5`, `double.NaN`, `global::Level.High`, `(global::Access)(3)`), 17 (a nullable enum keeps its `?`), 36 (`null`/`default` defaults no longer appear in help or `__schema`), 58 (`[Range(typeof(T), "min", "max")]` is parsed at build time, with AGH0038 on a bad bound), 89 (validation attributes on namespace and global options members are checked, whether the value comes before the namespace, after the command or from `[Env]`), 90 (AGH0038 on a bad bound of an options-type member, inherited ones included), and an unlisted regression from [#89](https://github.com/nullean/argh/pull/89): an unset `short?`, `byte?` or `Half?` with `[AllowedValues]` failed "value must be one of" because the check had no `HasValue` guard. On `small-fixes` (on top of [#91](https://github.com/nullean/argh/pull/91), no PR yet): 19 (`[Env]` on a positional argument is read), 20 (a `params` array is optional), 67 (a required namespace option is accepted after the command, and a namespace with required options and no command shows its help), 74 (`--help` after a value-taking flag is that flag's value, and an unknown option before `--help` is an error), 92 (a global option between the namespace and the command is bound and validated) and 93 (no nullable warnings for an optional non-nullable `string` with a string validation attribute). On `bool-parsing` (on `small-fixes`, no PR yet): 1 (the command line wins over an environment variable, also for a global or namespace flag given before the command), 2 (`--flag=false` and clap's other boolish values set a plain `bool`), 6 (boolean environment variables are read case-insensitively), 8 (an unparseable `bool?` value is an error) and 70 (a positional `bool` compiles and binds); the branch also fixes these unlisted ones: a `bool` default of `true` was ignored, `bool?` never read its `[Env]` variable, non-boolean `bool[]` elements bound as `true`, and `UseEnvironmentPrefix` was never applied. On `unmatched-args` (no PR yet): 3 (options after the command get the same parsing and errors as before it, for every type), 4 (an unexpected argument is an error, as in clap) and 7 (`--` ends the options), and an unlisted one: an invalid environment value for an options member was ignored and is now an error. The remaining bugs keep their numbers, because entries refer to each other by number.

This is the bug report for `main` at `45954b6` (0.21.0), kept with a scratch project that exercises each bug. The branch sits on the open PR branch that contains every other one, currently `unmatched-args` (no PR yet; it sits on `bool-parsing` and `small-fixes`, also without PRs, which sit on `emitter-cleanup`, [#91](https://github.com/nullean/argh/pull/91), which carries #81 to #90), so the scratch project runs against all pending fixes and every bug listed here still reproduces with them. Whenever a fix lands it is rebased again, onto a local merge of the PR tips if the stack has more than one. Line numbers in the entries are still from `45954b6`. **New bugs go here, on the `bugs` branch:** add a numbered section to the file for its proposed issue (the next free number across all five files; numbers are never reused) and a `bugNN.cs` that exercises it; a bug fixed on a branch is removed from this file and its scratch file deleted, and the note under Background lists it. The five groups below are the proposed issues; each bug keeps the number the report gave it, because entries refer to each other by number.

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

### 37. `__argh_root` leaks into schema usage strings

A namespace default handler's usage is `"Nullean.Argh.Tests.CliHost storage __argh_root"` (`Schema.cs:110` via `BuildCommandUsageSynopsisTail`). Related to [bug 27](BugsC.md#27-commandcontextcommandpath-and-commandname-leak-__argh_root).

### 38. Root and namespace default handlers are missing from the schema when registered by alias

`EXE` with no arguments runs the root default (`marker:root-default`), and `alias-scope --output x` runs `marker:alias-build`, but the schema has no `rootDefault` and `alias-scope` has no `defaultCommand`.

Cause: `MapAndRootAlias` targets live in `RegistryNode.RootAlias`, but `EmitSchemaRootDefaultExpression` (`Schema.cs:68`) and `EmitSchemaDefaultHandlerForNamespace` (`:94`) read only `RootCommand`.

### 39. `[Hidden(Schema = true)]` on a namespace is silently dropped

The attribute docs promise `hidden: true` in the schema, but `CliNamespaceSchema` has no `hidden` field (cli-schema issue [#7](https://github.com/cli-schema/cli-schema/issues/7)), and `EmitCliNamespaceSchemaBody` (`Schema.cs:243`) ignores `ch.IsHiddenInSchema`. `schema-hidden-schema-ns` is indistinguishable from `schema-hidden-ns`. Until the spec allows it, omit the namespace or mark its commands hidden.

### 40. Global and namespace options are duplicated into every command's `parameters`

`hello` lists `verbose`, `severity`, `mode` and `api-url`, which are also in `globalOptions`; `storage list` repeats `prefix`. The spec defines `globalOptions` as "available on every command", so consumers see every flag twice.

**Expected (cli-schema v1):** each option listed once, at the level where it's declared. The spec defines root `globalOptions` as "Flags available on every command" ([README.md#L103](https://github.com/cli-schema/cli-schema/blob/7842ac913f2c5b117fa7e9709514fe688c7bce58/spec/v1/README.md#L103)), a namespace's `options` as "Flags available on all commands within this namespace" ([#L259](https://github.com/cli-schema/cli-schema/blob/7842ac913f2c5b117fa7e9709514fe688c7bce58/spec/v1/README.md#L259)), and a command's `parameters` as "Command-specific flags and positionals" ([#L231](https://github.com/cli-schema/cli-schema/blob/7842ac913f2c5b117fa7e9709514fe688c7bce58/spec/v1/README.md#L231)). Its examples never repeat a global option in a command: git's `C`, `no-pager`, `version` and `help` are only in `globalOptions` ([git.cli-schema.json#L62-L93](https://github.com/cli-schema/cli-schema/blob/7842ac913f2c5b117fa7e9709514fe688c7bce58/spec/v1/examples/git.cli-schema.json#L62-L93)), not in `commit`'s `parameters` ([#L105-L137](https://github.com/cli-schema/cli-schema/blob/7842ac913f2c5b117fa7e9709514fe688c7bce58/spec/v1/examples/git.cli-schema.json#L105-L137)); gh's `repo` and `help` are only in `globalOptions` ([gh.cli-schema.json#L49-L65](https://github.com/cli-schema/cli-schema/blob/7842ac913f2c5b117fa7e9709514fe688c7bce58/spec/v1/examples/gh.cli-schema.json#L49-L65)), not in `repo clone` ([#L173-L188](https://github.com/cli-schema/cli-schema/blob/7842ac913f2c5b117fa7e9709514fe688c7bce58/spec/v1/examples/gh.cli-schema.json#L173-L188)).

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
