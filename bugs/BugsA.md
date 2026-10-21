# Argh bugs, issue A: Parsing and binding

Proposed issues: [A: Parsing and binding](BugsA.md) · [B: Generator: uncompilable output and missing diagnostics](BugsB.md) · [C: Runtime, dispatch and hosting](BugsC.md) · [D: Help and XML docs](BugsD.md) · [E: Completion and `__schema`](BugsE.md)

Wrong results when reading argv and environment variables. The parser is generated, but every bug is a runtime result the user sees.

| # | Bug | Severity | Scratch file |
|---|---|---|---|
| 1 | [An environment variable overrides a global or namespace flag given before the command](#1-an-environment-variable-overrides-a-global-or-namespace-flag-given-before-the-command) | Wrong result | `bug01.cs` |
| 2 | [`--flag=false` on a plain `bool` sets it to `true`](#2---flagfalse-on-a-plain-bool-sets-it-to-true) | Wrong result | `bug02.cs` |
| 3 | [A global enum flag, or a namespace option of any special type, after the command accepts any value silently](#3-a-global-enum-flag-or-a-namespace-option-of-any-special-type-after-the-command-accepts-any-value-silently) | Wrong result | `bug03.cs` |
| 4 | [Extra positional arguments are ignored](#4-extra-positional-arguments-are-ignored) | Wrong result | `bug04.cs` |
| 5 | [An empty value for a required `FileInfo` or `DirectoryInfo` crashes](#5-an-empty-value-for-a-required-fileinfo-or-directoryinfo-crashes) | Crash | `bug05.cs` |
| 6 | [Boolean environment variables are parsed case-sensitively](#6-boolean-environment-variables-are-parsed-case-sensitively) | Wrong result | `bug06.cs` |
| 7 | [`--` isn't a terminator](#7----isnt-a-terminator) | Misleading message | `bug07.cs` |
| 8 | [`bool?` with an unparseable value becomes `true`](#8-bool-with-an-unparseable-value-becomes-true) | Minor | `bug08.cs` |
| 9 | [A required separator collection accepts zero items](#9-a-required-separator-collection-accepts-zero-items) | Minor | `bug09.cs` |
| 19 | [`[Env]` on a positional argument is shown in help but never read](#19-env-on-a-positional-argument-is-shown-in-help-but-never-read) | Wrong result | `bug19.cs` |
| 20 | [`params string[]` is treated as required](#20-params-string-is-treated-as-required) | Minor | `bug20.cs` |
| 67 | [A required namespace option is only accepted before the command](#67-a-required-namespace-option-is-only-accepted-before-the-command) | Wrong result | `bug67.cs` |
| 71 | [`float`, `double` and `decimal` accept thousands separators](#71-float-double-and-decimal-accept-thousands-separators) | Wrong result | `bug71.cs` |
| 73 | [A required `Uri` binds empty, whitespace and garbage values as relative URIs](#73-a-required-uri-binds-empty-whitespace-and-garbage-values-as-relative-uris) | Wrong result | `bug73.cs` |
| 74 | [`--help` anywhere in the arguments wins](#74---help-anywhere-in-the-arguments-wins) | Wrong result | `bug74.cs` |
| 75 | [A repeated scalar flag silently takes the last value](#75-a-repeated-scalar-flag-silently-takes-the-last-value) | Design | `bug75.cs` |
| 76 | [A bare number is a `TimeSpan` in days, and the compact format is undocumented and incomplete](#76-a-bare-number-is-a-timespan-in-days-and-the-compact-format-is-undocumented-and-incomplete) | Misleading | `bug76.cs` |
| 81 | [Combined short flags are rejected, and the limitation isn't documented](#81-combined-short-flags-are-rejected-and-the-limitation-isnt-documented) | Minor | `bug81.cs` |
| 83 | [Command names are case-insensitive but flags aren't, and the unknown `-V` message depends on position](#83-command-names-are-case-insensitive-but-flags-arent-and-the-unknown--v-message-depends-on-position) | Minor | `bug83.cs` |
| 89 | [`[Range]` and `[AllowedValues]` on namespace or global options members are shown in help but never checked](#89-range-and-allowedvalues-on-namespace-or-global-options-members-are-shown-in-help-but-never-checked) | Wrong result | `bug89.cs` |

## Background

Found by reviewing `main` at `45954b6` (0.21.0) in a fresh clone, on Windows 11 with .NET 10. Every bug below was reproduced: by running the test fixture host `Nullean.Argh.Tests.CliHost` (built in Release), by building small probe projects that reference the framework, or both. Paths are relative to the repository root; line numbers are from `45954b6`.

The expected behaviour for each bug is what **clap** (Rust, v4.6.7) and **cobra** (Go, v1.10.2, with pflag v1.0.10) do, checked against their source. Where they differ, both are given. Source links are pinned to those release tags.

Repro commands use `EXE` for `.artifacts/bin/Nullean.Argh.Tests.CliHost/release/Nullean.Argh.Tests.CliHost.exe` with `NO_COLOR=1` set. The fixture's middleware output (`[tests:middleware:...]`) is left out.

Bugs fixed on the open branches are removed: 53 (en dash in help, and no Windows CI) and 65 (non-ASCII text in `__schema`) on `windows-encoding` ([#85](https://github.com/nullean/argh/pull/85)), 13 (parameters named like generated locals), 15 (`fooBar` and `foo_bar` sharing a local) and 66 (`Uri` and `Uri?` in one method) on `timeonly` ([#83](https://github.com/nullean/argh/pull/83)), the `allowed`/`denied` part of 35 on `enum-completion` ([#82](https://github.com/nullean/argh/pull/82)), and 31 (fish), 32 (zsh), 86 (bash 3.2 `mapfile`) and 87 (CRLF candidates) on `completion-test` ([#86](https://github.com/nullean/argh/pull/86), every real-shell matrix entry passing in [run 39530303045](https://github.com/jeremy-morren/argh/actions/runs/39530303045)). On `parse-culture` ([#88](https://github.com/nullean/argh/pull/88), stacked on [#87](https://github.com/nullean/argh/pull/87)): 72 (the culture-sensitive trailing re-parse; every numeric parse now uses the configured `ParserOptions`, invariant by default, with `Nullable<T>` lifted to `T`), and an unlisted bug where `[ArgumentParser]` on a `T?` parameter generated `Point??` and didn't compile (it now uses `IArgumentParser<T?>` if implemented, else `IArgumentParser<T>`). On `range-unbounded` ([#90](https://github.com/nullean/argh/pull/90)): 10 and 11 (every number, enum and char the generator emits now carries its type: `1.5m`, `1.5f`, `(byte)5`, `double.NaN`, `global::Level.High`, `(global::Access)(3)`), 17 (a nullable enum keeps its `?`), 36 (`null`/`default` defaults no longer appear in help or `__schema`), 58 (`[Range(typeof(T), "min", "max")]` is parsed at build time, with AGH0038 on a bad bound), and an unlisted regression from [#89](https://github.com/nullean/argh/pull/89): an unset `short?`, `byte?` or `Half?` with `[AllowedValues]` failed "value must be one of" because the check had no `HasValue` guard. The remaining bugs keep their numbers, because entries refer to each other by number.

Bugs 71 to 83: these started as suspicions and were confirmed with a repro app that has a global options class (`int Port = 80`, `long Big`, `double Ratio`, `float? Scale`, `decimal Money`, `DateTime? When`, `TimeSpan? Span`) and commands for each value type, in every binding path: method parameter, `[AsParameters]` DTO, environment variable, positional, and leading or trailing global.

This is the bug report for `main` at `45954b6` (0.21.0), kept with a scratch project that exercises each bug. The branch sits on the open PR branch that contains every other one, currently `range-unbounded` ([#90](https://github.com/nullean/argh/pull/90), which carries #81 to #89), so the scratch project runs against all pending fixes and every bug listed here still reproduces with them. Whenever a fix lands it is rebased again, onto a local merge of the PR tips if the stack has more than one. Line numbers in the entries are still from `45954b6`. **New bugs go here, on the `bugs` branch:** add a numbered section to the file for its proposed issue (the next free number across all five files; numbers are never reused) and a `bugNN.cs` that exercises it; a bug fixed on a branch is removed from this file and its scratch file deleted, and the note under Background lists it. The five groups below are the proposed issues; each bug keeps the number the report gave it, because entries refer to each other by number.

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

### 1. An environment variable overrides a global or namespace flag given before the command

```
TEST_API_URL=fromenv EXE --api-url cli env-global
env-global-api-url:fromenv        (expected: cli)

TEST_NS_KEY=nsenv EXE env-ns --key cli key
nsenv                             (expected: cli)
```

The same flag after the command works: `EXE env-global --api-url cli` prints `cli`. `EnvAttribute` documents the variable as a fallback "when the flag is not provided on the command line".

Cause: `EmitOptionsReconstructLocals` in `src/Nullean.Argh.Generator/CliParserGenerator.Emit.Parsing.cs:420-433` looks only in the command-trailing `flags` dictionary, then reads the environment variable, and only falls back to the pre-parsed static field, which already holds `cli`, when the variable is unset.

**Expected (clap/cobra):** the command line always wins. clap applies environment values after parsing and skips any argument already matched ([parser.rs#L1414-L1422](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L1414-L1422)). cobra has no env support; viper's order is explicit `Set`, then flags, then env, then config, then defaults ([README](https://github.com/spf13/viper/blob/528f741/README.md#L75-L82)), and it reads a flag only when `HasChanged()` ([viper.go#L1222-L1223](https://github.com/spf13/viper/blob/528f741/viper.go#L1222-L1223)).

Dev note: command line wins. Use command line, then env var.

### 2. `--flag=false` on a plain `bool` sets it to `true`

```
EXE mixed-variadic first --verbose=false
first:first verbose:True          (expected: False, or an error)

EXE env-bool --verbose=False      → True
EXE -v=false severity-cmd         → verbose set
```

`bool?` honours the value: `dry-run-cmd --dry-run=false` gives `false`.

Cause: binding tests `flags.ContainsKey(...)` and ignores the stored value. `src/Nullean.Argh.Generator/CliParserGenerator.Emit.Runner.cs:298` and `:301`; `CliParserGenerator.Emit.Parsing.cs:277` and `:404-407`.

**Expected (clap/cobra):** they differ, but neither ignores the value. pflag's `bool` has `NoOptDefVal = "true"` and parses an attached value with `strconv.ParseBool`, so `--verbose=false`, `=0` and `=F` set it to false and `=no` is an error ([bool.go#L20-L24](https://github.com/spf13/pflag/blob/v1.0.10/bool.go#L20-L24), [#L56](https://github.com/spf13/pflag/blob/v1.0.10/bool.go#L56)). clap's `ArgAction::SetTrue` takes no value, so `--verbose=false` is `error: unexpected value 'false' for '--verbose' found; no more were expected`, exit 2 ([parser.rs#L829-L850](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L829-L850)); accepting a value needs `num_args(0..=1)`, and then only `true`/`false` parse.

Dev note: Create separate PR: match clap's `BoolishValueParser` for flags and `FalseyValueParser` for environment variables.

### 3. A global enum flag, or a namespace option of any special type, after the command accepts any value silently

```
EXE severity-cmd --severity bogus
severity:Information              (expected: an error, exit 2)

EXE severity-cmd --severity 2     → Warning   (numeric value accepted)
EXE severity-cmd --severity=      → Information
```

Before the command the same flag is validated: `EXE --severity bogus severity-cmd` prints `Error: invalid value`.

Cause: `Emit.Runner.cs:286-287` skips `OptionsInjected` parameters in `EmitParseAndAssign`. The re-parse in `Emit.Parsing.cs:484-490` uses `Enum.TryParse(txt, true, ...)`, which accepts integers and ignores `[EnumValue]` names, and a comment says it keeps the static fallback silently. `int` and `long` fall back silently the same way at `:439-444`.

The same re-parse covers global and namespace options of every type, not only enums, and it also swallows a missing value. With a global `int Port = 80`, `DateTime? When` and so on:

```
EXE glob --port 1,000       → port=80, exit 0        (leading: Error: invalid int for --port: '1,000')
EXE glob --when bogus       → when=null, exit 0
EXE glob --port 1 --port x  → port=80, exit 0        (the valid value is lost too)
EXE glob --port --verbose   → port=80 and verbose=false: both lost
REPRO_GPORT=1,000 EXE glob  → the environment value fails the same way
```

Cause for these: `Emit.Parsing.cs:436-470` is `TryParse(...) ? parsed : fallback` for every type. Namespace options of every special type too: `TimeSpan`, `DateTime`, `DateTimeOffset`, `DateOnly`, `Uri`, `FileInfo` and `DirectoryInfo`, plain and nullable. Reproduced with the `scalars-ns` fixture on the `timeonly` branch, where `P` holds the required options:

```
EXE scalars-ns P --day-opt nope show   → Error: invalid DateOnly for --day-opt: 'nope'.   exit 2
EXE scalars-ns P show --day-opt nope   → day-opt=none                                     exit 0
```

**Expected (clap/cobra):** an error naming the value and the choices, at any position. clap: `error: invalid value 'bogus' for '--severity <SEVERITY>'` followed by `[possible values: trace, information, warning]`, exit 2 ([format.rs#L323](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/error/format.rs#L323)); its `PossibleValuesParser` compares strings only, so no numeric index ([possible_value.rs#L221-L228](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/builder/possible_value.rs#L221-L228)). cobra has no enum type; a custom `pflag.Value` error is wrapped as `invalid argument "bogus" for "--severity" flag: ...` ([errors.go#L108-L117](https://github.com/spf13/pflag/blob/v1.0.10/errors.go#L108-L117)) and printed as `Error: ...`, exit 1.

Dev note: Create a separate PR: handle additional unmatched parameters. Match clap/cobra behavoiur.

### 4. Extra positional arguments are ignored

```
EXE hello --name x extra          → ok:x, exit 0
EXE count-cmd --count 1 foo bar   → count:1, exit 0
EXE build extra                   → the root default command runs
```

Cause: `Emit.Runner.cs:279` adds every non-dash token to `positionals`, and nothing after binding (`:358-397`) checks `positionals.Count` against the declared positionals.

**Expected (clap/cobra):** clap, whose positionals are declared like Argh's, rejects them: `error: unexpected argument 'extra' found`, exit 2 ([parser.rs#L574-L580](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L574-L580)). cobra's default `legacyArgs` errors with `unknown command "extra" for "app"` only on a root that has subcommands; a subcommand or leaf accepts extras unless `cobra.NoArgs` or `ExactArgs` is set ([args.go#L28-L47](https://github.com/spf13/cobra/blob/v1.10.2/args.go#L28-L47)). Follow clap.

Dev note: follow clap: reject unparsed arguments. To work around this, however, ensure the user has a way to accept unmatched parameters (i.e. splat, should be possible to build `ls`). Combine with bug #3.

### 5. An empty value for a required `FileInfo` or `DirectoryInfo` crashes

```
EXE file-cmd --file ""
Unhandled exception. System.ArgumentException: The path is empty. (Parameter 'path')
   at System.IO.FileInfo..ctor(...)
   ...                             exit 1
```

Also `dir-cmd --dir ""`, `validate-existing-file --file ""` and `--file "  "`. The optional (`FileInfo?`) paths guard against this.

Cause: `Emit.Runner.cs:1217-1228` (required `FileInfo`) and `:1260-1271` (required `DirectoryInfo`) call `new FileInfo(raw!)` with no `IsNullOrWhiteSpace` guard. The optional branches at `:1197` and `:1240` have one.

**Expected (clap/cobra):** never a stack trace. clap's `PathBufValueParser` rejects an empty value: `a value is required for '--file <FILE>' but none was supplied`, exit 2 ([value_parser.rs#L1022-L1028](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/builder/value_parser.rs#L1022-L1028)). pflag has no path type; its `string` accepts `""`. Follow clap: a parse error, exit 2.

Dev note: Separeate PR: handle invalid `FileInfo`/`DirectoryInfo` input. Parse error on empty, allow whitespace: a file/directory named as a space is a valid (if silly) file. After that, do `new FileInfo()` or `new DirectoryInfo()`, catch any exception thrown and reject with the error message.

### 6. Boolean environment variables are parsed case-sensitively

```
MY_APP_VERBOSE=False EXE env-bool
env-verbose:True                  (expected: False)
```

`FALSE` and `no` give `True` too.

Cause: `Emit.Runner.cs:298` tests `EnvRaw != "0" && EnvRaw != "false"`; the same at `Emit.Parsing.cs:404`.

**Expected (clap/cobra):** case-insensitive. clap's `FalseyValueParser` and `BoolishValueParser` lowercase the value and match `y`, `yes`, `t`, `true`, `on`, `1` or `n`, `no`, `f`, `false`, `off`, `0` ([str_to_bool.rs#L1-L22](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/util/str_to_bool.rs#L1-L22)); `Falsey` treats an empty value as false and anything unknown as true, `Boolish` rejects unknown values. Go's `strconv.ParseBool` accepts `1`, `t`, `T`, `TRUE`, `true`, `True`, `0`, `f`, `F`, `FALSE`, `false`, `False`.

Dev note: combine with Bug #2.

### 7. `--` isn't a terminator

```
EXE temporal-cmd --
Error: unknown option '--'. Did you mean '--on'?

EXE copy-variadic red -- -x
Error: unknown option '--'

EXE copy-variadic red -5
Error: missing value for short flag '-5'.
```

So a positional can't start with `-`, and there's no way to pass one.

Cause: `Emit.Runner.cs:114-119` takes `a.Substring(2)`, gets `""`, and calls `FailUnknownLongOption("")`, whose fuzzy match (distance ≤ 2) then suggests any flag of two characters or fewer. `Emit.Parsing.cs:120-160` does the same at the root.

**Expected (clap/cobra):** both treat `--` as the end of options; everything after it is a positional, including `-5` and `--help` (clap [parser.rs#L130-L146](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L130-L146); pflag [flag.go#L1145-L1150](https://github.com/spf13/pflag/blob/v1.0.10/flag.go#L1145-L1150)). Without `--`, pflag reports `unknown shorthand flag: '5' in -5`, and clap accepts `-5` only for arguments with `allow_negative_numbers` or `allow_hyphen_values` ([#L693-L695](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L693-L695)).

Combine with bug #4. 

### 8. `bool?` with an unparseable value becomes `true`

```
EXE dry-run-cmd --dry-run=bogus   → dry-run:true
EXE dry-run-cmd --dry-run=no      → dry-run:true
```

Cause: `ParseNullableBool` in `Emit.Dispatch.cs:537-539` returns `fromYesSwitch` when `bool.TryParse` fails.

**Expected (clap/cobra):** an error. clap: `error: invalid value 'bogus' for '--dry-run [<DRY_RUN>]'` with `[possible values: true, false]` ([value_parser.rs#L1706-L1715](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/builder/value_parser.rs#L1706-L1715)). pflag: `invalid argument "bogus" for "--dry-run" flag: strconv.ParseBool: parsing "bogus": invalid syntax` ([errors.go#L117](https://github.com/spf13/pflag/blob/v1.0.10/errors.go#L117)).

Combime with #2.

### 9. A required separator collection accepts zero items

```
EXE schema-separator-list --ids=  → ids:   exit 0
EXE schema-separator-list --ids ,,  → ids:   exit 0
```

The flag is `[required]` in help. The repeatable-flag path checks the item count (`Emit.Parsing.cs:627-635`); the separator path only checks that the flag is present (`:568-603`) and skips empty parts at `:587`.

**Expected (clap/cobra):** both keep the empty parts as values and let the element parser reject them. pflag's `IntSlice` gives `invalid argument "" for "--ids" flag: strconv.Atoi: parsing "": invalid syntax` ([int_slice.go#L22-L30](https://github.com/spf13/pflag/blob/v1.0.10/int_slice.go#L22-L30)). clap splits `--ids=` into `[""]` and `--ids ,,` into three empty strings ([parser.rs#L1178-L1195](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L1178-L1195)), and an integer parser reports `cannot parse integer from empty string`. Follow them: don't skip empty parts silently.

Dev note: Separate PR: handle collections: parse into empty strings, then pass to the underlying type parser, who can then reject them if necessary. Add tests that a parameter of T (or T?) is parsed in the same way as a parameter of type T[] i.e. a user can define a custom argument parser on a collection parameter T?[] for type T and it is used. As a result, the default path for e.g. int (empty string is invalid) is handled correctly.

### 19. `[Env]` on a positional argument is shown in help but never read

```csharp
static void EnvPos([Argument, Env("HUNT_NAME")] string name) { }
```

`HUNT_NAME=x app env-pos` → `Error: missing required argument <name>`, while help prints `<name>  [env: HUNT_NAME]`.

Cause: `Emit.Runner.cs:289` skips positionals (`if (p.Kind == ParameterKind.Positional) continue;`) in env binding; the help emitter still appends the tag. Either bind it or report a diagnostic.

**Expected (clap/cobra):** clap's `env` works on positionals the same as on options.

Dev note: garden variety bug, fix.

### 20. `params string[]` is treated as required

A handler with `params string[] items`, run with no arguments → `missing required flag --items`.

Cause: `ComputeRequired` (`ParameterModel.cs:953`) ignores `IParameterSymbol.IsParams`.

Dev note: garden variety bug, fix.

### 67. A required namespace option is only accepted before the command

```
EXE scalars-ns show P        → Error: missing required flag --span.   exit 2
EXE scalars-ns P show        → runs
```

`P` is the required options, as in [bug 3](#3-a-global-enum-flag-or-a-namespace-option-of-any-special-type-after-the-command-accepts-any-value-silently). Optional namespace options are accepted on either side of the command.

Cause: the required check runs in the namespace pre-parse, before the command is matched (`src/Nullean.Argh.Generator/CliParserGenerator.Emit.Parsing.cs:302-309`), and fails before the command's own arguments are read.

**Expected (clap/cobra):** clap's `global(true)` arguments and cobra's persistent flags can appear before or after the subcommand, and required checks run after the whole command line is parsed.


Dev note: garden variety bug: match clap/cobra, fix.

### 71. `float`, `double` and `decimal` accept thousands separators

```
EXE num --f 1,5 --d 1,5 --m 1,5     → f=15 d=15 m=15     exit 0
EXE num --f 1,5,5                   → 155
EXE num --f 5,                      → 5
```

The same in every path: required, DTO, env var, positional, leading global. `int` rejects `1,000`, so the types disagree. Also accepted everywhere: `+5`, ` 5 `, and for `float`/`double` `1e3`, `NaN`, `Infinity`.

Cause: `NumberStyles.Float | NumberStyles.AllowThousands` at `Emit.Runner.cs:1370, 1379` and the nullable variants at `:1498-1506`; `NumberStyles.Number` (which includes `AllowThousands`) for `decimal` at `:1388`; `Emit.Parsing.cs:448, 452, 454, 456`.

**Expected (clap/cobra):** both reject `1,5`: Rust's `f64::from_str` and Go's `strconv.ParseFloat` take no thousands separators, and both are culture-independent.

**Partly addressed on `parse-culture` ([#88](https://github.com/nullean/argh/pull/88)):** the default is unchanged (.NET's own number styles, which allow thousands separators), but `ParserOptions.NumberStyles` now lets an app exclude `AllowThousands`.

Dev note: completely fix this as part of the parse-culture branch/pr.

### 73. A required `Uri` binds empty, whitespace and garbage values as relative URIs

```
EXE uri-req --u ""              → bound as an empty relative Uri, exit 0
EXE uri-req --u "not a uri"     → bound, relative
EXE uri-req --u "::"            → bound, relative
EXE uri-opt --n ""              → null              (optional: whitespace is treated as absent)
```

The fixture's `uri-cmd --uri ""` then throws `InvalidOperationException: not supported for a relative URI` from the handler, exit 1. `[UriScheme]` rejects relative values, but an optional `[UriScheme]` parameter with `""` becomes null. The DTO path behaves the same.

Cause: `Emit.Runner.cs:1284` has an `IsNullOrWhiteSpace` guard for the optional path only; `:1302` binds anything `Uri.TryCreate(..., UriKind.RelativeOrAbsolute)` accepts.

**Expected (clap/cobra):** clap's `PathBufValueParser` rejects an empty value, and a `value_parser!(Url)` rejects a non-URL. Reject empty and whitespace; decide whether relative URIs are intended.

Dev note: separate pr: add UriOptionsAttribute(UriKind). If not provided use UriKind.Absolute. Reject empty string.

### 74. `--help` anywhere in the arguments wins

```
EXE hello --name --help          → help, exit 0     (expected: name = "--help")
EXE hello --name x -- --help     → help              (bug 7: `--` is no terminator)
EXE hello --bogus --help         → help, no error
EXE hello --name=--help          → binds             (the only way)
EXE ns --bogus --help            → error             (namespaces don't pre-scan: inconsistent)
```

Cause: `Emit.Runner.cs:72-79` scans every argument for `--help`/`-h` before parsing.

**Expected (clap/cobra):** both bind `--help` as the value of `--name` when it follows a value-taking flag, and everything after `--` is positional.

Dev note: garden variety bug, fix.

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

**Expected (clap/cobra):** both accept `-vn foo` and `-nfoo`. Create this as a separate PR: support combined short flags.

### 83. Command names are case-insensitive but flags aren't, and the unknown `-V` message depends on position

`HELLO` routes to `hello`, but `--NAME` is an unknown option. `-V` before the command gives `unknown short option '-V'`; after it, `missing value for short flag '-V'`, which is misleading.

**Expected (clap/cobra):** both are case-sensitive for commands and flags by default. Pick one rule and one message.

Dev note: add StringComparer to parser options, default to StringComparer.Ordinal. Ensure parser options are documented, including the dateonly and timeonly parts.

### 89. `[Range]` and `[AllowedValues]` on namespace or global options members are shown in help but never checked

```
Bugs.exe bug89 --retries 99 show    → retries:99, exit 0      (the member has [Range(1, 10)])
Bugs.exe bug89 --mode bogus show    → accepted                (the member has [AllowedValues("fast", "slow")])
Bugs.exe bug89 show --retries 99    → accepted in either position
```

`bug89 show --help` prints `[range: 1-10]` and `[allowed: fast|slow]`, so the user is told a rule that isn't enforced. The same attributes on a method parameter are checked. Reported by session `argh-a5` while building `range-unbounded` ([#90](https://github.com/nullean/argh/pull/90)); reproduced in `bugs/bug89.cs`.

**Expected (clap/cobra):** clap runs an argument's value parser wherever the argument is defined, including `global(true)` arguments. Validate options members exactly like parameters.

### By design, after checking

- `--version` only works at the root (`hello --version` is an unknown option). clap accepts `--version` only where it's defined, which is the root by default, and cobra reports `unknown flag`, so this matches both.
