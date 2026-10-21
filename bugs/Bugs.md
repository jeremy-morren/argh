# Bugs in Argh

This is the bug report for `main` at `45954b6` (0.21.0), kept with a scratch project that exercises each bug. **New bugs go here, on the `bugs` branch:** add a numbered section to this file (next free number) and a `bugNN.cs` that exercises it; a bug fixed on a branch is removed from this file and its scratch file deleted, and the note under Background lists it. The five groups below are the proposed issues; each bug keeps the number the report gave it, because entries refer to each other by number.

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

## Proposed issues

### Issue A: Parsing and binding

Wrong results when reading argv and environment variables. The parser is generated, but every bug is a runtime result the user sees.

| # | Bug | Scratch file |
|---|---|---|
| 1 | An environment variable overrides a global or namespace flag given before the command | `bug01.cs` |
| 2 | `--flag=false` on a plain `bool` sets it to `true` | `bug02.cs` |
| 3 | A global enum flag, or a namespace option of any special type, after the command accepts any value silently | `bug03.cs` |
| 4 | Extra positional arguments are ignored | `bug04.cs` |
| 5 | An empty value for a required `FileInfo` or `DirectoryInfo` crashes with a stack trace | `bug05.cs` |
| 6 | Boolean environment variables are parsed case-sensitively | `bug06.cs` |
| 7 | `--` isn't a terminator, so negative numbers can't be positionals | `bug07.cs` |
| 8 | `bool?` with an unparseable value becomes `true` | `bug08.cs` |
| 9 | A required separator collection accepts zero items | `bug09.cs` |
| 19 | `[Env]` on a positional argument is shown in help but never read | `bug19.cs` |
| 20 | `params string[]` is treated as required | `bug20.cs` |
| 67 | A required namespace option is only accepted before the command | `bug67.cs` |
| 71 | `float`, `double` and `decimal` accept thousands separators, so `1,5` parses as `15` | `bug71.cs` |
| 73 | A required `Uri` binds empty, whitespace and garbage values as relative URIs | `bug73.cs` |
| 74 | `--help` anywhere in the arguments wins, so it can't be passed as a value | `bug74.cs` |
| 75 | A repeated scalar flag silently takes the last value | `bug75.cs` |
| 76 | A bare number is a `TimeSpan` in days, and the compact duration format is undocumented and incomplete | `bug76.cs` |
| 81 | Combined short flags (`-vn foo`, `-nfoo`) are rejected, and the limitation isn't documented | `bug81.cs` |
| 83 | Command names are case-insensitive but flags aren't; the unknown `-V` message depends on position | `bug83.cs` |

### Issue B: Generator: uncompilable output and missing diagnostics

Handler shapes that make the generated `ArghGenerated.g.cs` fail to compile, with no Argh diagnostic, plus diagnostics that are defined but never fire.

| # | Bug | Scratch file |
|---|---|---|
| 10 | An enum parameter with a non-zero default doesn't compile (open issue #75) | `bug10.cs` (build break, `#if BUG10`) |
| 11 | `float`, `decimal`, `char`, `NaN` and `Infinity` defaults don't compile | `bug11.cs` (build break, `#if BUG11`) |
| 12 | Unsupported scalar types are treated as `string` and don't compile, with no diagnostic | `bug12.cs` (build break, `#if BUG12`) |
| 14 | A command and a namespace with the same name don't compile, with no diagnostic | `bug14.cs` (build break, `#if BUG14`) |
| 16 | A cross-assembly `[AsParameters]` DTO with `required` members doesn't compile | `bug16.cs` + `lib/bug16.cs` (build break, `#if BUG16`) |
| 17 | A nullable enum parameter loses its nullability | `bug17.cs` |
| 18 | Duplicate CLI names created by `<param>` overrides aren't diagnosed; both parameters bind | `bug18.cs` |
| 21 | Literal command names aren't validated (`--help`, `-x`, `has space`, `""`) | `bug21.cs` (build break, `#if BUG21`) |
| 55 | Generator errors don't halt emission, so compiler errors follow every AGH error | `bug55.cs` (build break, `#if BUG55`) |
| 58 | A `[Range(typeof(decimal), "1", "2")]` attribute doesn't compile | `bug58.cs` (build break, `#if BUG58`) |
| 59 | A namespace name containing `{` or `}` doesn't compile | `bug59.cs` (build break, `#if BUG59`) |
| 60 | `__schema`, `__complete`, `__completion` and `__argh_root` can be mapped as commands; AGH0013 never fires | `bug60.cs` (build break, `#if BUG60`) |
| 61 | `[NamespaceSegment]` is silently ignored by `MapNamespace<T>("literal")`; AGH0018 never fires | `bug61.cs` |
| 68 | A non-constant initializer on an options property doesn't make it optional | `bug68.cs` |
| 70 | A positional `bool` parameter doesn't compile | `bug70.cs` (build break, `#if BUG70`) |
| 82 | AGH0003 fires when injected options precede an `[Argument]` parameter | `bug82.cs` (build break, `#if BUG82`) |
| 85 | The incremental generator's cache never hits, so every edit re-runs the whole emit | `bug85.cs` (comment only) |

### Issue C: Runtime, dispatch and hosting

Registration, lambda dispatch, DI lifetimes, cancellation and error reporting.

| # | Bug | Scratch file |
|---|---|---|
| 22 | `MapScoped`, `MapSingleton` and `MapTransient` register nothing | `hosted/bug22.cs` |
| 23 | Lambda commands inside `MapNamespace` crash or run the wrong handler | `bug23.cs` |
| 24 | `ValueTask` lambdas aren't awaited | `bug24.cs` |
| 25 | Scoped handler types are resolved from the root provider | `hosted/bug25.cs` |
| 26 | Ctrl+C prints a stack trace with generated-file paths and exits 1 | `bug26.cs` |
| 27 | `CommandContext.CommandPath` and `CommandName` leak `__argh_root` | `bug27.cs` |
| 28 | Namespace-option parse errors always hint at root help | `bug28.cs` |
| 29 | An unknown flag in a namespace without a root handler is reported as a command typo | `bug29.cs` |
| 30 | ANSI colour on stderr is decided by whether stdout is redirected | `bug30.cs` |
| 77 | The `Console.CancelKeyPress` handler is never removed and captures a disposed token source | `bug77.cs` |
| 78 | Hosting suppresses logging for any run that has `--help` or `--version` anywhere in argv | `hosted/bug78.cs` |
| 79 | Hosting's `Environment.Exit` skips `StopAsync` and disposal of other services | `hosted/bug79.cs` |
| 80 | A base type's validator is skipped when the derived type has one | `bug80.cs` |

### Issue D: Help and XML docs

What `--help` prints: rendering of XML doc tags, defaults, layout and colour.

| # | Bug | Scratch file |
|---|---|---|
| 42 | Multi-line `<param>` text breaks the option table | `bug42.cs` |
| 43 | Inline styles leak to the rest of the line in colour mode | `bug43.cs` |
| 44 | Blank lines and code styling in remarks differ between colour and plain mode | `bug44.cs` |
| 45 | String defaults show C# escape sequences | `bug45.cs` |
| 46 | Single-line `Notes:` skips the remarks transforms that multi-line `Notes:` applies | `bug46.cs` |
| 47 | `<see cref="M(System.String)"/>` renders as `String)` | `bug47.cs` |
| 48 | Element-only `<para>` content is pretty-printed with newlines and indent | `bug48.cs` |
| 49 | `<br/>` is dropped, joining words | `bug49.cs` |
| 50 | Multi-line `Notes:` starts with a blank line; two blank lines before it without `Options:` | `bug50.cs` |
| 51 | Numbered lists flatten to `-`; `<listheader>` and `<term>` are dropped | `bug51.cs` |
| 52 | Trailing spaces in `Usage:` and listing rows | `bug52.cs` |
| 54 | `<inheritdoc/>` produces no docs and loses the inherited short options, with no diagnostic | `bug54.cs` |
| 56 | Handlers in a referenced assembly get no documentation at all | `bug56.cs` + `lib/bug56.cs` |
| 57 | Enum member docs are never shown, and enum defaults print as `[default: 0]` | `bug57.cs` |
| 64 | A very long command name widens the listing column without a cap, squeezing every summary to 12 characters | `bug64.cs` (build break, `#if BUG64`) |
| 69 | Help pads columns by UTF-16 length, so CJK command names misalign | `bug69.cs` |

