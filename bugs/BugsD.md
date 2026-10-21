# Argh bugs, issue D: Help and XML docs

Proposed issues: [A: Parsing and binding](BugsA.md) · [B: Generator: uncompilable output and missing diagnostics](BugsB.md) · [C: Runtime, dispatch and hosting](BugsC.md) · [D: Help and XML docs](BugsD.md) · [E: Completion and `__schema`](BugsE.md)

What `--help` prints: rendering of XML doc tags, defaults, layout and colour.

| # | Bug | Severity | Scratch file |
|---|---|---|---|
| 42 | [Multi-line `<param>` text breaks the option table](#42-multi-line-param-text-breaks-the-option-table) | Wrong output | `bug42.cs` |
| 43 | [Inline styles leak to the rest of the line in colour mode](#43-inline-styles-leak-to-the-rest-of-the-line-in-colour-mode) | Wrong output | `bug43.cs` |
| 44 | [Blank lines and code styling in remarks differ between colour and plain mode](#44-blank-lines-and-code-styling-in-remarks-differ-between-colour-and-plain-mode) | Wrong output | `bug44.cs` |
| 45 | [String defaults show C# escape sequences](#45-string-defaults-show-c-escape-sequences) | Wrong output | `bug45.cs` |
| 46 | [Single-line `Notes:` skips the remarks transforms](#46-single-line-notes-skips-the-remarks-transforms) | Wrong output | `bug46.cs` |
| 47 | [`<see cref="M(System.String)"/>` renders as `String)`](#47-see-crefmsystemstring-renders-as-string) | Wrong output | `bug47.cs` |
| 48 | [Element-only `<para>` content is pretty-printed with newlines and indent](#48-element-only-para-content-is-pretty-printed-with-newlines-and-indent) | Wrong output | `bug48.cs` |
| 49 | [`<br/>` is dropped, joining words](#49-br-is-dropped-joining-words) | Wrong output | `bug49.cs` |
| 50 | [Multi-line `Notes:` starts with a blank line; two blank lines before it without `Options:`](#50-multi-line-notes-starts-with-a-blank-line-two-blank-lines-before-it-without-options) | Cosmetic | `bug50.cs` |
| 51 | [Numbered lists flatten to `-`; `<listheader>` and `<term>` are dropped](#51-numbered-lists-flatten-to---listheader-and-term-are-dropped) | Wrong output | `bug51.cs` |
| 52 | [Trailing spaces in `Usage:` and listing rows](#52-trailing-spaces-in-usage-and-listing-rows) | Cosmetic | `bug52.cs` |
| 54 | [`<inheritdoc/>` produces no docs and loses the inherited short options](#54-inheritdoc-produces-no-docs-and-loses-the-inherited-short-options) | Wrong output | `bug54.cs` |
| 56 | [Handlers in a referenced assembly get no documentation at all](#56-handlers-in-a-referenced-assembly-get-no-documentation-at-all) | Wrong output | `bug56.cs` + `lib/bug56.cs` |
| 57 | [Enum member docs are never shown, and enum defaults print as `[default: 0]`](#57-enum-member-docs-are-never-shown-and-enum-defaults-print-as-default-0) | Wrong output | `bug57.cs` |
| 64 | [A very long command name widens the listing column without a cap](#64-a-very-long-command-name-widens-the-listing-column-without-a-cap) | Cosmetic | `bug64.cs` (build break, `#if BUG64`) |
| 69 | [Help pads columns by UTF-16 length, so CJK command names misalign](#69-help-pads-columns-by-utf-16-length-so-cjk-command-names-misalign) | Cosmetic | `bug69.cs` |

## Background

Found by reviewing `main` at `45954b6` (0.21.0) in a fresh clone, on Windows 11 with .NET 10. Every bug below was reproduced: by running the test fixture host `Nullean.Argh.Tests.CliHost` (built in Release), by building small probe projects that reference the framework, or both. Paths are relative to the repository root; line numbers are from `45954b6`.

The expected behaviour for each bug is what **clap** (Rust, v4.6.7) and **cobra** (Go, v1.10.2, with pflag v1.0.10) do, checked against their source. Where they differ, both are given. Source links are pinned to those release tags.

Repro commands use `EXE` for `.artifacts/bin/Nullean.Argh.Tests.CliHost/release/Nullean.Argh.Tests.CliHost.exe` with `NO_COLOR=1` set. The fixture's middleware output (`[tests:middleware:...]`) is left out.

Bugs fixed on the open branches are removed: 53 (en dash in help, and no Windows CI) and 65 (non-ASCII text in `__schema`) on `windows-encoding` ([#85](https://github.com/nullean/argh/pull/85)), 13 (parameters named like generated locals), 15 (`fooBar` and `foo_bar` sharing a local) and 66 (`Uri` and `Uri?` in one method) on `timeonly` ([#83](https://github.com/nullean/argh/pull/83)), the `allowed`/`denied` part of 35 on `enum-completion` ([#82](https://github.com/nullean/argh/pull/82)), and 31 (fish), 32 (zsh), 86 (bash 3.2 `mapfile`) and 87 (CRLF candidates) on `completion-test` ([#86](https://github.com/nullean/argh/pull/86), every real-shell matrix entry passing in [run 39530303045](https://github.com/jeremy-morren/argh/actions/runs/39530303045)). On `parse-culture` ([#88](https://github.com/nullean/argh/pull/88), stacked on [#87](https://github.com/nullean/argh/pull/87)): 72 (the culture-sensitive trailing re-parse; every numeric parse now uses the configured `ParserOptions`, invariant by default, with `Nullable<T>` lifted to `T`), and an unlisted bug where `[ArgumentParser]` on a `T?` parameter generated `Point??` and didn't compile (it now uses `IArgumentParser<T?>` if implemented, else `IArgumentParser<T>`). The remaining bugs keep their numbers, because entries refer to each other by number.

These were reproduced with a probe project whose handlers use each XML tag; colour output was captured through a ConPTY so the app saw a real console.

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

Cause: `Emit.Help.cs:939` only trims the quotes from the `SymbolDisplay` literal. (Same root cause as the `[AllowedValues]` quoting fixed in [#82](https://github.com/nullean/argh/pull/82).)

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

### 54. `<inheritdoc/>` produces no docs and loses the inherited short options

A handler documented with `<inheritdoc/>` (interface implementation, same-assembly base override, cross-assembly base override, or `<inheritdoc cref="..."/>`) gets no summary and no parameter descriptions, and the short options declared in the inherited `<param>` tags (`-n`, `-c`, `-v`) are lost. The root listing shows bare names. No diagnostic is reported.

Cause: Roslyn's `GetDocumentationCommentXml()` returns the literal `<inheritdoc/>` unexpanded, and the trivia fallback (`src/Nullean.Argh.Generator/CliParserGenerator.Documentation.cs:218-238`) parses the same text. Nothing resolves the referenced member. `plans/vision.md` lists `<inheritdoc>` as supported.

### 56. Handlers in a referenced assembly get no documentation at all

A handler class in a referenced library, with XML docs and a generated doc file, shows no summary, no parameter descriptions, and loses its `-n` short option. An `[AsParameters]` DTO from the same library does show its docs, because `GetDocumentationXmlFromMetadataReference` (`Documentation.cs:432`) reads the doc file for DTOs; `CommandModel.cs:81, 116` has no such fallback for handlers. `GenerateDocumentationFile` on or off makes no difference to the output.

The compiled-XML path in `Documentation.ParseMethod` is dead for in-source handlers too: the document root is the `<member>` wrapper, so `root.Element("summary")` at `:388` is always null and the trivia path always runs. (Proven by patching `:383` to unwrap `<member>`: no change for in-source handlers, whose trivia path already works.)

### 57. Enum member docs are never shown, and enum defaults print as `[default: 0]`

The fixtures document their enum members (`TestColor`, `TestPalette` in `tests/Nullean.Argh.Tests/Fixtures/CliTestHandlers.cs`), but help only prints `One of: <red|blue>`. With the enum in the same file, another file, another assembly, or inside `IReadOnlySet<E>`, the result is the same.

Cause: `GetEnumMemberDocs` (`ParameterModel.cs:887`) reads `doc.Root?.Element("summary")`, which misses the `<member>` wrapper, so `EnumMemberDocs` is always empty. Unwrapping `<member>` makes same-assembly enums render `(red: Same red.; deep-blue: Same blue.)`; cross-assembly enums need the metadata-XML fallback too.

A related output bug: an enum parameter with a default prints `[default: 0]` rather than the member name, for example `--color <enum>  The color. [default: 0]`.

**Expected (clap/cobra):** clap's `ValueEnum` shows each variant's doc comment under `Possible values:` and prints the default as its name, `[default: red]`.

### 64. A very long command name widens the listing column without a cap

A 97-character command name makes the listing column 97 wide at every width tried (redirected 80 columns and a 40, 60 or 80-column console), so every summary wraps into 12-character slivers. No exception and no negative width: `Emit.Dispatch.cs:653, 749` take the longest name, `CliHelpFormatting.cs:137` widens further at runtime, and `:152` clamps the summary column to at least 12.

**Expected (clap/cobra):** clap wraps a long name onto its own line and starts the description on the next, keeping the column at `max_term_width`; cobra pads to the longest name like Argh does.

### 69. Help pads columns by UTF-16 length, so CJK command names misalign

```
Commands:
  unicode café  Accented command name.
  unicode 日本語   CJK command name.
```

`日本語` is three UTF-16 characters but six terminal columns, so its summary starts three columns too far right.

Cause: `WriteHelpListNameAndDescription` (`src/Nullean.Argh.Core/Help/CliHelpFormatting.cs:137-141`) pads with `name.Length`.

**Expected (clap/cobra):** clap measures display width, using `unicode-width` when its `unicode` feature is on. cobra pads by byte length and misaligns too.

### Checked and refuted

- `[Hidden]` items are excluded from listings but `hidden-cmd --help` still prints; cobra and clap behave the same for hidden subcommands, so this is intended.
