# Flags (named options)

Parameters without `[Argument]` become `--kebab-case` long flags. A `bool` flag defaults to `false` (or the parameter's default); pass `--flag` to set it, or `--flag=<value>` to give its value.

```csharp
public static Task<int> Build(string outputDir, bool release = false) { … }
// myapp build --output-dir ./bin --release
```

## Naming convention

C# parameter names are automatically converted from camelCase to kebab-case:

- `outputDir` → `--output-dir`
- `dryRun` → `--dry-run`
- `verbose` → `--verbose`

## Long name override

By default the CLI long name is derived from the C# parameter name. To use a different primary name, place a `--long-name` token before the description in an XML `<param>` tag.

Additional `--names` after the first become aliases. Once an explicit long name is specified, the derived name is dropped entirely.

```csharp
/// <summary>Tag one or more resources.</summary>
/// <param name="tags">-t, --tag, Tags to apply.</param>
public static void Tag(string[] tags) { … }
```

```
--tag a --tag b    ✓  (explicit primary name)
-t a               ✓  (short alias)
--tags             ✗  (derived name is dropped)
```

```csharp
/// <param name="outputDir">-o, --out, --output, Output directory.</param>
public static void Build(string outputDir) { … }
```

```
--out ./bin         ✓  (primary)
--output ./bin      ✓  (alias)
-o ./bin            ✓  (short alias)
--output-dir        ✗  (derived name is dropped)
```

:::{tip}
This also works on `[AsParameters]` properties, fields, and primary-constructor parameters via their `<summary>` or `<param>` doc lines.
:::

## Nullable bool - `--flag` / `--no-flag` pairs

A `bool?` flag generates **both** `--flag` (sets `true`) and `--no-flag` (sets `false`). Omitting either leaves the value `null`, letting you distinguish "not specified" from an explicit false.

Help output shows `--flag / --no-flag` for nullable bools.

```csharp
public static Task<int> Deploy(string env, bool? dryRun = null) { … }
// myapp deploy staging               → dryRun is null
// myapp deploy staging --dry-run     → dryRun is true
// myapp deploy staging --no-dry-run  → dryRun is false
```

## Boolean values

A boolean value on the command line, such as `--flag=<value>`, `-f=<value>` or a `bool` argument, is one of these, ignoring case. Anything else is an error.

| `true` | `false` |
|---|---|
| `y`, `yes`, `t`, `true`, `on`, `1` | `n`, `no`, `f`, `false`, `off`, `0` |

`--no-flag` takes no value, like git: `--no-flag=true`, `--no-flag=false` and `--no-flag=` are errors. A boolean environment variable (`[Env]`) is `false` when it's one of the `false` spellings and `true` for anything else; an empty one is unset, or `false` with `TreatEmptyAsUnset = false`. These are clap's `BoolishValueParser` and `FalseyValueParser`.

The command line wins over the environment variable, wherever the flag is given; the environment variable wins over the default.

## Required vs optional

Parameters without a default value are required flags. Parameters with a default value are optional.

```csharp
public static Task<int> Deploy(
    string environment,           // required: --environment <value>
    bool dryRun = false,          // optional: --dry-run
    string? region = null)        // optional: --region <value>
{ … }
```

## Collections

Collection flags (`T[]`, `List<T>`) accept the flag multiple times by default. Use `[CollectionSyntax]` for comma-separated values instead.

```csharp
public static Task<int> Deploy(string[] targets, [CollectionSyntax(Separator = ",")] string[] tags) { … }
// Repeated:   myapp deploy --targets web --targets api
// Separator:  myapp deploy --targets web,api --tags blue,green
```

Each element is parsed exactly as a single value of the element type, by the same parser, and a separator flag can be given more than once too. Empty parts are kept: `--tags blue,,green` is three strings, and `--ids 1,,3` on an `int[]` is an error, as `--id ""` is on an `int`.