### Issue E: Completion and `__schema`

The shell scripts, the `__complete` engine, and the cli-schema JSON that agents read.

| # | Bug | Scratch file |
|---|---|---|
| 33 | Hidden commands, namespaces and flags are completed | `bug33.cs` |
| 34 | `__schema` advertises command aliases that don't route or complete | `bug34.cs` |
| 35 | `timeSpanRange` constraint values carry C# quotes | `bug35.cs` |
| 36 | `defaultValue: "default"` for nullable value types | `bug36.cs` |
| 37 | `__argh_root` leaks into schema usage strings | `bug37.cs` |
| 38 | Root and namespace default handlers are missing from the schema when registered by alias | `bug38.cs` (build break, `#if BUG38`) |
| 39 | `[Hidden(Schema = true)]` on a namespace is silently dropped | `bug39.cs` |
| 40 | Global and namespace options are duplicated into every command's `parameters` | `bug40.cs` |
| 41 | A committed `--flag=value` breaks bash completion | `bug41.cs` |
| 62 | Completion lists flags after `--`, an unknown flag or a positional inside a command, but not at the root or in a namespace | `bug62.cs` |
| 63 | Enum collections lose their member names in `__schema` | `bug63.cs` |

## Background

Found by reviewing `main` at `45954b6` (0.21.0) in a fresh clone, on Windows 11 with .NET 10. Every bug below was reproduced: by running the test fixture host `Nullean.Argh.Tests.CliHost` (built in Release), by building small probe projects that reference the framework, or both. Paths are relative to the repository root; line numbers are from `45954b6`.

The expected behaviour for each bug is what **clap** (Rust, v4.6.7) and **cobra** (Go, v1.10.2, with pflag v1.0.10) do, checked against their source. Where they differ, both are given. Source links are pinned to those release tags.

Repro commands use `EXE` for `.artifacts/bin/Nullean.Argh.Tests.CliHost/release/Nullean.Argh.Tests.CliHost.exe` with `NO_COLOR=1` set. The fixture's middleware output (`[tests:middleware:...]`) is left out.


