using System.Globalization;

namespace Nullean.Argh;

/// <summary>
/// Parses CLI <see cref="TimeSpan"/> values: a <see cref="HumanTime"/> duration such as <c>90s</c>, <c>1h30m</c> or
/// <c>1.5h</c>, or a value with a <c>:</c> in <see cref="TimeSpan"/>'s own format, such as <c>00:05:00</c> or
/// <c>1.02:03:04</c>.
/// </summary>
/// <remarks>
/// The two forms can't be confused: a <see cref="HumanTime"/> duration never contains <c>:</c>, and every
/// <see cref="TimeSpan"/> format but a bare number of days does. A bare number such as <c>10</c> is neither, because
/// it's unclear what unit it's in; only <c>0</c> parses.
/// </remarks>
#if ARGH_GENERATOR
internal
#else
public
#endif
static class ArghTimeSpan
{
	/// <inheritdoc cref="TryParse(string?, IFormatProvider?, out TimeSpan)"/>
	public static bool TryParse(string? s, out TimeSpan value) =>
		TryParse(s, CultureInfo.InvariantCulture, out value);

	/// <summary>
	/// Parses a duration: <see cref="TimeSpan.TryParse(string?, IFormatProvider?, out TimeSpan)"/> with
	/// <paramref name="formatProvider"/> when <paramref name="s"/> contains <c>:</c>, otherwise
	/// <see cref="HumanTime.TryParse(string?, IFormatProvider?, out TimeSpan)"/>.
	/// </summary>
	public static bool TryParse(string? s, IFormatProvider? formatProvider, out TimeSpan value) =>
		TryParse(s, formatProvider, out value, out _);

	/// <summary>
	/// Like <see cref="TryParse(string?, IFormatProvider?, out TimeSpan)"/>, and gives the reason a value doesn't parse
	/// in <paramref name="error"/>, a sentence that error messages can show after the value.
	/// </summary>
	public static bool TryParse(string? s, IFormatProvider? formatProvider, out TimeSpan value, out string? error)
	{
		if (s is null || s.IndexOf(':') < 0)
			return HumanTime.TryParse(s, formatProvider, out value, out error);

		if (TimeSpan.TryParse(s, formatProvider, out value))
		{
			error = null;
			return true;
		}

		error = "Expected [-][d.]hh:mm[:ss[.fffffff]], or a duration such as 1h30m.";
		return false;
	}
}
