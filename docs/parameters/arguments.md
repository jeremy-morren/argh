# Arguments (positional)

Mark a parameter with `[Argument]` to make it positional. Indices must start at `0` and be consecutive.

```csharp
public static Task<int> Deploy([Argument] string environment) { … }
// myapp deploy production
```

## Variadic positionals

Combine `[Argument]` with a `T[]` type (or `params T[]`) to collect all remaining tokens into an array. The variadic argument must be the last positional. Zero items is valid.

Because C# requires `params` to be the last method parameter, a variadic positional can appear after flags in the method signature.

```csharp
public static void Copy([Argument] string dest, [Argument] params string[] files) { … }
// myapp copy ./out/ a.txt b.txt "path with spaces/c.txt"
```

Flags can appear before or after variadic tokens on the command line:

```csharp
public static void Archive([Argument] params string[] files, bool verbose = false) { … }
// myapp archive a.zip b.zip --verbose
```

Mixed scalar positional, flags, and variadic arguments work together. Note that `params` must be the last C# parameter:

```csharp
public static void Tag([Argument] string target, bool force, [Argument] params string[] tags) { … }
// myapp tag main-branch --force tag1 tag2 tag3
```

## Extra arguments and `--`

A command takes only the positionals it declares. Any other argument is an error, as in clap:

```
myapp deploy production extra
Error: unexpected argument 'extra' found.
```

To take any number of arguments, declare a variadic positional. A value that starts with `-` reads as an option, so put it after `--`: everything after `--` is positional, even `--help`. A command that declares no positionals rejects anything after `--` too.

This is enough for an `ls`:

```csharp
/// <param name="all">-a, Include hidden entries.</param>
/// <param name="longFormat">-l, Use the long listing format.</param>
/// <param name="paths">Paths to list.</param>
public static void List(bool all = false, bool longFormat = false, [Argument] params string[] paths) { … }

app.MapRoot(List);
// myapp -a -l dir1 dir2 -- -weird-name   → all, longFormat, paths = [dir1, dir2, -weird-name]
```

A [default command](../commands/registration.md#maproothandler) that declares positionals also takes any argument that isn't one of its scope's commands or namespaces, so `myapp dir1` runs it. Use `--` for a positional with a command's name: `myapp -- build`.

## Help output

Variadic positionals appear as `<files...>` / `[<files...>]` in `--help` and include a `[variadic]` annotation.

## Count validation

Apply `[MinLength(n)]` or `[MaxLength(n)]` to validate the item count:

```csharp
public static void Archive([Argument][MinLength(1)][MaxLength(20)] string[] files) { … }
// --help: <files...>   [variadic] [count: 1-20]
```

## Ordering rules

:::{important}
- All `[Argument]` positionals must precede flags in the parameter list (by convention).
- Variadic positionals must be the last positional.
- `params` must be the last method parameter (C# requirement).
- Flags can appear before or after positional tokens on the actual command line.
:::
