# Argh bugs, issue B: Generator: uncompilable output and missing diagnostics

Proposed issues: [A: Parsing and binding](BugsA.md) · [B: Generator: uncompilable output and missing diagnostics](BugsB.md) · [C: Runtime, dispatch and hosting](BugsC.md) · [D: Help and XML docs](BugsD.md) · [E: Completion and `__schema`](BugsE.md)

Handler shapes that make the generated `ArghGenerated.g.cs` fail to compile, with no Argh diagnostic, plus diagnostics that are defined but never fire.

| # | Bug | Severity | Scratch file |
|---|---|---|---|
| 12 | [Unsupported scalar types are treated as `string` and don't compile](#12-unsupported-scalar-types-are-treated-as-string-and-dont-compile) | Build break | `bug12.cs` (build break, `#if BUG12`) |
| 14 | [A command and a namespace with the same name don't compile](#14-a-command-and-a-namespace-with-the-same-name-dont-compile) | Build break | `bug14.cs` (build break, `#if BUG14`) |
| 16 | [A cross-assembly `[AsParameters]` DTO with `required` members doesn't compile](#16-a-cross-assembly-asparameters-dto-with-required-members-doesnt-compile) | Build break | `bug16.cs` + `lib/bug16.cs` (build break, `#if BUG16`) |
| 18 | [Duplicate CLI names created by `<param>` overrides aren't diagnosed](#18-duplicate-cli-names-created-by-param-overrides-arent-diagnosed) | Wrong result | `bug18.cs` |
| 21 | [Literal command names aren't validated](#21-literal-command-names-arent-validated) | Minor | `bug21.cs` (build break, `#if BUG21`) |
| 55 | [Generator errors don't halt emission](#55-generator-errors-dont-halt-emission) | Misleading message | `bug55.cs` (build break, `#if BUG55`) |
| 59 | [A namespace name containing `{` or `}` doesn't compile](#59-a-namespace-name-containing--or--doesnt-compile) | Build break | `bug59.cs` (build break, `#if BUG59`) |
| 60 | [Reserved meta-command names can be mapped as commands](#60-reserved-meta-command-names-can-be-mapped-as-commands) | Wrong result | `bug60.cs` (build break, `#if BUG60`) |
| 61 | [`[NamespaceSegment]` is silently ignored by `MapNamespace<T>("literal")`](#61-namespacesegment-is-silently-ignored-by-mapnamespacetliteral) | Minor | `bug61.cs` |
| 68 | [A non-constant initializer on an options property doesn't make it optional](#68-a-non-constant-initializer-on-an-options-property-doesnt-make-it-optional) | Wrong result | `bug68.cs` |
| 82 | [AGH0003 fires when injected options precede an `[Argument]` parameter](#82-agh0003-fires-when-injected-options-precede-an-argument-parameter) | Wrong diagnostic | `bug82.cs` (build break, `#if BUG82`) |
| 84 | [Moved to feature F1](#84-moved-to-feature-f1) | See Feature requests |  |
| 85 | [The incremental generator's cache never hits, so every edit re-runs the whole emit](#85-the-incremental-generators-cache-never-hits-so-every-edit-re-runs-the-whole-emit) | Performance | `bug85.cs` (comment only) |
| 97 | [`[Url]` on a `Uri[]` parameter doesn't compile](#97-url-on-a-uri-parameter-doesnt-compile) | Build break | `bug97.cs` (build break, `#if BUG97`) |
| F1 | [Support the common collection shapes, and lift the `IReadOnlySet<T>` value-type restriction](#f1-support-the-common-collection-shapes) | Feature request | `feat01.cs` (build break, `#if FEAT01`) |
| F2 | [Dictionary parameters](#f2-dictionary-parameters) | Feature request | `feat02.cs` (comment only) |

## Background

Found by reviewing `main` at `45954b6` (0.21.0) in a fresh clone, on Windows 11 with .NET 10. Every bug below was reproduced: by running the test fixture host `Nullean.Argh.Tests.CliHost` (built in Release), by building small probe projects that reference the framework, or both. Paths are relative to the repository root; line numbers are from `45954b6`.

The expected behaviour for each bug is what **clap** (Rust, v4.6.7) and **cobra** (Go, v1.10.2, with pflag v1.0.10) do, checked against their source. Where they differ, both are given. Source links are pinned to those release tags.

Repro commands use `EXE` for `.artifacts/bin/Nullean.Argh.Tests.CliHost/release/Nullean.Argh.Tests.CliHost.exe` with `NO_COLOR=1` set. The fixture's middleware output (`[tests:middleware:...]`) is left out.

Bugs fixed on the open branches are removed: 53 (en dash in help, and no Windows CI) and 65 (non-ASCII text in `__schema`) on `windows-encoding` ([#85](https://github.com/nullean/argh/pull/85)), 13 (parameters named like generated locals), 15 (`fooBar` and `foo_bar` sharing a local) and 66 (`Uri` and `Uri?` in one method) on `timeonly` ([#83](https://github.com/nullean/argh/pull/83)), the `allowed`/`denied` part of 35 on `enum-completion` ([#82](https://github.com/nullean/argh/pull/82)), and 31 (fish), 32 (zsh), 86 (bash 3.2 `mapfile`) and 87 (CRLF candidates) on `completion-test` ([#86](https://github.com/nullean/argh/pull/86), every real-shell matrix entry passing in [run 39530303045](https://github.com/jeremy-morren/argh/actions/runs/39530303045)). On `parse-culture` ([#88](https://github.com/nullean/argh/pull/88), stacked on [#87](https://github.com/nullean/argh/pull/87)): 71 (`float`, `double` and `decimal` no longer accept thousands separators: `ParserOptions.NumberStyles` now defaults to `NumberStyles.Float` for every floating-point and decimal parse), 72 (the culture-sensitive trailing re-parse; every numeric parse now uses the configured `ParserOptions`, invariant by default, with `Nullable<T>` lifted to `T`), and an unlisted bug where `[ArgumentParser]` on a `T?` parameter generated `Point??` and didn't compile (it now uses `IArgumentParser<T?>` if implemented, else `IArgumentParser<T>`). On `range-unbounded` ([#90](https://github.com/nullean/argh/pull/90)): 10 and 11 (every number, enum and char the generator emits now carries its type: `1.5m`, `1.5f`, `(byte)5`, `double.NaN`, `global::Level.High`, `(global::Access)(3)`), 17 (a nullable enum keeps its `?`), 36 (`null`/`default` defaults no longer appear in help or `__schema`), 58 (`[Range(typeof(T), "min", "max")]` is parsed at build time, with AGH0038 on a bad bound), 89 (validation attributes on namespace and global options members are checked, whether the value comes before the namespace, after the command or from `[Env]`), 90 (AGH0038 on a bad bound of an options-type member, inherited ones included), and an unlisted regression from [#89](https://github.com/nullean/argh/pull/89): an unset `short?`, `byte?` or `Half?` with `[AllowedValues]` failed "value must be one of" because the check had no `HasValue` guard. On `small-fixes` (on top of [#91](https://github.com/nullean/argh/pull/91), no PR yet): 19 (`[Env]` on a positional argument is read), 20 (a `params` array is optional), 67 (a required namespace option is accepted after the command, and a namespace with required options and no command shows its help), 74 (`--help` after a value-taking flag is that flag's value, and an unknown option before `--help` is an error), 92 (a global option between the namespace and the command is bound and validated) and 93 (no nullable warnings for an optional non-nullable `string` with a string validation attribute). On `bool-parsing` (on `small-fixes`, no PR yet): 1 (the command line wins over an environment variable, also for a global or namespace flag given before the command), 2 (`--flag=false` and clap's other boolish values set a plain `bool`), 6 (boolean environment variables are read case-insensitively), 8 (an unparseable `bool?` value is an error) and 70 (a positional `bool` compiles and binds); the branch also fixes these unlisted ones: a `bool` default of `true` was ignored, `bool?` never read its `[Env]` variable, non-boolean `bool[]` elements bound as `true`, and `UseEnvironmentPrefix` was never applied. On `unmatched-args` (no PR yet): 3 (options after the command get the same parsing and errors as before it, for every type), 4 (an unexpected argument is an error, as in clap) and 7 (`--` ends the options), and an unlisted one: an invalid environment value for an options member was ignored and is now an error. On `path-uri-input` (no PR yet): 5 (an empty or invalid `FileInfo`/`DirectoryInfo` value is a parse error and the constructor's exception is reported, also on .NET Framework) and 73 (an empty or invalid `Uri` is an error; the new `[UriOptions(UriKind)]` defaults to `Absolute`), and an unlisted one: `[UriScheme]` on a `string` generated code that didn't compile (CS1061 on `main`) and is now AGH0024. The remaining bugs keep their numbers, because entries refer to each other by number.

Each of these was reproduced with a small console project referencing the framework. "Doesn't compile" means the generated `ArghGenerated.g.cs` fails to compile, with no Argh diagnostic to explain why.

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

## Bugs

### 12. Unsupported scalar types are treated as `string` and don't compile

A parameter of type `char`, `byte`, `sbyte`, `short`, `ushort`, `uint`, `ulong`, `Guid`, or a user struct implementing `IParsable<T>` without `[ArgumentParser]`, produces dozens of CS0029/CS1503 errors, for example `string? b = 18446744073709551615;`.

Cause: `GetSimpleTypeName` in `ParameterModel.cs:910-951` falls through to `return "string"`, and `ClassifyScalar` (`:824`) has no "unsupported type" diagnostic.
Unrecognised collection shapes (`IList<T>`, `HashSet<T>`, `ImmutableArray<T>`, dictionaries, ...) fall through the same way and fail with CS1503; supporting them is feature F1/F2 below, but the missing diagnostic is this bug.

**Expected:** either support the type or report an Argh diagnostic naming the parameter and type. clap's derive macro fails at compile time with a message naming the type.

**Partly fixed on `parsable-types`** ([#89](https://github.com/nullean/argh/pull/89), stacked on [#88](https://github.com/nullean/argh/pull/88)): on .NET 7 and later, any type implementing `IParsable<T>` binds automatically, which covers `Guid`, `char`, `byte`, `sbyte`, `short`, `ushort`, `uint`, `ulong`, `Half`, `Int128`, `BigInteger` and user `IParsable`/`ISpanParsable` types (including explicit implementations), as plain, nullable, collection, DTO, options and env values. Still broken: those types on target frameworks before .NET 7, where `IParsable<T>` doesn't exist, and any type that is neither built in nor `IParsable`, which still fails with CS1503 and no diagnostic.

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

### 18. Duplicate CLI names created by `<param>` overrides aren't diagnosed

```csharp
/// <param name="name">--label, The name.</param>
static void Cmd(string name, string label) { }
```

This compiles. Help lists `--label` twice, `--label A` binds both parameters, and `--name` is "unknown option". The same happens for an alias such as `--nm` given to two parameters.

Cause: `ReportDuplicateCliNamesAcc` runs on the raw models (`CliParserGenerator.CommandModel.cs:68, 103`), but the explicit names and aliases from `<param>` are applied later by `ApplyParamDocumentation` (`:82, 117`; body `:338-375`), so AGH0007 never sees them.

**Expected (clap/cobra):** both reject two arguments with the same long name at build time.

### 21. Literal command names aren't validated

`app.Map("--help", …)`, `"-x"`, `"has space"` and `""` all compile. The `--help` command is unreachable because root help wins; `has space` is listed in help but can't be typed as one token. No diagnostic covers this.

**Expected (clap/cobra):** clap's debug asserts reject such names; cobra doesn't check. A diagnostic is the right answer.

### 55. Generator errors don't halt emission

`CLAUDE.md` says "Errors halt code generation; warnings allow it to continue". They don't: with AGH0033 (duplicate `-v`) reported, the generated file still contains both `case 'v':` labels and fails with CS0152; with AGH0027 (`Map("dup")` twice) the same happens for `case "dup":`. Users see the Argh error and then compiler noise from a file they didn't write.

Cause: `TryBuildAppEmitModel` (`Analysis.cs:368-470`) reports the diagnostics but never checks their severity; `Execute` (`CliParserGenerator.cs:214-218`) emits nothing only when no commands exist.

**Expected (clap/cobra):** n/a. A generator error should be the only error.

### 59. A namespace name containing `{` or `}` doesn't compile

`app.MapNamespace("ns{x}", ...)` → six `CS0103: The name 'x' does not exist`. Explicit option names, aliases and command names with braces, quotes or backslashes are fine.

Cause: `Emit.Dispatch.cs:276-277` builds `pathPrefix` and `nsHelp` with `Escape` only, and splices them into an interpolated string at `:296, 303, 323`, where the braces become interpolation holes. `EscapeForHelpInterpolation` exists for this.

### 60. Reserved meta-command names can be mapped as commands

`app.Map("__schema", ...)`, `Map("__complete", ...)`, `Map("__completion", ...)` and `Map("__argh_root", ...)` all compile silently and appear in `--help` and in the schema's `commands` next to `reservedMetaCommands`. `__schema foo` runs the user's handler, because `IsSchemaInvocation` requires exactly one argument. User `__complete` and `__completion` commands are unreachable (the intrinsic wins at `Emit.Dispatch.cs:103-141`), and a bare `__complete` prints the intrinsic's usage error.

Cause: the AGH0013 `ReservedCommandNameRoot` diagnostic (`Diagnostics.cs:123`) is defined but never created.

### 61. `[NamespaceSegment]` is silently ignored by `MapNamespace<T>("literal")`

`[NamespaceSegment("seg")]` on a class registered with `MapNamespace<Seg>("other")` produces no diagnostic; the literal wins. `ValidateNamespaceSegmentForExplicitName` (`Analysis.cs:1100`), which would report AGH0018, has no callers.

### 68. A non-constant initializer on an options property doesn't make it optional

```csharp
public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
```

`--timeout` is still required, and leaving it out fails with `missing required flag --timeout`. The same goes for `= TimeSpan.Zero`, `= new(...)` and any method call.

Cause: `TryFormatInitializerOperation` (`src/Nullean.Argh.Generator/CliParserGenerator.SymbolReaders.cs:392-435`) recognises only literals and constant or enum fields, and returns `null` for anything else, so the property counts as having no default.

**Expected (clap/cobra):** a default can be any expression. clap's `default_value_t` takes an expression, and pflag defaults are ordinary Go values.

### 82. AGH0003 fires when injected options precede an `[Argument]` parameter

`Handler(Globals g, [Argument] string who)` fails with `AGH0003: Parameters marked with [Argument] must start at position 0 and be consecutive`, while `([Argument] string who, Globals g)` builds. The injected options are expanded as flags at their parameter position before the check in `Validation.cs:183-196` runs (`CommandModel.cs:70, 105`).
Session `argh-a5` hit the same check from the other side: `[Argument]` parameters must come before the injected options parameter or AGH0003 fires.

### 84. Moved to feature F1

`HashSet<T>` and the other unrecognised collection shapes are recorded under Feature requests, F1. The number stays reserved.

### 85. The incremental generator's cache never hits, so every edit re-runs the whole emit

With step tracking on, the `Argh.AnalyzedInvocation` step reports `Modified` after an unrelated edit, such as adding a comment, so the collected invocations and the whole main emit re-run on every keystroke in an IDE.

Cause: `AIMapCommand` (`CliParserGenerator.Models.cs:206-209`) carries `ImmutableArray<CommandModel> Commands`, and the nested models hold more `ImmutableArray`s. `ImmutableArray<T>.Equals` is reference equality of the backing array, and the generator has no equatable wrapper and no custom `Equals`, so records from two runs are never equal. `CLAUDE.md` describes these records as ones "that Roslyn can cache per invocation"; they are symbol-free but not cacheable. Found and measured by session `argh-a5` with a probe and a skipped test in `IncrementalCachingTests` on its `netstandard-fixes` branch; confirmed here from the code.

**Fix:** an `EquatableArray<T>` wrapper with structural equality and hashing, used throughout the models, as the System.Text.Json source generator does.

### 97. `[Url]` on a `Uri[]` parameter doesn't compile

```csharp
static void Run([Url] Uri[] links) { }
```

```
dotnet build bugs -c Release -p:DefineConstants=BUG97
ArghGenerated.g.cs: error CS1503: Argument 1: cannot convert from 'System.Uri[]' to 'string?'
ArghGenerated.g.cs: error CS1503: Argument 2: cannot convert from 'System.UriKind' to 'in System.UriCreationOptions'
```

Generated:

```csharp
if (__p_links is { } __url___p_links && !
    (global::System.Uri.TryCreate(__url___p_links, global::System.UriKind.Absolute, out var __urlCheck___p_links) && ...))
```

`[Url]` on a single `Uri` compiles (`bug97-one`). The same errors on `main` (`45954b6`) and on `path-uri-input`, checked with a probe. Reported by the session building `path-uri-input`; reproduced in `bugs/bug97.cs`.

Cause: `EmitUrlConstraintCheck` (`src/Nullean.Argh.Generator/CliParserGenerator.Emit.Parsing.cs:1079-1094`) validates the variable as one value, and for a collection it is handed the whole array.

**Expected:** compile, and check each element as the other per-element constraints do, or report a diagnostic if `[Url]` isn't supported on `Uri` collections.

### Checked and refuted

- Explicit option names, aliases and command names containing `{`, `}`, `"` or `\` compile, render and parse correctly. `Escape(left)` at `Emit.Help.cs:167, 555, 585` sits inside a nested plain string literal, so the braces are inert. Only namespace names are affected (bug 59).

## Feature requests

Not defects, but gaps found while bug hunting that the user wants recorded. Confirmed on `main` at `45954b6` by session `argh-a5` with a probe app (one command per type, built and run with `c --v 1 --v 2`) and by the `feat01.cs` build here.

### F1. Support the common collection shapes

Supported today (build, and bind `1,2` correctly): `T[]`, `IEnumerable<T>`, `IReadOnlyList<T>`, `List<T>`, `IReadOnlySet<T>` with value-type or enum elements.

Not supported:

- `IReadOnlySet<string>`: rejected with `AGH0028: IReadOnlySet<T> only supports value-type or enum element types; 'string' is not allowed` (`src/Nullean.Argh.Generator/CliParserGenerator.Diagnostics.cs:245-246`).
- `IList<T>`, `ICollection<T>`, `IReadOnlyCollection<T>`, `ISet<T>`, `HashSet<T>` (formerly [bug 84](#84-moved-to-feature-f1)), `ImmutableArray<T>`, `ImmutableList<T>`: fall through to the scalar path and fail with `CS1503: cannot convert from 'string' to '<type>'`, with no Argh diagnostic (that missing diagnostic is [bug 12](#12-unsupported-scalar-types-are-treated-as-string-and-dont-compile)).

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