Bugs fixed on the open branches are removed: 53 (en dash in help, and no Windows CI) and 65 (non-ASCII text in `__schema`) on `windows-encoding`, 13 (parameters named like generated locals), 15 (`fooBar` and `foo_bar` sharing a local) and 66 (`Uri` and `Uri?` in one method) on `timeonly`, the `allowed`/`denied` part of 35 on `enum-completion`, and 31 (fish), 32 (zsh), 86 (bash 3.2 `mapfile`) and 87 (CRLF candidates) on `completion-test` (commit `92d5c2d`, every real-shell matrix entry passing in [run 39530303045](https://github.com/jeremy-morren/argh/actions/runs/39530303045)). On `parse-culture` (stacked on [#87](https://github.com/nullean/argh/pull/87), PR pending): 72 (the culture-sensitive trailing re-parse; every numeric parse now uses the configured `ParserOptions`, invariant by default, with `Nullable<T>` lifted to `T`), and an unlisted bug where `[ArgumentParser]` on a `T?` parameter generated `Point??` and didn't compile (it now uses `IArgumentParser<T?>` if implemented, else `IArgumentParser<T>`). The remaining bugs keep their numbers, because entries refer to each other by number.

## Summary

| # | Bug | Area | Severity |
|---|---|---|---|
| 1 | An environment variable overrides a global or namespace flag given before the command | Parsing | Wrong result |
| 2 | `--flag=false` on a plain `bool` sets it to `true` | Parsing | Wrong result |
| 3 | A global enum flag, or a namespace option of any special type, after the command accepts any value silently | Parsing | Wrong result |
| 4 | Extra positional arguments are ignored | Parsing | Wrong result |
| 5 | An empty value for a required `FileInfo` or `DirectoryInfo` crashes with a stack trace | Parsing | Crash |
| 6 | Boolean environment variables are parsed case-sensitively | Parsing | Wrong result |
| 7 | `--` isn't a terminator, so negative numbers can't be positionals | Parsing | Misleading message |
| 8 | `bool?` with an unparseable value becomes `true` | Parsing | Minor |
| 9 | A required separator collection accepts zero items | Parsing | Minor |
| 10 | An enum parameter with a non-zero default doesn't compile (open issue #75) | Generator | Build break |
| 11 | `float`, `decimal`, `char`, `NaN` and `Infinity` defaults don't compile | Generator | Build break |
| 12 | Unsupported scalar types are treated as `string` and don't compile, with no diagnostic | Generator | Build break |
| 14 | A command and a namespace with the same name don't compile, with no diagnostic | Generator | Build break |
| 16 | A cross-assembly `[AsParameters]` DTO with `required` members doesn't compile | Generator | Build break |
| 17 | A nullable enum parameter loses its nullability | Generator | Wrong result |
| 18 | Duplicate CLI names created by `<param>` overrides aren't diagnosed; both parameters bind | Generator | Wrong result |
| 19 | `[Env]` on a positional argument is shown in help but never read | Generator | Wrong result |
| 20 | `params string[]` is treated as required | Generator | Minor |
| 21 | Literal command names aren't validated (`--help`, `-x`, `has space`, `""`) | Generator | Minor |
| 22 | `MapScoped`, `MapSingleton` and `MapTransient` register nothing | Runtime | Wrong result |
| 23 | Lambda commands inside `MapNamespace` crash or run the wrong handler | Runtime | Crash |
| 24 | `ValueTask` lambdas aren't awaited | Runtime | Wrong result |
| 25 | Scoped handler types are resolved from the root provider | Hosting | Crash in Development |
| 26 | Ctrl+C prints a stack trace with generated-file paths and exits 1 | Runtime | Misleading message |
| 27 | `CommandContext.CommandPath` and `CommandName` leak `__argh_root` | Runtime | Minor |
| 28 | Namespace-option parse errors always hint at root help | Runtime | Misleading message |
| 29 | An unknown flag in a namespace without a root handler is reported as a command typo | Runtime | Misleading message |
| 30 | ANSI colour on stderr is decided by whether stdout is redirected | Runtime | Minor |
| 33 | Hidden commands, namespaces and flags are completed | Completion | Wrong result |
| 34 | `__schema` advertises command aliases that don't route or complete | Schema | Wrong result |
| 35 | `timeSpanRange` constraint values carry C# quotes | Schema | Wrong result |
| 36 | `defaultValue: "default"` for nullable value types | Schema, Help | Wrong result |
| 37 | `__argh_root` leaks into schema usage strings | Schema | Minor |
| 38 | Root and namespace default handlers are missing from the schema when registered by alias | Schema | Wrong result |
| 39 | `[Hidden(Schema = true)]` on a namespace is silently dropped | Schema | Minor |
| 40 | Global and namespace options are duplicated into every command's `parameters` | Schema | Design |
| 41 | A committed `--flag=value` breaks bash completion | Completion | Minor |
| 42 | Multi-line `<param>` text breaks the option table | Help | Wrong output |
| 43 | Inline styles leak to the rest of the line in colour mode | Help | Wrong output |
| 44 | Blank lines and code styling in remarks differ between colour and plain mode | Help | Wrong output |
| 45 | String defaults show C# escape sequences | Help | Wrong output |
| 46 | Single-line `Notes:` skips the remarks transforms that multi-line `Notes:` applies | Help | Wrong output |
| 47 | `<see cref="M(System.String)"/>` renders as `String)` | Help | Wrong output |
| 48 | Element-only `<para>` content is pretty-printed with newlines and indent | Help | Wrong output |
| 49 | `<br/>` is dropped, joining words | Help | Wrong output |
| 50 | Multi-line `Notes:` starts with a blank line; two blank lines before it without `Options:` | Help | Cosmetic |
| 51 | Numbered lists flatten to `-`; `<listheader>` and `<term>` are dropped | Help | Wrong output |
| 52 | Trailing spaces in `Usage:` and listing rows | Help | Cosmetic |
| 54 | `<inheritdoc/>` produces no docs and loses the inherited short options, with no diagnostic | Generator, Help | Wrong output |
| 55 | Generator errors don't halt emission, so compiler errors follow every AGH error | Generator | Misleading message |
| 56 | Handlers in a referenced assembly get no documentation at all | Generator, Help | Wrong output |
| 57 | Enum member docs are never shown, and enum defaults print as `[default: 0]` | Help | Wrong output |
| 58 | A `[Range(typeof(decimal), "1", "2")]` attribute doesn't compile | Generator | Build break |
| 59 | A namespace name containing `{` or `}` doesn't compile | Generator | Build break |
| 60 | `__schema`, `__complete`, `__completion` and `__argh_root` can be mapped as commands; AGH0013 never fires | Generator | Wrong result |
| 61 | `[NamespaceSegment]` is silently ignored by `MapNamespace<T>("literal")`; AGH0018 never fires | Generator | Minor |
| 62 | Completion lists flags after `--`, an unknown flag or a positional inside a command, but not at the root or in a namespace | Completion | Wrong result |
| 63 | Enum collections lose their member names in `__schema` | Schema | Wrong result |
| 64 | A very long command name widens the listing column without a cap, squeezing every summary to 12 characters | Help | Cosmetic |
| 67 | A required namespace option is only accepted before the command | Parsing | Wrong result |
| 68 | A non-constant initializer on an options property doesn't make it optional | Generator | Wrong result |
| 69 | Help pads columns by UTF-16 length, so CJK command names misalign | Help | Cosmetic |
| 70 | A positional `bool` parameter doesn't compile | Generator | Build break |
| 71 | `float`, `double` and `decimal` accept thousands separators, so `1,5` parses as `15` | Parsing | Wrong result |
| 73 | A required `Uri` binds empty, whitespace and garbage values as relative URIs | Parsing | Wrong result |
| 74 | `--help` anywhere in the arguments wins, so it can't be passed as a value | Parsing | Wrong result |
| 75 | A repeated scalar flag silently takes the last value | Parsing | Design |
| 76 | A bare number is a `TimeSpan` in days, and the compact duration format is undocumented and incomplete | Parsing | Misleading |
| 77 | The `Console.CancelKeyPress` handler is never removed and captures a disposed token source | Runtime | Crash |
| 78 | Hosting suppresses logging for any run that has `--help` or `--version` anywhere in argv | Hosting | Wrong result |
| 79 | Hosting's `Environment.Exit` skips `StopAsync` and disposal of other services | Hosting | Minor |
| 80 | A base type's validator is skipped when the derived type has one | Runtime | Wrong result |
| 81 | Combined short flags (`-vn foo`, `-nfoo`) are rejected, and the limitation isn't documented | Parsing | Minor |
| 82 | AGH0003 fires when injected options precede an `[Argument]` parameter | Generator | Wrong diagnostic |
| 83 | Command names are case-insensitive but flags aren't; the unknown `-V` message depends on position | Parsing | Minor |
| 84 | Moved to feature F1: `HashSet<T>` and the other collection shapes | Generator | See Feature requests |
| 85 | The incremental generator's cache never hits, so every edit re-runs the whole emit | Generator | Performance |

## Parsing and binding

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

### 4. Extra positional arguments are ignored

```
EXE hello --name x extra          → ok:x, exit 0
EXE count-cmd --count 1 foo bar   → count:1, exit 0
EXE build extra                   → the root default command runs
```

Cause: `Emit.Runner.cs:279` adds every non-dash token to `positionals`, and nothing after binding (`:358-397`) checks `positionals.Count` against the declared positionals.

**Expected (clap/cobra):** clap, whose positionals are declared like Argh's, rejects them: `error: unexpected argument 'extra' found`, exit 2 ([parser.rs#L574-L580](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L574-L580)). cobra's default `legacyArgs` errors with `unknown command "extra" for "app"` only on a root that has subcommands; a subcommand or leaf accepts extras unless `cobra.NoArgs` or `ExactArgs` is set ([args.go#L28-L47](https://github.com/spf13/cobra/blob/v1.10.2/args.go#L28-L47)). Follow clap.

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

### 6. Boolean environment variables are parsed case-sensitively

```
MY_APP_VERBOSE=False EXE env-bool
env-verbose:True                  (expected: False)
```

`FALSE` and `no` give `True` too.

Cause: `Emit.Runner.cs:298` tests `EnvRaw != "0" && EnvRaw != "false"`; the same at `Emit.Parsing.cs:404`.

**Expected (clap/cobra):** case-insensitive. clap's `FalseyValueParser` and `BoolishValueParser` lowercase the value and match `y`, `yes`, `t`, `true`, `on`, `1` or `n`, `no`, `f`, `false`, `off`, `0` ([str_to_bool.rs#L1-L22](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/util/str_to_bool.rs#L1-L22)); `Falsey` treats an empty value as false and anything unknown as true, `Boolish` rejects unknown values. Go's `strconv.ParseBool` accepts `1`, `t`, `T`, `TRUE`, `true`, `True`, `0`, `f`, `F`, `FALSE`, `false`, `False`.

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

### 8. `bool?` with an unparseable value becomes `true`

```
EXE dry-run-cmd --dry-run=bogus   → dry-run:true
EXE dry-run-cmd --dry-run=no      → dry-run:true
```

Cause: `ParseNullableBool` in `Emit.Dispatch.cs:537-539` returns `fromYesSwitch` when `bool.TryParse` fails.

**Expected (clap/cobra):** an error. clap: `error: invalid value 'bogus' for '--dry-run [<DRY_RUN>]'` with `[possible values: true, false]` ([value_parser.rs#L1706-L1715](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/builder/value_parser.rs#L1706-L1715)). pflag: `invalid argument "bogus" for "--dry-run" flag: strconv.ParseBool: parsing "bogus": invalid syntax` ([errors.go#L117](https://github.com/spf13/pflag/blob/v1.0.10/errors.go#L117)).

### 9. A required separator collection accepts zero items

```
EXE schema-separator-list --ids=  → ids:   exit 0
EXE schema-separator-list --ids ,,  → ids:   exit 0
```

The flag is `[required]` in help. The repeatable-flag path checks the item count (`Emit.Parsing.cs:627-635`); the separator path only checks that the flag is present (`:568-603`) and skips empty parts at `:587`.

**Expected (clap/cobra):** both keep the empty parts as values and let the element parser reject them. pflag's `IntSlice` gives `invalid argument "" for "--ids" flag: strconv.Atoi: parsing "": invalid syntax` ([int_slice.go#L22-L30](https://github.com/spf13/pflag/blob/v1.0.10/int_slice.go#L22-L30)). clap splits `--ids=` into `[""]` and `--ids ,,` into three empty strings ([parser.rs#L1178-L1195](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L1178-L1195)), and an integer parser reports `cannot parse integer from empty string`. Follow them: don't skip empty parts silently.

## Generator

Each of these was reproduced with a small console project referencing the framework. "Doesn't compile" means the generated `ArghGenerated.g.cs` fails to compile, with no Argh diagnostic to explain why.

### 10. An enum parameter with a non-zero default doesn't compile

```csharp
public enum Level { Low = 0, Mid = 5, High = 10 }
static void Cmd(Level level = Level.High) { }
```

Generated: `global::Level level = 10;` → four `CS0266: Cannot implicitly convert type 'int' to 'Level'`. `Level.Low` compiles only because `0` converts. This is open issue [#75](https://github.com/nullean/argh/issues/75).

Cause: `TryGetDefaultLiteral` in `src/Nullean.Argh.Generator/CliParserGenerator.ParameterModel.cs:977-996` formats `ExplicitDefaultValue` (the underlying integer) through `IFormattable`. Options-type properties go through `QualifyOptionsEnumDefaultLiteral`, which looks the member up; method parameters don't.

**Fix:** never emit a bare number for a typed default. An enum default is emitted as its qualified member, `global::Level.High`, or as a cast, `(global::Level)10`, when the value isn't a named member (flags combinations). The same rule applies to every constant the generator writes into typed code (see bug 11).

### 11. `float`, `decimal`, `char`, `NaN` and `Infinity` defaults don't compile

| Declared | Generated | Error |
|---|---|---|
| `decimal r = 1.5m` | `decimal r = 1.5;` | CS0664 |
| `float f = 1.5f` | `float f = 1.5;` | CS0664 |
| `float? f = 2.5f` | `float? f = 2.5;` | CS0266 |
| `double d = double.NaN` | `double d = NaN;` | CS0103 |
| `double d = double.PositiveInfinity` | `double d = Infinity;` | CS0103 |
| `char c = 'x'` | `string? c = 'x';` | CS0029 |

Same cause as bug 10: `TryGetDefaultLiteral` drops the type suffix. `long.MinValue`, `1e-7`, and strings with quotes or braces are fine.

Options-type properties have the same problem through a different path: `decimal Money { get; set; } = 1.25m`, `double Ratio = 0.5` and `float Scale = 0.5f` on a global options class generate `decimal money = 1.25;` and fail with CS0664. `TryFormatInitializerOperation` (`src/Nullean.Argh.Generator/CliParserGenerator.SymbolReaders.cs:403`) formats the literal's constant value without its `m`/`f` suffix, and `Emit.Runner.cs:387` emits it.

**Fix:** every constant the generator emits must carry its C# type, either as the literal's suffix or as an explicit type, so the compiler never has to guess: `1.5m` for `decimal`, `1.5f` for `float`, `1.5d` for `double`, `5L`/`5UL`/`5U` for the wide integers, `'x'` (escaped) for `char`, and the named constants `double.NaN`, `double.PositiveInfinity`, `float.NegativeInfinity`, `decimal.MaxValue` and so on, which have no literal form. Where no suffix exists, write the type: `(global::Level)10`, `(byte)200`. Use the `R` round-trip format for floating-point values so the emitted literal equals the declared one. This covers both paths, `TryGetDefaultLiteral` (method parameters) and `TryFormatInitializerOperation` (options properties), and the `[Range]` bounds of bug 58, which are spliced into comparisons the same way.

### 12. Unsupported scalar types are treated as `string` and don't compile

A parameter of type `char`, `byte`, `sbyte`, `short`, `ushort`, `uint`, `ulong`, `Guid`, or a user struct implementing `IParsable<T>` without `[ArgumentParser]`, produces dozens of CS0029/CS1503 errors, for example `string? b = 18446744073709551615;`.

Cause: `GetSimpleTypeName` in `ParameterModel.cs:910-951` falls through to `return "string"`, and `ClassifyScalar` (`:824`) has no "unsupported type" diagnostic.
Unrecognised collection shapes (`IList<T>`, `HashSet<T>`, `ImmutableArray<T>`, dictionaries, ...) fall through the same way and fail with CS1503; supporting them is feature F1/F2 below, but the missing diagnostic is this bug.

**Expected:** either support the type or report an Argh diagnostic naming the parameter and type. clap's derive macro fails at compile time with a message naming the type.


**Partly fixed on `parsable-types`** (stacked on `parse-culture`, PR pending): on .NET 7 and later, any type implementing `IParsable<T>` binds automatically, which covers `Guid`, `char`, `byte`, `sbyte`, `short`, `ushort`, `uint`, `ulong`, `Half`, `Int128`, `BigInteger` and user `IParsable`/`ISpanParsable` types (including explicit implementations), as plain, nullable, collection, DTO, options and env values. Still broken: those types on target frameworks before .NET 7, where `IParsable<T>` doesn't exist, and any type that is neither built in nor `IParsable`, which still fails with CS1503 and no diagnostic.
### 14. A command and a namespace with the same name don't compile

```csharp
app.Map("storage", H.Plain);
app.MapNamespace("storage", "ns", g => g.Map("x", H.Plain));
```

Generated: `CS0152: duplicate case "storage"`. No diagnostic: AGH0027 (`Analysis.cs:475`) compares commands only with commands, and AGH0018 only covers segment conflicts from attributes or XML.

**Expected (clap/cobra):** both reject duplicate subcommand names at build time (clap panics in debug asserts with the name; cobra's `AddCommand` doesn't check, but the second never routes). A diagnostic is the right answer here.

### 16. A cross-assembly `[AsParameters]` DTO with `required` members doesn't compile

```csharp
// in another project
public sealed class ExtReq { public required string Name { get; init; } }
// handler
static void Cmd([AsParameters] ExtReq r) { }
```

Generated: `var __rt_default_P07Lib_ExtReq = new global::P07Lib.ExtReq();` → three `CS9035: Required member 'ExtReq.Name' must be set`. A `required` member in the same assembly works.

Cause: `isCrossAssemblyDefault = defaultValueLiteral is null && DeclaringSyntaxReferences.IsEmpty` (`ParameterModel.cs:316, 604`) ignores `IPropertySymbol.IsRequired`; the emit at `Emit.Runner.cs:636-645` then instantiates the type to read its defaults.

### 17. A nullable enum parameter loses its nullability

```csharp
static void NEnum(Level? level = null) => Console.WriteLine(level?.ToString() ?? "null");
```

Running `nenum` with no arguments prints `Low`, not `null`. Generated: `global::Level level = default;`. The schema reports `defaultValue: "default"` (bug 36).

Cause: `IsNullableAnnotated` is set only on the options-property paths (`ParameterModel.cs:349, 410, 639`), never on the method-parameter path (`:253`, `:495`), so `GetCSharpCliType` (`Emit.Runner.cs:797-803`) returns the non-nullable enum because `DefaultValueLiteral` is `"default"`.

**Expected (clap/cobra):** an optional value that isn't given is absent. clap's `Option<Level>` is `None`; Argh should bind `null`.

### 18. Duplicate CLI names created by `<param>` overrides aren't diagnosed

```csharp
/// <param name="name">--label, The name.</param>
static void Cmd(string name, string label) { }
```

This compiles. Help lists `--label` twice, `--label A` binds both parameters, and `--name` is "unknown option". The same happens for an alias such as `--nm` given to two parameters.

Cause: `ReportDuplicateCliNamesAcc` runs on the raw models (`CliParserGenerator.CommandModel.cs:68, 103`), but the explicit names and aliases from `<param>` are applied later by `ApplyParamDocumentation` (`:82, 117`; body `:338-375`), so AGH0007 never sees them.

**Expected (clap/cobra):** both reject two arguments with the same long name at build time.

### 19. `[Env]` on a positional argument is shown in help but never read

```csharp
static void EnvPos([Argument, Env("HUNT_NAME")] string name) { }
```

`HUNT_NAME=x app env-pos` → `Error: missing required argument <name>`, while help prints `<name>  [env: HUNT_NAME]`.

Cause: `Emit.Runner.cs:289` skips positionals (`if (p.Kind == ParameterKind.Positional) continue;`) in env binding; the help emitter still appends the tag. Either bind it or report a diagnostic.

**Expected (clap/cobra):** clap's `env` works on positionals the same as on options.

### 20. `params string[]` is treated as required

A handler with `params string[] items`, run with no arguments → `missing required flag --items`.

Cause: `ComputeRequired` (`ParameterModel.cs:953`) ignores `IParameterSymbol.IsParams`.

### 21. Literal command names aren't validated

`app.Map("--help", …)`, `"-x"`, `"has space"` and `""` all compile. The `--help` command is unreachable because root help wins; `has space` is listed in help but can't be typed as one token. No diagnostic covers this.

**Expected (clap/cobra):** clap's debug asserts reject such names; cobra doesn't check. A diagnostic is the right answer.

## Runtime and hosting

### 22. `MapScoped`, `MapSingleton` and `MapTransient` register nothing

A hosted app that registers its commands only with `b.MapScoped<ScopedCommands>()` prints `No commands are registered.` for every command (exit 2). `Map<T>(ServiceLifetime.Scoped)` works.

Cause: the generator scans invocations named `Map`, `MapNamespace`, `MapRoot`, `MapAndRootAlias`, `UseGlobalOptions`, `UseNamespaceOptions`, `UseMiddleware`, `UseCliDescription`, `UseSchemaVersion` and `DocumentEnvironmentVariables` (`src/Nullean.Argh.Generator/CliParserGenerator.cs:54-64`, `CliParserGenerator.Analysis.cs:61-82`). The hosting builder's lifetime variants (`src/Nullean.Argh.Hosting/ArghHostingBuilder.cs:46-56`) aren't in the list, although the docs lead with them (`docs/hosting/dependency-injection.md:30, 42`, `docs/getting-started/hosted-app.md:56`, `docs/features/aot.md:21`).

### 23. Lambda commands inside `MapNamespace` crash or run the wrong handler

```csharp
app.MapNamespace("ns", "x", g =>
{
    g.Map("lam", (string s) => Console.WriteLine("ns-lam " + s));
    g.MapRoot(() => Console.WriteLine("ns-root-default"));
});
```

`ns lam --s x` → `NullReferenceException`, exit 1. `ns` → `InvalidCastException: Unable to cast Action<string> to Action`, because it picked up the app-level `MapRoot` lambda; with matching signatures it would silently run the wrong handler.

Cause: `ArghApp.Map`/`MapRoot` store lambdas under `"ns/name"` and `"ns/__argh_root"` (`src/Nullean.Argh.Core/ArghApp.cs:25-29, 51-57`), but the generator computes `LambdaStorageKey` with an empty prefix (`Analysis.cs:1225, 1331`), and the re-prefix step only rewrites `RoutePrefix` and `RunMethodName` (`:576-583, 605-609`). The generated code calls `GetRegisteredLambda("lam")`.

### 24. `ValueTask` lambdas aren't awaited

```csharp
app.Map("vt", async ValueTask (string msg) => { await Task.Delay(200); Console.WriteLine("done"); });
```

`vt --msg m` prints nothing and exits 0 before the lambda finishes. The build emits CS4014 in the generated file. Method-group handlers returning `ValueTask` are awaited correctly.

Cause: `EmitLambdaInvocation` (`Emit.Runner.cs:1775-1800`) handles `Task`, `Task<T>`, `Task<int>`, `ValueTask<int>` and `int`; everything else becomes `__lambdaDelegate(args);`.

### 25. Scoped handler types are resolved from the root provider

With `b.Map<ScopedCommands>(ServiceLifetime.Scoped)` and `AddScoped<ScopedDep>()`, `DOTNET_ENVIRONMENT=Development` gives `InvalidOperationException: Cannot resolve scoped service 'ScopedCommands' from root provider`, exit 1. In Production it runs, but `ScopedDep.Dispose` never fires. The docs promise "Resolved per command invocation".

Cause: the generated code calls `ArghServices.ServiceProvider?.GetService(typeof(T))` (`Emit.Runner.cs:449-460`, `Emit.Dispatch.cs:390-397`), and Hosting assigns the root provider (`src/Nullean.Argh.Hosting/ArghHostingCliService.cs:38`). No `IServiceScope` is created.

### 26. Ctrl+C prints a stack trace and exits 1

A handler that does `await Task.Delay(5000, ct)`, interrupted with Ctrl+C, prints a 12-line `OperationCanceledException` stack trace including `ArghGenerated.g.cs:line 2823`, and exits 1. The token itself works: both console Ctrl+C and `IHostApplicationLifetime.StopApplication()` cancel it. Any unhandled handler exception prints the same kind of trace.

Cause: `RunWithCancellationAsync` catches `Exception` and writes `ex.ToString()` (`Emit.Dispatch.cs:347-351`).

**Expected (clap/cobra):** no stack trace. A cobra `RunE` that returns `context.Canceled` prints `Error: context canceled` to stderr and exits 1 ([command.go#L1150-L1167](https://github.com/spf13/cobra/blob/v1.10.2/command.go#L1150-L1167)). Rust installs no SIGINT handler, so the process dies and the shell reports 130. Print one line and use a non-zero exit code; 130 matches the shell convention.

### 27. `CommandContext.CommandPath` and `CommandName` leak `__argh_root`

Middleware sees `path='__argh_root'` for a `MapRoot` handler and `path='ns/__argh_root'` for a namespace root. The `CommandName` docs say an empty path gives `string.Empty`.

Cause: `EmitCommandPathLiteral` (`Emit.Runner.cs:597-611`) appends `cmd.CommandName`, which is `RootDefaultInternalCommandName` (`Analysis.cs:1298`).

### 28. Namespace-option parse errors always hint at root help

```
EXE storage --bogus
Error: unknown option '--bogus'.

Run 'Nullean.Argh.Tests.CliHost --help' for usage.      (expected: ... storage --help)
```

Also `storage --prefix` (missing value) and `env-ns --bogus key`.

Cause: `Emit.Parsing.cs:43-48` hard-codes `Run '<app> --help'`.

**Expected (clap/cobra):** the hint points at the command that failed. clap prints the usage of the subcommand being parsed, then `For more information, try '--help'.` ([parser.rs#L572-L580](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/parser/parser.rs#L572-L580), [format.rs#L463](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/error/format.rs#L463)); cobra prints the failing subcommand's `UsageString()` after the error ([command.go#L1159-L1167](https://github.com/spf13/cobra/blob/v1.10.2/command.go#L1159-L1167)).

### 29. An unknown flag in a namespace without a root handler is reported as a command typo

```
EXE storage blob --bogus
Error: unknown command or namespace '--bogus'.
```

It's also fuzzy-matched against command names.

Cause: `Emit.Dispatch.cs:878-890` only falls through to flag handling when a root or alias handler exists.

**Expected (clap/cobra):** `error: unexpected argument '--bogus' found` (clap, [format.rs#L358](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/error/format.rs#L358)) or `Error: unknown flag: --bogus` (cobra, [errors.go#L39-L40](https://github.com/spf13/pflag/blob/v1.0.10/errors.go#L39-L40)), never a command-name suggestion.

### 30. ANSI colour on stderr is decided by whether stdout is redirected

`UseAnsiColors` checks only `NO_COLOR` and `Console.IsOutputRedirected` (`src/Nullean.Argh.Core/Help/CliHelpFormatting.cs:13-15`), but error output and flag help go to stderr (`Emit.Dispatch.cs:311`). `app typo 2>err.txt` in a terminal writes escape codes into the file. `TERM=dumb` isn't considered.

**Expected (clap/cobra):** clap wraps stdout and stderr in separate `AutoStream`s ([fmt.rs#L36-L52](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/output/fmt.rs#L36-L52)), so each stream is coloured only when it is a terminal; `anstream` also honours `NO_COLOR`, `CLICOLOR_FORCE`, `CLICOLOR=0` and `TERM=dumb` ([auto.rs#L198-L215](https://github.com/rust-cli/anstyle/blob/d891cda/crates/anstream/src/auto.rs#L198-L215)). cobra has no colour.

## Completion and schema

Bugs 31 and 32 come from reading the templates against the shells' documented behaviour; zsh and fish aren't installed on this machine. Both were confirmed by sending the exe exactly what the script would send.

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

A namespace default handler's usage is `"Nullean.Argh.Tests.CliHost storage __argh_root"` (`Schema.cs:110` via `BuildCommandUsageSynopsisTail`). Related to bug 27.

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

## Help and XML docs

These were reproduced with a probe project whose handlers use each XML tag; colour output was captured through a ConPTY so the app saw a real console.

### 42. Multi-line `<param>` text breaks the option table

```
  -n, --name <string>  [required] The name to use.
This continues on a second line
and a third line.
  --count <int>        Count of things. 
Second para. [default: 1]
```

Cause: `FlattenParam` (`Documentation.cs:659-670`) and `Emit.Help.cs:904-905` never collapse whitespace; the summary does (`:388`).

**Expected (clap/cobra):** clap's derive macro joins the lines of a doc comment into one paragraph, with a blank line starting a new one, unless `verbatim_doc_comment` is set; cobra takes `Short`/`Long` strings as given. Collapse the whitespace, as the summary already does.

### 43. Inline styles leak to the rest of the line in colour mode

`<c>`, `<b>`, `<i>`, `<u>`, `<see langword>` and OSC 8 links end their span with the base colour (`\x1b[90m` or `37m`) instead of a reset: `^[[1mcode ^[[90mthen plain text that should not be bold.^[[m`.

Cause: `DocStyledSpan` in `CliHelpFormatting.cs:105-111` (`cont`) and `:122` (`Osc8Hyperlink`).

### 44. Blank lines and code styling in remarks differ between colour and plain mode

Plain mode prints one blank line after `Notes:` and none between paragraphs; colour mode prints two and one, plus trailing blanks. In a `<code>` block the first line is unstyled and the second is grey.

Cause: whitespace-only and multi-line text nodes are wrapped whole in SGR (`XmlDocumentationRenderer.cs:97`), so `CollapseBlankRuns` and `TrimEnd` (`:35`) and the empty-line check (`:56`) no longer see `\n\n`, and code lines glued to a trailing `\x1b[0m` skip `DocRemarksLine`.

### 45. String defaults show C# escape sequences

| Declared | Help shows |
|---|---|
| `string s = @"c:\temp\x"` | `[default: c:\\temp\\x]` |
| `"say \"hi\"\\now"` | `[default: say \"hi\"\\now]` |
| `"\""` | `[default: \]` |
| `"\t"` | `[default: \t]` |

Cause: `Emit.Help.cs:939` only trims the quotes from the `SymbolDisplay` literal. (Same root cause as the `[AllowedValues]` quoting fixed in `a1a542c`.)

**Expected (clap/cobra):** clap prints the raw value: `[default: c:\temp\x]` ([help_template.rs#L789-L807](https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/output/help_template.rs#L789-L807)). pflag prints string defaults with `%q` ([flag.go#L754-L758](https://github.com/spf13/pflag/blob/v1.0.10/flag.go#L754-L758)), so it shows Go escapes on purpose; follow clap.

### 46. Single-line `Notes:` skips the remarks transforms

Single-line remarks print `Notes:  Set outputDir and see MultiLineNotes; code and bold.`; the same text inside `<para>` prints `Set --out and see helprepro single-line-notes --out <string>; ...` with styling. The fixture's `hello --help` shows it (`See DocLambdaEcho; set name.`), and `HelpContentTests.cs:37` locks it in, contrary to `docs/features/help.md:70`.

The same applies to `<paramref>`: a single-line `<remarks>Use <paramref name="name"/> to set the name.</remarks>` prints `Use name to set the name.`; with an explicit `--full-name` override it still prints `name`; inside `<para>` it prints `--name`.

Cause: `Emit.Help.cs:112-122` prefers the plain `cmd.RemarksRendered`, built by `FlattenNodes` (`Documentation.cs:725-731`, which appends the raw name); only the multi-line path (`:128`) uses `TransformRemarksInnerXmlForHelp` (`Documentation.cs:278-284`).

### 47. `<see cref="M(System.String)"/>` renders as `String)`

`<see cref="System.Console.WriteLine(System.String)"/>` renders as `String)` in listings, summaries, `<param>` and Notes. Compiled XML-doc crefs for referenced DTOs are always in this form.

Cause: `CrefShortName` takes the last `.` before stripping `(`: `Documentation.cs:783` and `XmlDocumentationRenderer.cs:262`.

### 48. Element-only `<para>` content is pretty-printed with newlines and indent

`<para><b>Bold</b><i>Italic</i></para>` renders as four lines, `Bold`, `Italic`, `a`, `b`, each indented, because `XElement.ToString()` (`Documentation.cs:425, 317`) pretty-prints and remarks mode preserves the result.

### 49. `<br/>` is dropped, joining words

Listing: `br-summary  Line oneLine two`. Notes: `Remark oneRemark two`. `FlattenNodes` (`Documentation.cs:684-747`) has no `br` case.

### 50. Multi-line `Notes:` starts with a blank line; two blank lines before it without `Options:`

The inner XML starts with `\n` and `:35` only trims the end; `Emit.Help.cs:782` and `:801` both print a blank line when there's no `Options:` section.

### 51. Numbered lists flatten to `-`; `<listheader>` and `<term>` are dropped

`Documentation.cs:712` renders every list item as `-`; `XmlDocumentationRenderer.cs:177-187` drops `<listheader>` and `<term>` when a `<description>` exists.

### 52. Trailing spaces in `Usage:` and listing rows

`Usage: … cmd ` ends with a space when there are no usage hints (`Emit.Help.cs:719`); listing rows with no summary (`CliHelpFormatting.cs:146`) and option rows with an empty description carry trailing padding.

## Second pass: generator, help and completion

These started as suspicions and were confirmed with dedicated probe projects.

### 54. `<inheritdoc/>` produces no docs and loses the inherited short options

A handler documented with `<inheritdoc/>` (interface implementation, same-assembly base override, cross-assembly base override, or `<inheritdoc cref="..."/>`) gets no summary and no parameter descriptions, and the short options declared in the inherited `<param>` tags (`-n`, `-c`, `-v`) are lost. The root listing shows bare names. No diagnostic is reported.

Cause: Roslyn's `GetDocumentationCommentXml()` returns the literal `<inheritdoc/>` unexpanded, and the trivia fallback (`src/Nullean.Argh.Generator/CliParserGenerator.Documentation.cs:218-238`) parses the same text. Nothing resolves the referenced member. `plans/vision.md` lists `<inheritdoc>` as supported.

### 55. Generator errors don't halt emission

`CLAUDE.md` says "Errors halt code generation; warnings allow it to continue". They don't: with AGH0033 (duplicate `-v`) reported, the generated file still contains both `case 'v':` labels and fails with CS0152; with AGH0027 (`Map("dup")` twice) the same happens for `case "dup":`. Users see the Argh error and then compiler noise from a file they didn't write.

Cause: `TryBuildAppEmitModel` (`Analysis.cs:368-470`) reports the diagnostics but never checks their severity; `Execute` (`CliParserGenerator.cs:214-218`) emits nothing only when no commands exist.

**Expected (clap/cobra):** n/a. A generator error should be the only error.

### 56. Handlers in a referenced assembly get no documentation at all

A handler class in a referenced library, with XML docs and a generated doc file, shows no summary, no parameter descriptions, and loses its `-n` short option. An `[AsParameters]` DTO from the same library does show its docs, because `GetDocumentationXmlFromMetadataReference` (`Documentation.cs:432`) reads the doc file for DTOs; `CommandModel.cs:81, 116` has no such fallback for handlers. `GenerateDocumentationFile` on or off makes no difference to the output.

The compiled-XML path in `Documentation.ParseMethod` is dead for in-source handlers too: the document root is the `<member>` wrapper, so `root.Element("summary")` at `:388` is always null and the trivia path always runs. (Proven by patching `:383` to unwrap `<member>`: no change for in-source handlers, whose trivia path already works.)

### 57. Enum member docs are never shown, and enum defaults print as `[default: 0]`

The fixtures document their enum members (`TestColor`, `TestPalette` in `tests/Nullean.Argh.Tests/Fixtures/CliTestHandlers.cs`), but help only prints `One of: <red|blue>`. With the enum in the same file, another file, another assembly, or inside `IReadOnlySet<E>`, the result is the same.

Cause: `GetEnumMemberDocs` (`ParameterModel.cs:887`) reads `doc.Root?.Element("summary")`, which misses the `<member>` wrapper, so `EnumMemberDocs` is always empty. Unwrapping `<member>` makes same-assembly enums render `(red: Same red.; deep-blue: Same blue.)`; cross-assembly enums need the metadata-XML fallback too.

A related output bug: an enum parameter with a default prints `[default: 0]` rather than the member name, for example `--color <enum>  The color. [default: 0]`.

**Expected (clap/cobra):** clap's `ValueEnum` shows each variant's doc comment under `Possible values:` and prints the default as its name, `[default: red]`.

### 58. `[Range(typeof(decimal), "1", "2")]` doesn't compile

Generated: `if (d < typeof(decimal) || d > "1")` → two `CS0019`, and the error message text reads "between typeof(decimal) and 1".

Cause: `Validation.cs:283` reads constructor arguments 0 and 1 regardless of which `RangeAttribute` constructor was used; the `(Type, string, string)` form puts the bounds at 1 and 2. `[Range(1.5, 2.5)]` works on both `int` and `double` parameters.

### 59. A namespace name containing `{` or `}` doesn't compile

`app.MapNamespace("ns{x}", ...)` → six `CS0103: The name 'x' does not exist`. Explicit option names, aliases and command names with braces, quotes or backslashes are fine.

Cause: `Emit.Dispatch.cs:276-277` builds `pathPrefix` and `nsHelp` with `Escape` only, and splices them into an interpolated string at `:296, 303, 323`, where the braces become interpolation holes. `EscapeForHelpInterpolation` exists for this.

### 60. Reserved meta-command names can be mapped as commands

`app.Map("__schema", ...)`, `Map("__complete", ...)`, `Map("__completion", ...)` and `Map("__argh_root", ...)` all compile silently and appear in `--help` and in the schema's `commands` next to `reservedMetaCommands`. `__schema foo` runs the user's handler, because `IsSchemaInvocation` requires exactly one argument. User `__complete` and `__completion` commands are unreachable (the intrinsic wins at `Emit.Dispatch.cs:103-141`), and a bare `__complete` prints the intrinsic's usage error.

Cause: the AGH0013 `ReservedCommandNameRoot` diagnostic (`Diagnostics.cs:123`) is defined but never created.

### 61. `[NamespaceSegment]` is silently ignored by `MapNamespace<T>("literal")`

`[NamespaceSegment("seg")]` on a class registered with `MapNamespace<Seg>("other")` produces no diagnostic; the literal wins. `ValidateNamespaceSegmentForExplicitName` (`Analysis.cs:1100`), which would report AGH0018, has no callers.

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

### 64. A very long command name widens the listing column without a cap

A 97-character command name makes the listing column 97 wide at every width tried (redirected 80 columns and a 40, 60 or 80-column console), so every summary wraps into 12-character slivers. No exception and no negative width: `Emit.Dispatch.cs:653, 749` take the longest name, `CliHelpFormatting.cs:137` widens further at runtime, and `:152` clamps the summary column to at least 12.

**Expected (clap/cobra):** clap wraps a long name onto its own line and starts the description on the next, keeping the column at `max_term_width`; cobra pads to the longest name like Argh does.

## Found while fixing the encoding and TimeOnly branches

These were found while working on the `windows-encoding` and `timeonly` branches.

### 67. A required namespace option is only accepted before the command

```
EXE scalars-ns show P        → Error: missing required flag --span.   exit 2
EXE scalars-ns P show        → runs
```

`P` is the required options, as in bug 3. Optional namespace options are accepted on either side of the command.

Cause: the required check runs in the namespace pre-parse, before the command is matched (`src/Nullean.Argh.Generator/CliParserGenerator.Emit.Parsing.cs:302-309`), and fails before the command's own arguments are read.

**Expected (clap/cobra):** clap's `global(true)` arguments and cobra's persistent flags can appear before or after the subcommand, and required checks run after the whole command line is parsed.

### 68. A non-constant initializer on an options property doesn't make it optional

```csharp
public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
```

`--timeout` is still required, and leaving it out fails with `missing required flag --timeout`. The same goes for `= TimeSpan.Zero`, `= new(...)` and any method call.

Cause: `TryFormatInitializerOperation` (`src/Nullean.Argh.Generator/CliParserGenerator.SymbolReaders.cs:392-435`) recognises only literals and constant or enum fields, and returns `null` for anything else, so the property counts as having no default.

**Expected (clap/cobra):** a default can be any expression. clap's `default_value_t` takes an expression, and pflag defaults are ordinary Go values.

### 69. Help pads columns by UTF-16 length, so CJK command names misalign

```
Commands:
  unicode café  Accented command name.
  unicode 日本語   CJK command name.
```

`日本語` is three UTF-16 characters but six terminal columns, so its summary starts three columns too far right.

Cause: `WriteHelpListNameAndDescription` (`src/Nullean.Argh.Core/Help/CliHelpFormatting.cs:137-141`) pads with `name.Length`.

**Expected (clap/cobra):** clap measures display width, using `unicode-width` when its `unicode` feature is on. cobra pads by byte length and misaligns too.

### 70. A positional `bool` parameter doesn't compile

```csharp
static void Run([Argument] bool first, [Argument] bool second) { }
```

Generated, after the positional loop:

```csharp
if (positionals.Count <= 0)
    first = false;
else
{
    first = positionals[0]; // fallback
}
```

`first` is never declared, and the raw string is assigned to a `bool`, so the build fails with `CS0103: The name 'first' does not exist in the current context`, including at the handler call. A single `[Argument] bool` fails the same way, so it isn't a naming collision.

Cause: the positional binding path in `src/Nullean.Argh.Generator/CliParserGenerator.Emit.Runner.cs` (search for `// fallback`) has no `bool` case. Reported by session `argh-a5`; reproduced in the `bugs` scratch project (`bugs/bug70.cs`).

**Expected (clap/cobra):** clap accepts a positional `bool` through `value_parser!(bool)` (`true`/`false`); cobra has no typed positionals. Either compile it or report a diagnostic.

## Third pass: parsing and runtime

These started as suspicions and were confirmed with a repro app that has a global options class (`int Port = 80`, `long Big`, `double Ratio`, `float? Scale`, `decimal Money`, `DateTime? When`, `TimeSpan? Span`) and commands for each value type, in every binding path: method parameter, `[AsParameters]` DTO, environment variable, positional, and leading or trailing global.

### 71. `float`, `double` and `decimal` accept thousands separators

```
EXE num --f 1,5 --d 1,5 --m 1,5     → f=15 d=15 m=15     exit 0
EXE num --f 1,5,5                   → 155
EXE num --f 5,                      → 5
```

The same in every path: required, DTO, env var, positional, leading global. `int` rejects `1,000`, so the types disagree. Also accepted everywhere: `+5`, ` 5 `, and for `float`/`double` `1e3`, `NaN`, `Infinity`.

Cause: `NumberStyles.Float | NumberStyles.AllowThousands` at `Emit.Runner.cs:1370, 1379` and the nullable variants at `:1498-1506`; `NumberStyles.Number` (which includes `AllowThousands`) for `decimal` at `:1388`; `Emit.Parsing.cs:448, 452, 454, 456`.

**Expected (clap/cobra):** both reject `1,5`: Rust's `f64::from_str` and Go's `strconv.ParseFloat` take no thousands separators, and both are culture-independent.


**Partly addressed on `parse-culture`:** the default is unchanged (.NET's own number styles, which allow thousands separators), but `ParserOptions.NumberStyles` now lets an app exclude `AllowThousands`.
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

### 75. A repeated scalar flag silently takes the last value

```
EXE hello --name a --name b     → b
EXE hello -n a --name b         → b
EXE --port 2 glob --port 1      → 1
```

The fixture's `deploy --app-env prod --app-port 1 --app-env dev` gives `deploy:dev:1`. No warning anywhere; the generator has no notion of "specified multiple times".

**Expected (clap/cobra):** they differ. clap errors with `the argument '--name <NAME>' cannot be used multiple times` unless `ArgAction::Append` or `args_override_self` is set; pflag takes the last value silently. A design decision, but a deliberate one.

### 76. A bare number is a `TimeSpan` in days, and the compact format is undocumented and incomplete

`ArghTimeSpan` (`src/Nullean.Argh.Core/ArghTimeSpan.cs`) accepts `10s`, `90m`, `1d`, `00:30`, `1:2` (1 h 2 min) and `1.00:00:00`, and rejects `1.5h`, `1h30m`, `-5m`, `10ms`, `1w`, `0.5s`, `+5s` and `PT10S`. A bare `10` falls back to `TimeSpan.TryParse`, which reads it as 10 days: `temporal-cmd --duration 10` → 864000 seconds. Help shows only `<timeSpan>`, and `docs/` never mentions the compact form.

**Expected (clap/cobra):** Go's `time.ParseDuration` and Rust's `humantime` both reject a bare number and accept compound values such as `1h30m`, `1.5h`, `-5m` and `10ms`. Reject bare numbers (or document them), accept compound values, and document the format in help.

### 77. The `Console.CancelKeyPress` handler is never removed and captures a disposed token source

Generated code: `using var cts = ...; Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };` (`Emit.Dispatch.cs:333-335`). The handler stays registered after `RunAsync` returns, so a Ctrl+C after that, or during a second in-process `RunAsync`, calls `Cancel()` on a disposed source: `Unhandled exception. System.ObjectDisposedException: The CancellationTokenSource has been disposed`, exit `0xE0434352`. Observed by the repro agent with `GenerateConsoleCtrlEvent` after `RunAsync` returned, and in a second run where the stale handler fires first; not reproduced under a Bash or PowerShell pipe here, where no console event reaches the process.

**Expected (clap/cobra):** n/a. Unregister the handler when the run ends.

### 78. Hosting suppresses logging for any run that has `--help` or `--version` anywhere in argv

`ReproHost send --body --version` runs the command (`send body=[--version]`), yet the host's "Hosting starting" debug line and the handler's `LogInformation` output are suppressed. `send --body hi` shows them.

Cause: `IsArghBuiltinIntrinsic` uses `Array.Exists(args, a => a == "--help" || a == "--version")` (`src/Nullean.Argh.Core/ArghApp.cs:170-174`), consumed by `ArghHostingExtensions.cs:48, 104`.

### 79. Hosting's `Environment.Exit` skips `StopAsync` and disposal of other services

For a normal run and for `--help`: another hosted service's `StartAsync` runs, but its `StopAsync`, `IAsyncDisposable`/`IDisposable` singleton disposal, and `ApplicationStopping`/`ApplicationStopped` never happen; only `ProcessExit` fires. Cause: `ArghHostingCliService.cs:59` calls `Environment.Exit`. `docs/hosting/index.md:34` says the host doesn't continue, but not that shutdown is skipped (the XML remarks do).

### 80. A base type's validator is skipped when the derived type has one

With validators registered for `BaseDto` and `DerivedDto`, binding a `DerivedDto` runs only the derived validator: `val --level -1 --name x` passes although the base validator rejects `level < 0`. With only the base validator registered, it covers the derived type.

Cause: `ArghValidation.cs:60-73` returns after the first type in the hierarchy that has a validator.

### 81. Combined short flags are rejected, and the limitation isn't documented

`-vn foo`, `-nfoo`, `-c1` and `-vv` all fail with `Error: combined short flags (e.g. -abc) are not supported.` followed by the full help. `-n=foo` works. `docs/` never states the limitation.

**Expected (clap/cobra):** both accept `-vn foo` and `-nfoo`. Either support clustering or document that it isn't.

### 82. AGH0003 fires when injected options precede an `[Argument]` parameter

`Handler(Globals g, [Argument] string who)` fails with `AGH0003: Parameters marked with [Argument] must start at position 0 and be consecutive`, while `([Argument] string who, Globals g)` builds. The injected options are expanded as flags at their parameter position before the check in `Validation.cs:183-196` runs (`CommandModel.cs:70, 105`).
Session `argh-a5` hit the same check from the other side: `[Argument]` parameters must come before the injected options parameter or AGH0003 fires.

### 83. Command names are case-insensitive but flags aren't, and the unknown `-V` message depends on position

`HELLO` routes to `hello`, but `--NAME` is an unknown option. `-V` before the command gives `unknown short option '-V'`; after it, `missing value for short flag '-V'`, which is misleading.

**Expected (clap/cobra):** both are case-sensitive for commands and flags by default. Pick one rule and one message.

### 84. Moved to feature F1

`HashSet<T>` and the other unrecognised collection shapes are recorded under Feature requests, F1. The number stays reserved.

### 85. The incremental generator's cache never hits, so every edit re-runs the whole emit

With step tracking on, the `Argh.AnalyzedInvocation` step reports `Modified` after an unrelated edit, such as adding a comment, so the collected invocations and the whole main emit re-run on every keystroke in an IDE.

Cause: `AIMapCommand` (`CliParserGenerator.Models.cs:206-209`) carries `ImmutableArray<CommandModel> Commands`, and the nested models hold more `ImmutableArray`s. `ImmutableArray<T>.Equals` is reference equality of the backing array, and the generator has no equatable wrapper and no custom `Equals`, so records from two runs are never equal. `CLAUDE.md` describes these records as ones "that Roslyn can cache per invocation"; they are symbol-free but not cacheable. Found and measured by session `argh-a5` with a probe and a skipped test in `IncrementalCachingTests` on its `netstandard-fixes` branch; confirmed here from the code.

**Fix:** an `EquatableArray<T>` wrapper with structural equality and hashing, used throughout the models, as the System.Text.Json source generator does.

### By design, after checking

- `--version` only works at the root (`hello --version` is an unknown option). clap accepts `--version` only where it's defined, which is the root by default, and cobra reports `unknown flag`, so this matches both.

Checked and refuted:

- Explicit option names, aliases and command names containing `{`, `}`, `"` or `\` compile, render and parse correctly. `Escape(left)` at `Emit.Help.cs:167, 555, 585` sits inside a nested plain string literal, so the braces are inert. Only namespace names are affected (bug 59).
- `[Hidden]` items are excluded from listings but `hidden-cmd --help` still prints; cobra and clap behave the same for hidden subcommands, so this is intended.

## Feature requests

Not defects, but gaps found while bug hunting that the user wants recorded. Confirmed on `main` at `45954b6` by session `argh-a5` with a probe app (one command per type, built and run with `c --v 1 --v 2`) and by the `feat01.cs` build here.

| # | Feature | Scratch file |
|---|---|---|
| F1 | Support the common collection shapes, and lift the `IReadOnlySet<T>` value-type restriction | `feat01.cs` (build break, `#if FEAT01`) |
| F2 | Dictionary parameters | `feat02.cs` (comment only) |

### F1. Support the common collection shapes

Supported today (build, and bind `1,2` correctly): `T[]`, `IEnumerable<T>`, `IReadOnlyList<T>`, `List<T>`, `IReadOnlySet<T>` with value-type or enum elements.

Not supported:

- `IReadOnlySet<string>`: rejected with `AGH0028: IReadOnlySet<T> only supports value-type or enum element types; 'string' is not allowed` (`src/Nullean.Argh.Generator/CliParserGenerator.Diagnostics.cs:245-246`).
- `IList<T>`, `ICollection<T>`, `IReadOnlyCollection<T>`, `ISet<T>`, `HashSet<T>` (formerly bug 84), `ImmutableArray<T>`, `ImmutableList<T>`: fall through to the scalar path and fail with `CS1503: cannot convert from 'string' to '<type>'`, with no Argh diagnostic (that missing diagnostic is bug 12).

The recognised generic definitions are listed at `src/Nullean.Argh.Generator/CliParserGenerator.SymbolReaders.cs:679-682`; arrays come through `IArrayTypeSymbol` in `ParameterModel.cs`.

Wanted: `IReadOnlyList`, `IList`, `IReadOnlyCollection`, `ICollection`, `IReadOnlySet`, `ISet`, `List`, `HashSet`, `ImmutableArray` and `ImmutableList`, with any supported element type. Each must also work with `[CollectionSyntax(Separator = ...)]` and the repeatable form, completion of enum element values, `__schema` (`array` + `elementType`), default values, nullable collections, and `[AsParameters]`/options-class members as well as method parameters.

**Prior art (clap/cobra):** clap binds a multi-value argument into any `FromIterator` collection (`Vec`, `HashSet`, `BTreeSet`); pflag offers `StringSlice`, `IntSlice`, `StringArray` and friends.

### F2. Dictionary parameters

`IReadOnlyDictionary<string, int>`, `Dictionary<string, int>` and `ImmutableDictionary<string, int>` all fail like F1's types (CS1503, no diagnostic). Supporting them needs:

- a CLI syntax: repeated `--label key=value`, and a separator form `--label a=1,b=2`;
- key and value parsing for every supported scalar type, including enums and `[ArgumentParser]` types;
- a duplicate-key policy: error, last wins, or merge;
- help text (`--label <key=value>`), completion of enum keys and values, defaults, nullable, `[AsParameters]` members;
- a `__schema` shape: cli-schema v1 has no map type, so an array of strings with a pattern, or a vendor extension.

**Prior art (clap/cobra):** pflag has `StringToString` and `StringToInt` (`--labels a=1,b=2`, repeatable); clap does it with a custom value parser that splits on `=`.
