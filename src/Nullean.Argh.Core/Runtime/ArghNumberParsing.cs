using System.ComponentModel;
using Nullean.Argh.Parsing;

namespace Nullean.Argh.Runtime;

/// <summary>
/// Number parsing for generated code, with the configured <see cref="ParserOptions.NumberStyles"/> and culture.
/// Called from generated code; not intended for app code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ArghNumberParsing
{
	/// <summary>Parses an <see cref="int"/>.</summary>
	public static bool TryParseInt32(string? s, ParserOptions options, out int value) =>
		int.TryParse(s, options.NumberStyles, options.Culture, out value);

	/// <summary>Parses a <see cref="long"/>.</summary>
	public static bool TryParseInt64(string? s, ParserOptions options, out long value) =>
		long.TryParse(s, options.NumberStyles, options.Culture, out value);

	/// <summary>Parses a <see cref="float"/>.</summary>
	public static bool TryParseSingle(string? s, ParserOptions options, out float value) =>
		float.TryParse(s, options.NumberStyles, options.Culture, out value);

	/// <summary>Parses a <see cref="double"/>.</summary>
	public static bool TryParseDouble(string? s, ParserOptions options, out double value) =>
		double.TryParse(s, options.NumberStyles, options.Culture, out value);

	/// <summary>Parses a <see cref="decimal"/>.</summary>
	public static bool TryParseDecimal(string? s, ParserOptions options, out decimal value) =>
		decimal.TryParse(s, options.NumberStyles, options.Culture, out value);
}
