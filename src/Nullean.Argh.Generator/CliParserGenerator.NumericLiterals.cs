using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Nullean.Argh;

public sealed partial class CliParserGenerator
{
	/// <summary>
	/// A C# literal for a numeric constant that keeps its exact type, so boxing or overload resolution can't change it:
	/// a suffix where C# has one (<c>1.5d</c>, <c>1.5f</c>, <c>1.5m</c>, <c>5L</c>, <c>5UL</c>, <c>5U</c>), a cast where it
	/// doesn't (<c>(byte)5</c>, <c>(short)(-5)</c>), and the type's named constant for non-finite values
	/// (<c>global::System.Double.NaN</c>). Formatted with the invariant culture at round-trip precision.
	/// Returns <see langword="null"/> for a value that isn't a C# numeric primitive.
	/// </summary>
	private static string? FormatNumericLiteral(object? value) =>
		value switch
		{
			int i => i.ToString(CultureInfo.InvariantCulture),
			long l => l.ToString(CultureInfo.InvariantCulture) + "L",
			uint u => u.ToString(CultureInfo.InvariantCulture) + "U",
			ulong ul => ul.ToString(CultureInfo.InvariantCulture) + "UL",
			short s => CastLiteral("short", s.ToString(CultureInfo.InvariantCulture)),
			ushort us => CastLiteral("ushort", us.ToString(CultureInfo.InvariantCulture)),
			byte b => CastLiteral("byte", b.ToString(CultureInfo.InvariantCulture)),
			sbyte sb => CastLiteral("sbyte", sb.ToString(CultureInfo.InvariantCulture)),
			float f => FloatLiteral(f),
			double d => DoubleLiteral(d),
			decimal m => m.ToString(CultureInfo.InvariantCulture) + "m",
			_ => null,
		};

	/// <summary>The C# keyword of a numeric constant's type, or <see langword="null"/>.</summary>
	private static string? NumericKeyword(object? value) =>
		value switch
		{
			int => "int",
			long => "long",
			uint => "uint",
			ulong => "ulong",
			short => "short",
			ushort => "ushort",
			byte => "byte",
			sbyte => "sbyte",
			float => "float",
			double => "double",
			decimal => "decimal",
			_ => null,
		};

	/// <summary>Whether values of this numeric keyword type are compared with <c>.Equals</c> rather than <c>==</c>.</summary>
	private static bool IsFloatingKeyword(string keyword) => keyword is "float" or "double" or "decimal";

	private static string CastLiteral(string keyword, string text) =>
		text.StartsWith("-", StringComparison.Ordinal) ? $"({keyword})({text})" : $"({keyword}){text}";

