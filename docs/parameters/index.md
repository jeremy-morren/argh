# Parameters and binding

Method parameters become CLI flags automatically. No attribute boilerplate for the common case.

## Overview

Nullean.Argh supports several parameter binding modes:

- **[Flags](flags.md)** - named options (`--output-dir ./bin`), derived from C# parameter names
- **[Arguments](arguments.md)** - positional parameters (`myapp deploy production`), marked with `[Argument]`
- **[DTO binding](../features/dto-binding.md)** - expand records/classes into individual flags with `[AsParameters]`
- **[Custom parsers](custom-parsers.md)** - `IArgumentParser<T>` for types with no built-in support
- **[Validation](../features/validation.md)** - DataAnnotations and filesystem path attributes

## Supported types

| Category | Types |
|----------|-------|
| Primitives | `string`, `int`, `long`, `double`, `float`, `decimal`, `bool`, `bool?` |
| System | `enum`, `FileInfo`, `DirectoryInfo`, `Uri` |
| Collections | `List<T>`, `T[]` |

### Culture and styles

Numbers, dates and times, and `TimeSpan` values are parsed with `CultureInfo.InvariantCulture` by default, so `--amount 1.5` and `--day 2024-06-15` work the same on every machine. To accept a specific culture's format instead, set it on the app or hosting builder:

```csharp
var app = new ArghApp().UseCulture(CultureInfo.GetCultureInfo("de-DE"));
// myapp pay --amount 1,5 --day 15.06.2024
```

`UseParserOptions` sets the culture and the styles together:

```csharp
app.UseParserOptions(new ParserOptions
{
    Culture = CultureInfo.GetCultureInfo("en-GB"),
    NumberStyles = NumberStyles.Float | NumberStyles.AllowThousands, // --count 1,000
    DateTimeStyles = DateTimeStyles.AdjustToUniversal,
});
```

- `Culture`: `CultureInfo.InvariantCulture` by default.
- `NumberStyles`: `Float` by default, for every number type: a leading sign, a decimal point, an exponent, and leading and trailing whitespace, so `1.5`, `-2` and `1e3` parse. Group separators are rejected, so `1,000` is an error; add `AllowThousands` to accept them. An integer takes a decimal point or an exponent only for a whole number: `--count 1e3` is `1000` and `--count 1.0` is `1`, but `--count 1.5` is an error.
- `DateTimeStyles`: `RoundtripKind | AllowWhiteSpaces` by default, so `2024-06-15T14:30:00Z` stays UTC. It applies to `DateTime` and `DateTimeOffset`. `DateOnly` and `TimeOnly` get only its whitespace flags, because they fail on any other. `TimeSpan` doesn't use it: it has its own format and Argh's compact syntax such as `5m`.

The options apply to flag and positional values, environment-variable fallbacks, collection elements, `[AsParameters]` members and options classes, nullable or not, and they're passed to [custom parsers](custom-parsers.md). Numbers that Argh shows users use the culture too: `[default: …]` in help, the values of `[AllowedValues]` and `[DeniedValues]` and the bounds of `[Range]` in help and validation errors, and `[AllowedValues]` numbers offered by [completion](../features/completions.md), so a completed value parses back to the same number. The current options are `ArghRuntime.ParserOptions`. `TryParseArgh` and `TryParseArghExact` have overloads that take `ParserOptions` for one call. Command and option names, enum values, string allowed values and `__schema` stay invariant, and completion matches what's typed so far without the culture, as parsing does, so the Turkish `I` doesn't stop `--INFO` matching `--info`.

Things to know about cultures:

- Under a culture whose decimal separator is `,`, a `.` is a group separator, so `--amount 1.5` is rejected, or reads as `15` with `AllowThousands`.
- ISO 8601 dates such as `2024-06-15` parse under any culture with a Gregorian calendar. A culture with another default calendar reads them in that calendar: under `fa-IR`, `2024-06-15` is a Persian date (`1403/03/26` is 15 June 2024). Keep the invariant culture if you expect ISO dates.
- .NET Framework uses Windows culture data (NLS), and .NET 5 and later use ICU, so some details differ. For example, ICU accepts the Unicode minus sign `−` under `sv-SE` and uses `٫` as the `fa-IR` decimal separator, where NLS rejects `−` and uses `/`.
- With `InvariantGlobalization` enabled and `PredefinedCulturesOnly` set to `false`, every culture carries invariant data, so a configured culture has no effect.

### Collections

Collection flags accept the flag multiple times by default. Use `[CollectionSyntax(Separator=",")]` for comma-separated input instead. For variadic positionals, use `[Argument] T[]` or `[Argument] params T[]`.

::::tab-set

:::tab-item Repeated flag
:sync: repeated

```csharp
public static Task<int> Deploy(string[] targets) { … }
```

```
myapp deploy --targets web --targets api
```

:::

:::tab-item Comma-separated
:sync: separator

```csharp
public static Task<int> Deploy([CollectionSyntax(Separator = ",")] string[] tags) { … }
```

```
myapp deploy --tags blue,green
```

:::

::::

## How it works

The source generator reads your method signatures at build time and emits typed parsers for each parameter. Parameter names are converted from camelCase to kebab-case for CLI flags.

Parameters with default values are optional; those without are required.
