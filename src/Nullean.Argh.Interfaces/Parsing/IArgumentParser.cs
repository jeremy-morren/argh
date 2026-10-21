namespace Nullean.Argh.Parsing;

/// <summary>Parses a CLI string into a custom type (implemented by the user; invoked from generated code).</summary>
public interface IArgumentParser<T>
{
	/// <summary>Parses <paramref name="raw"/>, the value given on the command line or in an environment variable.</summary>
	/// <param name="raw">The value to parse.</param>
	/// <param name="options">
	/// The parser options set with <c>UseParserOptions</c> (invariant culture by default). Use its culture and number styles
	/// for any culture-sensitive parsing, so custom types parse like the built-in ones.
	/// </param>
	/// <param name="value">The parsed value.</param>
	/// <returns><see langword="true"/> when <paramref name="raw"/> is valid.</returns>
	bool TryParse(string raw, ParserOptions options, out T value);
}