	private static string DoubleLiteral(double d)
	{
		if (double.IsNaN(d))
			return "global::System.Double.NaN";
		if (double.IsPositiveInfinity(d))
			return "global::System.Double.PositiveInfinity";
		if (double.IsNegativeInfinity(d))
			return "global::System.Double.NegativeInfinity";
		if (d.Equals(0d) && BitConverter.DoubleToInt64Bits(d) < 0)
			return "-0d";
		var text = d.ToString("R", CultureInfo.InvariantCulture);
		if (!double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture).Equals(d))
			text = d.ToString("G17", CultureInfo.InvariantCulture);
		return text + "d";
	}

	private static string FloatLiteral(float f)
	{
		if (float.IsNaN(f))
			return "global::System.Single.NaN";
		if (float.IsPositiveInfinity(f))
			return "global::System.Single.PositiveInfinity";
		if (float.IsNegativeInfinity(f))
			return "global::System.Single.NegativeInfinity";
		if (f.Equals(0f) && BitConverter.DoubleToInt64Bits(f) < 0)
			return "-0f";
		var text = f.ToString("R", CultureInfo.InvariantCulture);
		if (!float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture).Equals(f))
			text = f.ToString("G9", CultureInfo.InvariantCulture);
		return text + "f";
	}

	private static readonly Regex NumericLiteralPattern = new(
		@"^(?:\((?<cast>[a-z]+)\)\(?(?<castValue>-?[0-9][0-9.E+\-]*)\)?|(?<value>-?[0-9][0-9._E+\-]*?)(?<suffix>UL|ul|L|l|U|u|m|M|d|D|f|F)?)$",
		RegexOptions.CultureInvariant);

	/// <summary>
	/// The invariant text of a literal made by <see cref="FormatNumericLiteral"/> (no suffix or cast; <c>NaN</c>,
	/// <c>Infinity</c>, <c>-Infinity</c> for non-finite values), for help and <c>__schema</c>. Other text is returned as is.
	/// </summary>
	private static string NumericLiteralText(string literal)
	{
		var trimmed = literal.Trim();
		if (trimmed.EndsWith(".NaN", StringComparison.Ordinal))
			return "NaN";
		if (trimmed.EndsWith(".PositiveInfinity", StringComparison.Ordinal))
			return "Infinity";
		if (trimmed.EndsWith(".NegativeInfinity", StringComparison.Ordinal))
			return "-Infinity";
		var match = NumericLiteralPattern.Match(trimmed);
		if (!match.Success)
			return literal;
		return match.Groups["cast"].Success ? match.Groups["castValue"].Value : match.Groups["value"].Value.Replace("_", "");
	}

	/// <summary>
	/// Like <see cref="ConvertNumericExactly"/>, but a finite <c>double</c> constant becomes the nearest <c>float</c> for a
	/// <c>float</c> target, since C# has no <c>float</c> attribute constant to write instead (<c>0.1</c> means <c>0.1f</c>).
	/// </summary>
	private static object? ConvertNumericConstant(object? value, ITypeSymbol? target) =>
		ConvertNumericExactly(value, target)
		?? (value is double d && target?.SpecialType == SpecialType.System_Single && Math.Abs(d) <= float.MaxValue ? (float)d : null);

	/// <summary>
	/// <paramref name="value"/> converted to the numeric type <paramref name="target"/> when that loses nothing (checked by
	/// converting back), otherwise <see langword="null"/>. Only C# numeric primitive targets are supported.
	/// </summary>
	private static object? ConvertNumericExactly(object? value, ITypeSymbol? target)
	{
		if (value is null || target is null || NumericKeyword(value) is null)
			return null;
		var clrType = ClrNumericType(target);
		if (clrType is null)
			return null;
		if (clrType == value.GetType())
			return value;
		try
		{
			if (value is double or float && clrType == typeof(decimal))
			{
				var asDouble = Convert.ToDouble(value, CultureInfo.InvariantCulture);
				if (double.IsNaN(asDouble) || double.IsInfinity(asDouble))
					return null;
			}
			var converted = Convert.ChangeType(value, clrType, CultureInfo.InvariantCulture);
			var back = Convert.ChangeType(converted, value.GetType(), CultureInfo.InvariantCulture);
			return back.Equals(value) ? converted : null;
		}
		catch (Exception e) when (e is OverflowException or InvalidCastException or FormatException)
		{
			return null;
		}
	}

	/// <summary>
	/// Parses <paramref name="text"/> as the C# numeric primitive <paramref name="target"/> with <see cref="NumberStyles.Float"/>
	/// and the invariant culture, the defaults for command-line input, so <c>"1e3"</c> and <c>"1.0"</c> are valid integers and
	/// <c>"1.5"</c> isn't. Returns <see langword="null"/> when it isn't a number of that type.
	/// </summary>
	private static object? ParseNumericInvariant(string text, ITypeSymbol target)
	{
		const NumberStyles styles = NumberStyles.Float;
		var culture = CultureInfo.InvariantCulture;
		try
		{
			return ClrNumericType(target) switch
			{
				{ } t when t == typeof(int) => int.Parse(text, styles, culture),
				{ } t when t == typeof(long) => long.Parse(text, styles, culture),
				{ } t when t == typeof(uint) => uint.Parse(text, styles, culture),
				{ } t when t == typeof(ulong) => ulong.Parse(text, styles, culture),
				{ } t when t == typeof(short) => short.Parse(text, styles, culture),
				{ } t when t == typeof(ushort) => ushort.Parse(text, styles, culture),
				{ } t when t == typeof(byte) => byte.Parse(text, styles, culture),
				{ } t when t == typeof(sbyte) => sbyte.Parse(text, styles, culture),
				{ } t when t == typeof(float) => float.Parse(text, styles, culture),
				{ } t when t == typeof(double) => double.Parse(text, styles, culture),
				{ } t when t == typeof(decimal) => decimal.Parse(text, styles, culture),
				_ => null,
			};
		}
		catch (Exception e) when (e is OverflowException or FormatException)
		{
			return null;
		}
	}

	private static Type? ClrNumericType(ITypeSymbol target) =>
		target.SpecialType switch
		{
			SpecialType.System_Int32 => typeof(int),
			SpecialType.System_Int64 => typeof(long),
			SpecialType.System_UInt32 => typeof(uint),
			SpecialType.System_UInt64 => typeof(ulong),
			SpecialType.System_Int16 => typeof(short),
			SpecialType.System_UInt16 => typeof(ushort),
			SpecialType.System_Byte => typeof(byte),
			SpecialType.System_SByte => typeof(sbyte),
			SpecialType.System_Single => typeof(float),
			SpecialType.System_Double => typeof(double),
			SpecialType.System_Decimal => typeof(decimal),
			_ => null,
		};
}
