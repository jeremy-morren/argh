# Argh bugs, issue A: Parsing and binding

Proposed issues: [A: Parsing and binding](BugsA.md) · [B: Generator: uncompilable output and missing diagnostics](BugsB.md) · [C: Runtime, dispatch and hosting](BugsC.md) · [D: Help and XML docs](BugsD.md) · [E: Completion and `__schema`](BugsE.md)

Wrong results when reading argv and environment variables. The parser is generated, but every bug is a runtime result the user sees.

| # | Bug | Severity | Scratch file |
|---|---|---|---|
| 9 | [A required separator collection accepts zero items](#9-a-required-separator-collection-accepts-zero-items) | Minor | `bug09.cs` |
| 75 | [A repeated scalar flag silently takes the last value](#75-a-repeated-scalar-flag-silently-takes-the-last-value) | Design | `bug75.cs` |
| 76 | [A bare number is a `TimeSpan` in days, and the compact format is undocumented and incomplete](#76-a-bare-number-is-a-timespan-in-days-and-the-compact-format-is-undocumented-and-incomplete) | Misleading | `bug76.cs` |
| 81 | [Combined short flags are rejected, and the limitation isn't documented](#81-combined-short-flags-are-rejected-and-the-limitation-isnt-documented) | Minor | `bug81.cs` |
| 83 | [Command names are case-insensitive but flags aren't](#83-command-names-are-case-insensitive-but-flags-arent) | Minor | `bug83.cs` |
| 95 | [A value-taking flag followed by another flag takes that flag as its value](#95-a-value-taking-flag-followed-by-another-flag-takes-that-flag-as-its-value) | Wrong result | `bug95.cs` |
| 96 | [`[property: X]` validation attributes on a positional record's `[AsParameters]` parameters are ignored](#96-property-x-validation-attributes-on-a-positional-records-asparameters-parameters-are-ignored) | Wrong result | `bug96.cs` |

## Background

Found by reviewing `main` at `45954b6` (0.21.0) in a fresh clone, on Windows 11 with .NET 10. Every bug below was reproduced: by running the test fixture host `Nullean.Argh.Tests.CliHost` (built in Release), by building small probe projects that reference the framework, or both. Paths are relative to the repository root; line numbers are from `45954b6`.

The expected behaviour for each bug is what **clap** (Rust, v4.6.7) and **cobra** (Go, v1.10.2, with pflag v1.0.10) do, checked against their source. Where they differ, both are given. Source links are pinned to those release tags.

Repro commands use `EXE` for `.artifacts/bin/Nullean.Argh.Tests.CliHost/release/Nullean.Argh.Tests.CliHost.exe` with `NO_COLOR=1` set. The fixture's middleware output (`[tests:middleware:...]`) is left out.

Bugs fixed on the open branches are removed: 53 (en dash in help, and no Windows CI) and 65 (non-ASCII text in `__schema`) on `windows-encoding` ([#85](https://github.com/nullean/argh/pull/85)), 13 (parameters named like generated locals), 15 (`fooBar` and `foo_bar` sharing a local) and 66 (`Uri` and `Uri?` in one method) on `timeonly` ([#83](https://github.com/nullean/argh/pull/83)), the `allowed`/`denied` part of 35 on `enum-completion` ([#82](https://github.com/nullean/argh/pull/82)), and 31 (fish), 32 (zsh), 86 (bash 3.2 `mapfile`) and 87 (CRLF candidates) on `completion-test` ([#86](https://github.com/nullean/argh/pull/86), every real-shell matrix entry passing in [run 39530303045](https://github.com/jeremy-morren/argh/actions/runs/39530303045)). On `parse-culture` ([#88](https://github.com/nullean/argh/pull/88), stacked on [#87](https://github.com/nullean/argh/pull/87)): 71 (`float`, `double` and `decimal` no longer accept thousands separators: `ParserOptions.NumberStyles` now defaults to `NumberStyles.Float` for every floating-point and decimal parse), 72 (the culture-sensitive trailing re-parse; every numeric parse now uses the configured `ParserOptions`, invariant by default, with `Nullable<T>` lifted to `T`), and an unlisted bug where `[ArgumentParser]` on a `T?` parameter generated `Point??` and didn't compile (it now uses `IArgumentParser<T?>` if implemented, else `IArgumentParser<T>`). On `range-unbounded` ([#90](https://github.com/nullean/argh/pull/90)): 10 and 11 (every number, enum and char the generator emits now carries its type: `1.5m`, `1.5f`, `(byte)5`, `double.NaN`, `global::Level.High`, `(global::Access)(3)`), 17 (a nullable enum keeps its `?`), 36 (`null`/`default` defaults no longer appear in help or `__schema`), 58 (`[Range(typeof(T), "min", "max")]` is parsed at build time, with AGH0038 on a bad bound), 89 (validation attributes on namespace and global options members are checked, whether the value comes before the namespace, after the command or from `[Env]`), 90 (AGH0038 on a bad bound of an options-type member, inherited ones included), and an unlisted regression from [#89](https://github.com/nullean/argh/pull/89): an unset `short?`, `byte?` or `Half?` with `[AllowedValues]` failed "value must be one of" because the check had no `HasValue` guard. On `small-fixes` (on top of [#91](https://github.com/nullean/argh/pull/91), no PR yet): 19 (`[Env]` on a positional argument is read), 20 (a `params` array is optional), 67 (a required namespace option is accepted after the command, and a namespace with required options and no command shows its help), 74 (`--help` after a value-taking flag is that flag's value, and an unknown option before `--help` is an error), 92 (a global option between the namespace and the command is bound and validated) and 93 (no nullable warnings for an optional non-nullable `string` with a string validation attribute). On `bool-parsing` (on `small-fixes`, no PR yet): 1 (the command line wins over an environment variable, also for a global or namespace flag given before the command), 2 (`--flag=false` and clap's other boolish values set a plain `bool`), 6 (boolean environment variables are read case-insensitively), 8 (an unparseable `bool?` value is an error) and 70 (a positional `bool` compiles and binds); the branch also fixes these unlisted ones: a `bool` default of `true` was ignored, `bool?` never read its `[Env]` variable, non-boolean `bool[]` elements bound as `true`, and `UseEnvironmentPrefix` was never applied. On `unmatched-args` (no PR yet): 3 (options after the command get the same parsing and errors as before it, for every type), 4 (an unexpected argument is an error, as in clap) and 7 (`--` ends the options), and an unlisted one: an invalid environment value for an options member was ignored and is now an error. On `path-uri-input` (no PR yet): 5 (an empty or invalid `FileInfo`/`DirectoryInfo` value is a parse error and the constructor's exception is reported, also on .NET Framework) and 73 (an empty or invalid `Uri` is an error; the new `[UriOptions(UriKind)]` defaults to `Absolute`), and an unlisted one: `[UriScheme]` on a `string` generated code that didn't compile (CS1061 on `main`) and is now AGH0024. The remaining bugs keep their numbers, because entries refer to each other by number.

Bugs 71 to 83: these started as suspicions and were confirmed with a repro app that has a global options class (`int Port = 80`, `long Big`, `double Ratio`, `float? Scale`, `decimal Money`, `DateTime? When`, `TimeSpan? Span`) and commands for each value type, in every binding path: method parameter, `[AsParameters]` DTO, environment variable, positional, and leading or trailing global.

This is the bug report for `main` at `45954b6` (0.21.0), kept with a scratch project that exercises each bug. The branch sits on the open PR branch that contains every other one, currently `path-uri-input` (no PR yet; it sits on `unmatched-args`, `bool-parsing` and `small-fixes`, also without PRs, which sit on `emitter-cleanup`, [#91](https://github.com/nullean/argh/pull/91), which carries #81 to #90), so the scratch project runs against all pending fixes and every bug listed here still reproduces with them. Whenever a fix lands it is rebased again, onto a local merge of the PR tips if the stack has more than one. Line numbers in the entries are still from `45954b6`. **New bugs go here, on the `bugs` branch:** add a numbered section to the file for its proposed issue (the next free number across all five files; numbers are never reused) and a `bugNN.cs` that exercises it; a bug fixed on a branch is removed from this file and its scratch file deleted, and the note under Background lists it. The five groups below are the proposed issues; each bug keeps the number the report gave it, because entries refer to each other by number.

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

### 9. A required separator collection accepts zero items

```
EXE schema-separator-list --ids=  → ids:   exit 0
EXE schema-separator-list --ids ,,  → ids:   exit 0
```

The flag is `[required]` in help. The repeatable-flag path checks the item count (`Emit.Parsing.cs:627-635`); the separator path only checks that the flag is present (`:568-603`) and skips empty parts at `:587`.

**Expected (clap/cobra):** both keep the empty parts as values and let the element parser reject them. pflag's `IntSlice` gives `invalid argument "" for "--ids" flag: strconv.Atoi: parsing "": invalid syntax` ([int_slice.go#L22-L30](https://github.com/spf13/pflag/blob/v1.0.10/int_slice.go#L22-L30)). clap splits `--ids=` into `[""]` and `--ids ,,` into three empty strings ([parser.rs#L1178-L1195](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L1178-L1195)), and an integer parser reports `cannot parse integer from empty string`. Follow them: don't skip empty parts silently.

Dev note: Separate PR: handle collections: parse into empty strings, then pass to the underlying type parser, who can then reject them if necessary. Add tests that a parameter of T (or T?) is parsed in the same way as a parameter of type T[] i.e. a user can define a custom argument parser on a collection parameter T?[] for type T and it is used. As a result, the default path for e.g. int (empty string is invalid) is handled correctly.

### 75. A repeated scalar flag silently takes the last value

```
EXE hello --name a --name b     → b
EXE hello -n a --name b         → b
EXE --port 2 glob --port 1      → 1
```

The fixture's `deploy --app-env prod --app-port 1 --app-env dev` gives `deploy:dev:1`. No warning anywhere; the generator has no notion of "specified multiple times".

**Expected (clap/cobra):** they differ. clap errors with `the argument '--name <NAME>' cannot be used multiple times` unless `ArgAction::Append` or `args_override_self` is set; pflag takes the last value silently. A design decision, but a deliberate one.

Separate PR: Handle repeated scalar flags explicitly. Add RepeatedScalarBehaviour enum and option: `Reject` (default), `AllowIdentical` (if the same value is passed twice, use this) and `TakeLast` (uses the last value provided). This should also be overridable for a specific flag, which takes precedence over global options.

### 76. A bare number is a `TimeSpan` in days, and the compact format is undocumented and incomplete

`ArghTimeSpan` (`src/Nullean.Argh.Core/ArghTimeSpan.cs`) accepts `10s`, `90m`, `1d`, `00:30`, `1:2` (1 h 2 min) and `1.00:00:00`, and rejects `1.5h`, `1h30m`, `-5m`, `10ms`, `1w`, `0.5s`, `+5s` and `PT10S`. A bare `10` falls back to `TimeSpan.TryParse`, which reads it as 10 days: `temporal-cmd --duration 10` → 864000 seconds. Help shows only `<timeSpan>`, and `docs/` never mentions the compact form.

**Expected (clap/cobra):** Go's `time.ParseDuration` and Rust's `humantime` both reject a bare number and accept compound values such as `1h30m`, `1.5h`, `-5m` and `10ms`. Reject bare numbers (or document them), accept compound values, and document the format in help.

Separate PR: Implement feature-equivalent to `humantime` or `time.ParseDuration` (whichever is more popular or understood): Expose as a public method `HumanTime.Parse/TryParse(CultureInfo)`, with the same feature set (so that users could use just this part of the library, and document this in documentation). Then use that in parsing TimeSpan.

### 81. Combined short flags are rejected, and the limitation isn't documented

`-vn foo`, `-nfoo`, `-c1` and `-vv` all fail with `Error: combined short flags (e.g. -abc) are not supported.` followed by the full help. `-n=foo` works. `docs/` never states the limitation.

**Expected (clap/cobra):** both accept `-vn foo` and `-nfoo`. 

Dev note: support combined short flags, PR combine with #75.

### 83. Command names are case-insensitive but flags aren't

`HELLO` routes to `hello`, but `--NAME` is an unknown option. `-V` before the command gives `unknown short option '-V'`; after it, `missing value for short flag '-V'`, which is misleading.

**Expected (clap/cobra):** both are case-sensitive for commands and flags by default. Pick one rule and one message.

Dev note: separate PR: add CommandStringComparer (default to OrdinalIgnoreCase) and ArgumentStringComparer (default to Ordinal) to parser options. These should be used for all matching. Analyzer warning if 2 commands are defined that are case-insensitive equal. Ensure parser options are documented in docs/, including the dateonly and timeonly parts.

**Partly fixed on `unmatched-args` (no PR yet):** `-V` now gives `unknown short option '-V'` before and after the command. Command names are still matched case-insensitively and flags case-sensitively.

### 95. A value-taking flag followed by another flag takes that flag as its value

```
Bugs.exe bug95 --name --port 5             → Error: unexpected argument '5' found.              exit 2
Bugs.exe bug95 --port --severity warning   → Error: unexpected argument 'warning' found.        exit 2
Bugs.exe bug95 --port --severity=warning   → Error: invalid int for --port: '--severity=warning'.
Bugs.exe --port --severity warning bug95   → Error: invalid int for --port: '--severity'.       (root position)
Bugs.exe bug95 --name --help               → name:--help, exit 0                                (no help)
Bugs.exe bug95 --name -x                   → name:-x, exit 0
Bugs.exe bug95 --offset -5                 → offset:-5                                          (negative numbers work)
```

The token after a value-taking flag is always its value, even when it is another known flag, so the error names the wrong token or the wrong problem, and `--name --help` binds `--help` instead of failing. `--name --help` binding is the deliberate behaviour of the `small-fixes` branch (it was the expected behaviour of the removed bug 74, which said both clap and cobra bind it); it matches pflag but not clap. Negative numbers (`--offset -5`, `numeric-defaults --limit -2.5`, `--factor -0.25` in the test host) are values today and must stay so. Reproduced in `bugs/bug95.cs` on `unmatched-args`.

Cause: the value-taking branch checks only that another token exists (`if (i + 1 >= args.Length)`, `src/Nullean.Argh.Generator/CliParserGenerator.Emit.Runner.cs:182-196`, and the root parser at `CliParserGenerator.Emit.Parsing.cs:171-180`, both at `45954b6`; the same check is on the current base) and never whether that token is a flag. Before `unmatched-args` the left-over `5` or `warning` was silently ignored (the removed bug 4); now it is reported as an unexpected argument.

**Expected (clap/cobra):** they differ; follow clap.
- clap: when an option is waiting for its value and the next token is a long flag, it starts that flag, and starting any argument first resolves the pending option ([`parse_opt_value`](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L1077), [`react`](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L1150)), which fails with no values ([`verify_num_args`](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L1335-L1346)): `error: a value is required for '--name <NAME>' but none was supplied` ([format.rs#L219](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/error/format.rs#L219)). That includes `--name --help`: `--help` is an argument too, so the pending `--name` fails before help runs. The token is taken as a value only if the option has `allow_hyphen_values` ([long](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L776-L781), [short](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L894-L899)), or `allow_negative_numbers` for a negative number ([arg.rs#L1549-L1552](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/builder/arg.rs#L1549-L1552)). By default `--offset -5` is therefore `error: unexpected argument '-5' found` ([parser.rs#L186-L195](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L186-L195), [format.rs#L358](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/error/format.rs#L358)); `--offset=-5` always works.
- pflag takes the next token whatever it is (`'--flag arg'`, [flag.go#L1020-L1023](https://github.com/spf13/pflag/blob/v1.0.10/flag.go#L1020-L1023); `'-f arg'`, [#L1092-L1095](https://github.com/spf13/pflag/blob/v1.0.10/flag.go#L1092-L1095)), which is what Argh does now.

Follow clap for tokens that are known flags (`--port`, `--severity`, `--help`, `-h`): report `a value is required for '--name'`. Keep negative numbers as values of numeric options, as if `allow_negative_numbers` were on, so `--offset -5` and `--limit -2.5` keep working; `--name=--help` stays the way to pass a flag-like string.

### 96. `[property: X]` validation attributes on a positional record's `[AsParameters]` parameters are ignored

```
Bugs.exe bug96 --count 99      → count:99, exit 0        ([property: Range(1, 10)])
Bugs.exe bug96 --name abcdef   → name:abcdef, exit 0     ([property: StringLength(3)])
Bugs.exe bug96 --mode bogus    → mode:bogus, exit 0      ([property: AllowedValues("fast", "slow")])
Bugs.exe bug96 --level 99      → Error: --level: value must be between 1 and 10.     ([Range] on the parameter)
```

Every validation attribute is ignored when it targets the synthesized property of a positional record bound with `[AsParameters]` (`public sealed record Opts([property: Range(1, 10)] int Count = 1, ...)`). The same attributes on a class's properties, or on the record parameter without a target, are checked. `[property: ...]` is the form .NET's own `Validator` sees, so it's the one users write. The same on `main` (`45954b6`), checked with a probe. Reported by the session building `path-uri-input`; reproduced in `bugs/bug96.cs`.

Cause: `ReadValidationConstraints(cp, ...)` reads the constructor parameter's attributes only (`src/Nullean.Argh.Generator/CliParserGenerator.ParameterModel.cs:497`), and `[property: ...]` puts them on the property. `[CollectionSyntax]` already looks at the mirrored property (`CliParserGenerator.SymbolReaders.cs:704-711`).

**Expected:** checked exactly like the same attributes on a class property. Read the mirrored property's validation attributes the way `[CollectionSyntax]` is read.

### By design, after checking

- `--version` only works at the root (`hello --version` is an unknown option). clap accepts `--version` only where it's defined, which is the root by default, and cobra reports `unknown flag`, so this matches both.
