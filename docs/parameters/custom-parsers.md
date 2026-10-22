# Custom parsing with `IArgumentParser<T>`

For types with no built-in support, implement `IArgumentParser<T>` and annotate the parameter with `[ArgumentParser]`.

## Implementing a parser

```csharp
public class SemVerParser : IArgumentParser<SemVer>
{
    public bool TryParse(string value, ParserOptions options, out SemVer result) =>
        SemVer.TryParse(value, out result);
}
```

`options` holds the [parser options](index.md#parser-options) set with `UseParserOptions`: `options.Culture`, `CultureInfo.InvariantCulture` by default, `options.NumberStyles`, `NumberStyles.Float` by default, and `options.DateTimeStyles`. Pass them to any culture-sensitive parsing, such as numbers or dates, so your type parses the same way as the built-in ones.

## Using the parser

Annotate the parameter with `[ArgumentParser(typeof(...))]`:

```csharp
public static Task<int> Release([ArgumentParser(typeof(SemVerParser))] SemVer version) { … }
// myapp release 1.2.3
```

A parser for a struct `T` also binds `T?`: the value is `null` when the option is absent. If the parser implements both `IArgumentParser<T>` and `IArgumentParser<T?>`, a `T?` uses the `IArgumentParser<T?>` implementation.

On a collection, a parser for the element type parses each element, as it would a single value: `[ArgumentParser(typeof(SemVerParser))] SemVer[] versions` (or `List<SemVer?>`) takes `--versions 1.2.3 --versions 2.0.0`. A parser that implements `IArgumentParser` of the collection type itself parses the whole value instead.

## Types that implement `IParsable<T>`

A type that implements `IParsable<T>` doesn't need a parser on .NET 7 and later: Argh calls its `TryParse` (see [`IParsable<T>` types](index.md#iparsablet-types)). An `[ArgumentParser]` still wins when the parameter has one, for example to accept a different format than the type's own.

## Package reference

`IArgumentParser<T>` is defined in `Nullean.Argh.Interfaces`.

:::{tip}
If you are building a shared library of custom parsers for reuse across multiple CLI apps, reference the interfaces package directly:

```xml
<ItemGroup>
  <PackageReference Include="Nullean.Argh.Interfaces" />
</ItemGroup>
```

For normal apps that reference `Nullean.Argh` or `Nullean.Argh.Hosting`, the interfaces are already available transitively.
:::
