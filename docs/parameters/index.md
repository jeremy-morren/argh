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
| Dates and times | `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` (see [Durations](#durations)) |
| Collections | `List<T>`, `T[]` |
| Parsable | Any type that implements `IParsable<T>` on .NET 7 and later, such as `Guid`, `short`, `char` or your own types (see [below](#iparsablet-types)) |

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
- `DateTimeStyles`: `RoundtripKind | AllowWhiteSpaces` by default, so `2024-06-15T14:30:00Z` stays UTC. It applies to `DateTime` and `DateTimeOffset`. `DateOnly` and `TimeOnly` get only its whitespace flags, because they fail on any other. `TimeSpan` doesn't use it (see [Durations](#durations)).
- `RepeatedScalarBehaviour`: what a flag given more than once does, `Reject` by default (see [A flag given more than once](flags.md#a-flag-given-more-than-once)).

The options apply to flag and positional values, environment-variable fallbacks, collection elements, `[AsParameters]` members and options classes, nullable or not, and they're passed to [custom parsers](custom-parsers.md). Numbers that Argh shows users use the culture too: `[default: …]` in help, the values of `[AllowedValues]` and `[DeniedValues]` and the bounds of `[Range]` in help and validation errors, and `[AllowedValues]` numbers offered by [completion](../features/completions.md), so a completed value parses back to the same number. The current options are `ArghRuntime.ParserOptions`. `TryParseArgh` and `TryParseArghExact` have overloads that take `ParserOptions` for one call. Command and option names, enum values, string allowed values and `__schema` stay invariant, and completion matches what's typed so far without the culture, as parsing does, so the Turkish `I` doesn't stop `--INFO` matching `--info`.

Things to know about cultures:

- Under a culture whose decimal separator is `,`, a `.` is a group separator, so `--amount 1.5` is rejected, or reads as `15` with `AllowThousands`.
- ISO 8601 dates such as `2024-06-15` parse under any culture with a Gregorian calendar. A culture with another default calendar reads them in that calendar: under `fa-IR`, `2024-06-15` is a Persian date (`1403/03/26` is 15 June 2024). Keep the invariant culture if you expect ISO dates.
- .NET Framework uses Windows culture data (NLS), and .NET 5 and later use ICU, so some details differ. For example, ICU accepts the Unicode minus sign `−` under `sv-SE` and uses `٫` as the `fa-IR` decimal separator, where NLS rejects `−` and uses `/`.
- With `InvariantGlobalization` enabled and `PredefinedCulturesOnly` set to `false`, every culture carries invariant data, so a configured culture has no effect.

### Durations

A `TimeSpan` takes a human-readable duration, the union of what Go's [`time.ParseDuration`](https://github.com/golang/go/blob/go1.27.3/src/time/format.go#L1618-L1731) and Rust's [humantime](https://github.com/chronotope/humantime/blob/v2.4.0/src/duration.rs#L112-L373) accept:

```
myapp deploy --timeout 90s
myapp deploy --timeout 1h30m          # or "1h 30m", 1.5h, 90min, 5400s
myapp deploy --timeout "2days 3hours"
myapp deploy --timeout=-5m            # a sign applies to the whole value
myapp deploy --timeout 300ms
```

A duration is one or more parts, each a number and a unit, added up. Parts can be written together or apart, and whitespace may separate a number from its unit. A number can have a fraction (`1.5h`, `.5h`). Units are case-sensitive:

| Unit | Spellings |
|------|-----------|
| nanosecond | `ns`, `nsec`, `nanos` |
| microsecond | `us`, `µs`, `μs`, `usec` |
| millisecond | `ms`, `msec`, `millis` |
| second | `s`, `sec`, `secs`, `second`, `seconds` |
| minute | `m`, `min`, `mins`, `minute`, `minutes` |
| hour | `h`, `hr`, `hrs`, `hour`, `hours` |
| day | `d`, `day`, `days` |
| week | `w`, `wk`, `wks`, `week`, `weeks` |
| month (30.44 days) | `M`, `month`, `months` |
| year (365.25 days) | `y`, `yr`, `yrs`, `year`, `years` |

- A number needs a unit, as in Go and humantime, so `--timeout 10` is an error; only `0` needs none.
- `m` is minutes and `M` is months; `5H` or `5S` is an error.
- A value with a `:` is read in `TimeSpan`'s own format instead, so `00:05:00`, `1:30` (1 h 30 min) and `1.02:03:04` (1 day, 2 h 3 min 4 s) still work.
- The value must fit a `TimeSpan` (about ±29,000 years) and be a whole number of ticks (100 ns), so `150ns` is an error.
- The decimal separator is `.`, and also the configured culture's, so under `de-DE` both `1,5h` and `1.5h` work. Digits are ASCII.

An invalid value says why:

```
$ myapp deploy --timeout 10
Error: invalid TimeSpan for --timeout: '10'. Expected a unit after 10, such as 10s or 10m.
```

The same rules apply to flags, positionals, collection elements, `[AsParameters]` and options members, and environment variables. `[TimeSpanRange("5m", "2h")]` takes the same syntax. The generator parses its bounds at build time with the invariant culture and reports `AGH0040` when one isn't a duration or the minimum is above the maximum. Help shows `[time-span-range: 5m-2h]`, and `__schema` gives the bounds as ISO 8601 durations (`PT5M`, `PT2H`).

The parser is public, so you can use it on its own:

```csharp
TimeSpan t = HumanTime.Parse("1h 30m");                                    // 01:30:00
HumanTime.TryParse("1,5h", CultureInfo.GetCultureInfo("de-DE"), out var u); // 01:30:00
HumanTime.TryParse("10", null, out _, out var error);                       // false: "Expected a unit after 10, ..."
```

`Parse` throws a `FormatException` for text that isn't a duration and an `OverflowException` for one that doesn't fit a `TimeSpan`. `ArghTimeSpan.TryParse` adds the `:` form, as Argh binds a `TimeSpan`.

### Files, directories and URIs

An empty value for a `FileInfo`, `DirectoryInfo` or `Uri` is an error (exit code 2), as in clap. Any other value goes to the type's constructor, and a value it rejects is an error with the constructor's message:

```
$ myapp build --output ""
Error: invalid path for --output: ''. The path is empty.
```

Whitespace isn't rejected up front, because it can name a file, but some platforms refuse it: Windows does, Linux doesn't. Which paths a constructor rejects depends on the runtime too. .NET Framework rejects characters such as `|` that .NET Core accepts:

```
$ myapp build --output "a|b"     # on .NET Framework
Error: invalid path for --output: 'a|b'. Illegal characters in path.
```

A `Uri` must be absolute. `[UriOptions]` accepts relative URIs too, or only relative ones:

```csharp
public static void Fetch(
    Uri server,                                          // https://example.com
    [UriOptions(UriKind.Relative)] Uri path,             // docs/index.html
    [UriOptions(UriKind.RelativeOrAbsolute)] Uri? link = null)
{ … }
```

```
$ myapp fetch --server example.com --path docs
Error: invalid URI for --server: 'example.com'. Expected an absolute URI.
```

Help shows `[relative]` or `[relative or absolute]` after the description. `[UriOptions]` also applies to each element of a `Uri` collection. On another type it does nothing, and the generator reports `AGH0024`, as it does for `[UriScheme]` on a type other than `Uri` and for `UriKind.Relative` with `[UriScheme]` or `[Url]`, which no value can pass.

These rules apply wherever the value comes from: a flag, a positional, a collection element, an `[AsParameters]` or options member, or an environment variable.

### Collections

Collection flags accept the flag multiple times by default. Use `[CollectionSyntax(Separator=",")]` for comma-separated input instead. For variadic positionals, use `[Argument] T[]` or `[Argument] params T[]`.

Each element of a collection, including a `T?` element and one read with an `[ArgumentParser]`, is parsed exactly as a single value of its type. A separated value is split with empty parts kept, so the element type decides: `--tags a,,b` gives three strings, while `--ids 1,,3` on an `int[]` is an error. A separator flag can also be given more than once, and an `[Env]` variable (or `UseEnvironmentPrefix`) supplies the value when the flag isn't given, split the same way.

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

### `IParsable<T>` types

On .NET 7 and later, a type that implements `IParsable<T>`, including `ISpanParsable<T>`, binds without a [custom parser](custom-parsers.md). Argh calls its `TryParse` with the configured culture. Number types that also implement `INumberBase<T>`, such as `short` or `BigInteger`, get the configured `NumberStyles` too, `Float` by default. That covers your own types, such as an `OrderId`, and .NET types with no built-in handling, such as `Guid`, `short`, `byte`, `char`, `uint` and `Half`. It works for flags, positionals, nullable values, collections, `[AsParameters]` and options members, and environment-variable fallbacks, and also for types that implement `IParsable<T>` explicitly.

When more than one rule could apply, the first one wins:

1. An `[ArgumentParser]` on the parameter or member.
2. The built-in handling for the types in the table above. It keeps Argh's own formats, such as a `Z` date staying UTC and [durations](#durations) like `5m`.
3. `IParsable<T>`.

Help shows the type name as the placeholder, as in `--order <orderId>`, and an invalid value prints `Error: invalid OrderId for --order: 'x'.` and exits with code 2.

## How it works

The source generator reads your method signatures at build time and emits typed parsers for each parameter. Parameter names are converted from camelCase to kebab-case for CLI flags.

Parameters with default values are optional; those without are required.
