namespace Nullean.Argh;

/// <summary>
/// Reads boolean values the way clap does: <see cref="TryParseBoolish"/> for a value given on the command line
/// (clap's <c>BoolishValueParser</c>) and <see cref="ParseFalsey"/> for an environment variable (clap's
/// <c>FalseyValueParser</c>).
/// </summary>
/// <remarks>
/// The spellings are clap's <c>TRUE_LITERALS</c> and <c>FALSE_LITERALS</c>, matched ignoring ASCII case, as clap matches
/// them after <c>to_lowercase()</c>; no other character lowercases to one of them. See
/// <see href="https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/util/str_to_bool.rs#L1-L21">str_to_bool.rs</see>.
/// </remarks>
public static class ArghBool
{
	// clap v4.6.7 clap_builder/src/util/str_to_bool.rs: TRUE_LITERALS and FALSE_LITERALS.
	private static readonly string[] TrueLiterals = ["y", "yes", "t", "true", "on", "1"];
	private static readonly string[] FalseLiterals = ["n", "no", "f", "false", "off", "0"];

	/// <summary>
	/// Parses a command-line boolean: <c>y</c>, <c>yes</c>, <c>t</c>, <c>true</c>, <c>on</c> or <c>1</c> is
	/// <see langword="true"/>; <c>n</c>, <c>no</c>, <c>f</c>, <c>false</c>, <c>off</c> or <c>0</c> is
	/// <see langword="false"/>, ignoring case. Anything else, including an empty value, doesn't parse.
	/// </summary>
	/// <remarks>
	/// clap's <c>BoolishValueParser</c>
	/// (<see href="https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/builder/value_parser.rs#L1894-L1924">value_parser.rs</see>).
	/// </remarks>
	public static bool TryParseBoolish(string? s, out bool value)
	{
		value = false;
		if (s is null)
			return false;
		if (Matches(s, TrueLiterals))
		{
			value = true;
			return true;
		}

		return Matches(s, FalseLiterals);
	}

	/// <summary>
	/// Reads an environment-variable boolean: <see langword="false"/> when the value is unset, empty, or <c>n</c>,
	/// <c>no</c>, <c>f</c>, <c>false</c>, <c>off</c> or <c>0</c> ignoring case; <see langword="true"/> for anything else.
	/// </summary>
	/// <remarks>
	/// clap's <c>FalseyValueParser</c>
	/// (<see href="https://github.com/clap-rs/clap/blob/v4.6.7/clap_builder/src/builder/value_parser.rs#L1795-L1823">value_parser.rs</see>).
	/// </remarks>
	public static bool ParseFalsey(string? s) => !string.IsNullOrEmpty(s) && !Matches(s!, FalseLiterals);

	private static bool Matches(string s, string[] lowercaseLiterals)
	{
		foreach (var literal in lowercaseLiterals)
		{
			if (EqualsIgnoreAsciiCase(s, literal))
				return true;
		}

		return false;
	}

	private static bool EqualsIgnoreAsciiCase(string s, string lowercaseLiteral)
	{
		if (s.Length != lowercaseLiteral.Length)
			return false;
		for (var i = 0; i < s.Length; i++)
		{
			var c = s[i];
			if (c is >= 'A' and <= 'Z')
				c = (char)(c + ('a' - 'A'));
			if (c != lowercaseLiteral[i])
				return false;
		}

		return true;
	}
}
