using System.Globalization;

namespace Nullean.Argh;

/// <summary>
/// Parses CLI <see cref="TimeSpan"/> values: compact <c>Ns</c>/<c>Nm</c>/<c>Nh</c>/<c>Nd</c> (integer, no fraction) or invariant <see cref="TimeSpan.TryParse(string?, IFormatProvider?, out TimeSpan)"/>.
/// </summary>
public static class ArghTimeSpan
{
	/// <inheritdoc cref="TryParse(string?, IFormatProvider?, out TimeSpan)"/>
	public static bool TryParse(string? s, out TimeSpan value) =>
		TryParse(s, CultureInfo.InvariantCulture, out value);

	/// <summary>
	/// Parses a duration: compact form is one or more ASCII digits followed by <c>s</c>, <c>m</c>, <c>h</c>, or <c>d</c> (case-insensitive); otherwise invariant <see cref="TimeSpan"/> parsing.
	/// </summary>
	public static bool TryParse(string? s, IFormatProvider? formatProvider, out TimeSpan value)
	{
		value = default;
		if (string.IsNullOrWhiteSpace(s))
			return false;

		var trimmed = s!.Trim();
		if (trimmed.Length < 2)
			return TimeSpan.TryParse(trimmed, formatProvider, out value);

		var last = trimmed[trimmed.Length - 1];
		var u = char.ToLowerInvariant(last);
		if (u is not ('s' or 'm' or 'h' or 'd'))
			return TimeSpan.TryParse(trimmed, formatProvider, out value);

		for (var i = 0; i < trimmed.Length - 1; i++)
		{
			if (!char.IsDigit(trimmed[i]))
				return TimeSpan.TryParse(trimmed, formatProvider, out value);
		}

		// The span overload (.NET Core 2.1, .NET Standard 2.1) parses the digits without allocating a substring.
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
		var numPart = trimmed.AsSpan(0, trimmed.Length - 1);
#else
		var numPart = trimmed.Substring(0, trimmed.Length - 1);
#endif
		if (!ulong.TryParse(numPart, NumberStyles.None, formatProvider, out var n))
			return TimeSpan.TryParse(trimmed, formatProvider, out value);

		try
		{
			switch (u)
			{
				case 's':
					value = TimeSpan.FromSeconds(n);
					return true;
				case 'm':
					value = TimeSpan.FromMinutes(n);
					return true;
				case 'h':
					value = TimeSpan.FromHours(n);
					return true;
				case 'd':
					value = TimeSpan.FromDays(n);
					return true;
				default:
					return TimeSpan.TryParse(trimmed, formatProvider, out value);
			}
		}
		catch (OverflowException)
		{
			return TimeSpan.TryParse(trimmed, formatProvider, out value);
		}
	}
}
