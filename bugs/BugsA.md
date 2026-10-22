# Argh bugs, issue A: Parsing and binding

Proposed issues: [A: Parsing and binding](BugsA.md) · [B: Generator: uncompilable output and missing diagnostics](BugsB.md) · [C: Runtime, dispatch and hosting](BugsC.md) · [D: Help and XML docs](BugsD.md) · [E: Completion and `__schema`](BugsE.md)

Wrong results when reading argv and environment variables. The parser is generated, but every bug is a runtime result the user sees.

| # | Bug | Severity | Scratch file |
|---|---|---|---|
| 83 | [Command names are case-insensitive but flags aren't](#83-command-names-are-case-insensitive-but-flags-arent) | Minor | `bug83.cs` |
| 99 | [`[Url]` on a `Uri` accepts only http and https, on a `string` also ftp](#99-url-on-a-uri-accepts-only-http-and-https-on-a-string-also-ftp) | Wrong result | `bug99.cs` |

## Background

Found by reviewing `main` at `45954b6` (0.21.0) in a fresh clone, on Windows 11 with .NET 10. Every bug below was reproduced: by running the test fixture host `Nullean.Argh.Tests.CliHost` (built in Release), by building small probe projects that reference the framework, or both. Paths are relative to the repository root; line numbers are from `45954b6`.

The expected behaviour for each bug is what **clap** (Rust, v4.6.7) and **cobra** (Go, v1.10.2, with pflag v1.0.10) do, checked against their source. Where they differ, both are given. Source links are pinned to those release tags.

Repro commands use `EXE` for `.artifacts/bin/Nullean.Argh.Tests.CliHost/release/Nullean.Argh.Tests.CliHost.exe` with `NO_COLOR=1` set. The fixture's middleware output (`[tests:middleware:...]`) is left out.

Bugs fixed on the open branches are removed: 53 (en dash in help, and no Windows CI) and 65 (non-ASCII text in `__schema`) on `windows-encoding` ([#85](https://github.com/nullean/argh/pull/85)), 13 (parameters named like generated locals), 15 (`fooBar` and `foo_bar` sharing a local) and 66 (`Uri` and `Uri?` in one method) on `timeonly` ([#83](https://github.com/nullean/argh/pull/83)), the `allowed`/`denied` part of 35 on `enum-completion` ([#82](https://github.com/nullean/argh/pull/82)), and 31 (fish), 32 (zsh), 86 (bash 3.2 `mapfile`) and 87 (CRLF candidates) on `completion-test` ([#86](https://github.com/nullean/argh/pull/86), every real-shell matrix entry passing in [run 39530303045](https://github.com/jeremy-morren/argh/actions/runs/39530303045)). On `parse-culture` ([#88](https://github.com/nullean/argh/pull/88), stacked on [#87](https://github.com/nullean/argh/pull/87)): 71 (`float`, `double` and `decimal` no longer accept thousands separators: `ParserOptions.NumberStyles` now defaults to `NumberStyles.Float` for every floating-point and decimal parse), 72 (the culture-sensitive trailing re-parse; every numeric parse now uses the configured `ParserOptions`, invariant by default, with `Nullable<T>` lifted to `T`), and an unlisted bug where `[ArgumentParser]` on a `T?` parameter generated `Point??` and didn't compile (it now uses `IArgumentParser<T?>` if implemented, else `IArgumentParser<T>`). On `range-unbounded` ([#90](https://github.com/nullean/argh/pull/90)): 10 and 11 (every number, enum and char the generator emits now carries its type: `1.5m`, `1.5f`, `(byte)5`, `double.NaN`, `global::Level.High`, `(global::Access)(3)`), 17 (a nullable enum keeps its `?`), 36 (`null`/`default` defaults no longer appear in help or `__schema`), 58 (`[Range(typeof(T), "min", "max")]` is parsed at build time, with AGH0038 on a bad bound), 89 (validation attributes on namespace and global options members are checked, whether the value comes before the namespace, after the command or from `[Env]`), 90 (AGH0038 on a bad bound of an options-type member, inherited ones included), and an unlisted regression from [#89](https://github.com/nullean/argh/pull/89): an unset `short?`, `byte?` or `Half?` with `[AllowedValues]` failed "value must be one of" because the check had no `HasValue` guard. On `small-fixes` (on top of [#91](https://github.com/nullean/argh/pull/91), no PR yet): 19 (`[Env]` on a positional argument is read), 20 (a `params` array is optional), 67 (a required namespace option is accepted after the command, and a namespace with required options and no command shows its help), 74 (`--help` after a value-taking flag is that flag's value, and an unknown option before `--help` is an error), 92 (a global option between the namespace and the command is bound and validated) and 93 (no nullable warnings for an optional non-nullable `string` with a string validation attribute). On `bool-parsing` (on `small-fixes`, no PR yet): 1 (the command line wins over an environment variable, also for a global or namespace flag given before the command), 2 (`--flag=false` and clap's other boolish values set a plain `bool`), 6 (boolean environment variables are read case-insensitively), 8 (an unparseable `bool?` value is an error) and 70 (a positional `bool` compiles and binds); the branch also fixes these unlisted ones: a `bool` default of `true` was ignored, `bool?` never read its `[Env]` variable, non-boolean `bool[]` elements bound as `true`, and `UseEnvironmentPrefix` was never applied. On `unmatched-args` (no PR yet): 3 (options after the command get the same parsing and errors as before it, for every type), 4 (an unexpected argument is an error, as in clap) and 7 (`--` ends the options), and an unlisted one: an invalid environment value for an options member was ignored and is now an error. On `path-uri-input` (no PR yet): 5 (an empty or invalid `FileInfo`/`DirectoryInfo` value is a parse error and the constructor's exception is reported, also on .NET Framework) and 73 (an empty or invalid `Uri` is an error; the new `[UriOptions(UriKind)]` defaults to `Absolute`), and an unlisted one: `[UriScheme]` on a `string` generated code that didn't compile (CS1061 on `main`) and is now AGH0024. On `collection-elements` (no PR yet): 9 (every element, empty parts included, goes through the element parser, so `--ids=` and `--ids ,,` are errors), 96 (`[property: X]` validation attributes on a positional record are read) and 97 (`[Url]` on a `Uri[]` compiles and checks each element), and unlisted ones: collections of `T?` elements (`int?[]`, `IReadOnlySet<Guid?>`) didn't compile, value constraints on collections didn't compile, and `[Env]` and `UseEnvironmentPrefix` were never read for collections. On `flag-tokens` (no PR yet): 75 (a flag that takes one value given twice is an error by default; `ParserOptions.RepeatedScalarBehaviour` and `[RepeatedScalar]` change that for the app or one flag), 81 (short flags combine: `-vn foo`, `-nfoo`, `-n=foo`) and 95 (a value-taking flag followed by another option reports `a value is required for --x but none was supplied`; negative numbers stay values). On `human-time` (no PR yet): 76 (durations are read by the new public `HumanTime`, like Go's `time.ParseDuration` and Rust's `humantime`: `1h30m`, `1.5h`, `-5m`, `10ms` and `1w` work, a bare number is an error, units are case-sensitive, and `docs/parameters/index.md` documents the format; help still shows only `<timeSpan>`), and the `timeSpanRange` part of 35. The remaining bugs keep their numbers, because entries refer to each other by number.

Bugs 71 to 83: these started as suspicions and were confirmed with a repro app that has a global options class (`int Port = 80`, `long Big`, `double Ratio`, `float? Scale`, `decimal Money`, `DateTime? When`, `TimeSpan? Span`) and commands for each value type, in every binding path: method parameter, `[AsParameters]` DTO, environment variable, positional, and leading or trailing global.

This is the bug report for `main` at `45954b6` (0.21.0), kept with a scratch project that exercises each bug. The branch sits on the open PR branch that contains every other one, currently `human-time` (no PR yet; it sits on `flag-tokens`, `collection-elements`, `path-uri-input`, `unmatched-args`, `bool-parsing` and `small-fixes`, also without PRs, which sit on `emitter-cleanup`, [#91](https://github.com/nullean/argh/pull/91), which carries #81 to #90), so the scratch project runs against all pending fixes and every bug listed here still reproduces with them. Whenever a fix lands it is rebased again, onto a local merge of the PR tips if the stack has more than one. Line numbers in the entries are still from `45954b6`. **New bugs go here, on the `bugs` branch:** add a numbered section to the file for its proposed issue (the next free number across all five files; numbers are never reused) and a `bugNN.cs` that exercises it; a bug fixed on a branch is removed from this file and its scratch file deleted, and the note under Background lists it. The five groups below are the proposed issues; each bug keeps the number the report gave it, because entries refer to each other by number.

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

## DEV NOTE

When working: 
- Move as much code as possible into core rather than generated code, but take any opportunity to use Spans, Range etc (which have to be done in generated code). The reason is that we want generated code to be compilable on multiple platforms e.g. a project targeting .net472, .net5, .net10 at the same time.
- Base each branch/PR on previous ones, so that maintainer never has to deal with merge conflicts.
- Keep looking for any opportunity to simplify code or make a solution more general

## Bugs

### 83. Command names are case-insensitive but flags aren't

`HELLO` routes to `hello`, but `--NAME` is an unknown option. `-V` before the command gives `unknown short option '-V'`; after it, `missing value for short flag '-V'`, which is misleading.

**Expected (clap/cobra):** both are case-sensitive for commands and flags by default. Pick one rule and one message.

Dev note: separate PR: add CommandStringComparer (default to OrdinalIgnoreCase) and ArgumentStringComparer (default to Ordinal) to parser options. These should be used for all matching. Analyzer warning if 2 commands are defined that are case-insensitive equal. Ensure parser options are documented in docs/, including the dateonly and timeonly parts.

**Partly fixed on `unmatched-args` (no PR yet):** `-V` now gives `unknown short option '-V'` before and after the command. Command names are still matched case-insensitively and flags case-sensitively.

### 99. `[Url]` on a `Uri` accepts only http and https, on a `string` also ftp

```
Bugs.exe bug99-uri --link ftp://x   → Error: --link: URI scheme must be one of: http, https.   exit 2   ([Url] Uri link)
Bugs.exe bug99-str --url ftp://x    → url:ftp://x                                              exit 0   ([Url] string url)
```

The same attribute means different things on the two types, and help and `__schema` say so:

| | `[Url] Uri` | `[Url] string` |
|---|---|---|
| Accepts | `http`, `https` | `http`, `https`, `ftp` |
| Help | `[schemes: http\|https]` | `[url]` |
| `__schema` | `{"kind": "uriScheme", "values": ["http", "https"]}` | `{"kind": "url"}` |

The mapping is the same on `main` (`45954b6`). Reproduced in `bugs/bug99.cs`.

Cause: `src/Nullean.Argh.Generator/CliParserGenerator.Validation.cs:352-357` turns `[Url]` on a `Uri` into `UriSchemeConstraint("http", "https")`; on a `string` it becomes `UrlConstraint`, whose check allows `http`, `https` and `ftp` (`CliParserGenerator.Emit.Parsing.cs:1079-1094`).

**Expected:** the rule of .NET's `UrlAttribute`, which accepts `http`, `https` and `ftp` for an absolute `Uri` and for a `string` alike ([UrlAttribute.cs#L22-L33](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.ComponentModel.Annotations/src/System/ComponentModel/DataAnnotations/UrlAttribute.cs#L22-L33)), on both types, with the same help hint and `__schema` constraint.

### By design, after checking

- `--version` only works at the root (`hello --version` is an unknown option). clap accepts `--version` only where it's defined, which is the root by default, and cobra reports `unknown flag`, so this matches both.
