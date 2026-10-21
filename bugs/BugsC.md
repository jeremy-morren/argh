# Argh bugs, issue C: Runtime, dispatch and hosting

Proposed issues: [A: Parsing and binding](BugsA.md) · [B: Generator: uncompilable output and missing diagnostics](BugsB.md) · [C: Runtime, dispatch and hosting](BugsC.md) · [D: Help and XML docs](BugsD.md) · [E: Completion and `__schema`](BugsE.md)

Registration, lambda dispatch, DI lifetimes, cancellation and error reporting.

| # | Bug | Severity | Scratch file |
|---|---|---|---|
| 22 | [`MapScoped`, `MapSingleton` and `MapTransient` register nothing](#22-mapscoped-mapsingleton-and-maptransient-register-nothing) | Wrong result | `hosted/bug22.cs` |
| 23 | [Lambda commands inside `MapNamespace` crash or run the wrong handler](#23-lambda-commands-inside-mapnamespace-crash-or-run-the-wrong-handler) | Crash | `bug23.cs` |
| 24 | [`ValueTask` lambdas aren't awaited](#24-valuetask-lambdas-arent-awaited) | Wrong result | `bug24.cs` |
| 25 | [Scoped handler types are resolved from the root provider](#25-scoped-handler-types-are-resolved-from-the-root-provider) | Crash in Development | `hosted/bug25.cs` |
| 26 | [Ctrl+C prints a stack trace and exits 1](#26-ctrlc-prints-a-stack-trace-and-exits-1) | Misleading message | `bug26.cs` |
| 27 | [`CommandContext.CommandPath` and `CommandName` leak `__argh_root`](#27-commandcontextcommandpath-and-commandname-leak-__argh_root) | Minor | `bug27.cs` |
| 28 | [Namespace-option parse errors always hint at root help](#28-namespace-option-parse-errors-always-hint-at-root-help) | Misleading message | `bug28.cs` |
| 29 | [An unknown flag in a namespace without a root handler is reported as a command typo](#29-an-unknown-flag-in-a-namespace-without-a-root-handler-is-reported-as-a-command-typo) | Misleading message | `bug29.cs` |
| 30 | [ANSI colour on stderr is decided by whether stdout is redirected](#30-ansi-colour-on-stderr-is-decided-by-whether-stdout-is-redirected) | Minor | `bug30.cs` |
| 77 | [The `Console.CancelKeyPress` handler is never removed and captures a disposed token source](#77-the-consolecancelkeypress-handler-is-never-removed-and-captures-a-disposed-token-source) | Crash | `bug77.cs` |
| 78 | [Hosting suppresses logging for any run that has `--help` or `--version` anywhere in argv](#78-hosting-suppresses-logging-for-any-run-that-has---help-or---version-anywhere-in-argv) | Wrong result | `hosted/bug78.cs` |
| 79 | [Hosting's `Environment.Exit` skips `StopAsync` and disposal of other services](#79-hostings-environmentexit-skips-stopasync-and-disposal-of-other-services) | Minor | `hosted/bug79.cs` |
| 80 | [A base type's validator is skipped when the derived type has one](#80-a-base-types-validator-is-skipped-when-the-derived-type-has-one) | Wrong result | `bug80.cs` |

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

## Bugs

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
