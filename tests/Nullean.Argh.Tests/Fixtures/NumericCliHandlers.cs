using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Numerics;
using Nullean.Argh;
using Nullean.Argh.Tests.ReferencedDtos;

namespace Nullean.Argh.Tests.Fixtures;

/// <summary>An enum whose members aren't 0, 1, 2.</summary>
internal enum FixtureLevel
{
	Low = 0,
	Mid = 5,
	High = 10,
}

/// <summary>A [Flags] enum, so a default can combine members that have no name of their own.</summary>
[Flags]
internal enum FixtureAccess
{
	None = 0,
	Read = 1,
	Write = 2,
	Execute = 4,
}

/// <summary>[AsParameters] DTO whose property initializers need typed literals.</summary>
internal sealed class NumericDefaultsArgs
{
	/// <summary>A decimal default.</summary>
	public decimal Money { get; init; } = 1.25m;

	/// <summary>A float default.</summary>
	public float Factor { get; init; } = 0.5f;

	/// <summary>A non-finite default.</summary>
	public double Limit { get; init; } = double.PositiveInfinity;

	/// <summary>A half-open range on a DTO member.</summary>
	[Range(1, long.MaxValue)]
	public long? Budget { get; init; }

	/// <summary>A byte default on a DTO member.</summary>
	public byte Retries { get; init; } = 3;

	/// <summary>An enum default that isn't 0 on a DTO member.</summary>
	public FixtureLevel Grade { get; init; } = FixtureLevel.Mid;

	/// <summary>Decimal allowed values on a DTO member.</summary>
	[AllowedValues(0.1, 0.2)]
	public decimal? Tick { get; init; }
}

/// <summary>Namespace options whose defaults and limits need typed literals.</summary>
internal sealed class NumericNamespaceOptions : TestGlobalCliOptions
{
	/// <summary>A decimal default on an options member.</summary>
	public decimal Fee { get; set; } = 0.25m;

	/// <summary>A float default on an options member.</summary>
	public float Gain { get; set; } = 1.5f;

	/// <summary>A NaN default on an options member.</summary>
	public double Missing { get; set; } = double.NaN;

	/// <summary>A ulong default on an options member.</summary>
	public ulong Big { get; set; } = 18446744073709551615UL;

	/// <summary>A short default on an options member.</summary>
	public short Offset { get; set; } = -7;

	/// <summary>An enum default that isn't 0 on an options member.</summary>
	public FixtureLevel Tier { get; set; } = FixtureLevel.High;

	/// <summary>Only an upper bound on an options member: the lower one is double.MinValue.</summary>
	[Range(double.MinValue, 100)]
	public double? Ceiling { get; set; }

	/// <summary>Float allowed values on an options member.</summary>
	[AllowedValues(0.1, 0.5)]
	public float? Step { get; set; }

	/// <summary>Both bounds on an options member, read from ARGH_TEST_NS_SHARDS when not given.</summary>
	[Env("ARGH_TEST_NS_SHARDS")]
	[Range(1, 10)]
	public int? Shards { get; set; }

	/// <summary>A denied value on an options member.</summary>
	[DeniedValues(13)]
	public int? Lucky { get; set; }

	/// <summary>Bounds parsed from text on an options member.</summary>
	[Range(typeof(double), "0.5", "1.5")]
	public double? Ratio { get; set; }

	/// <summary>Both bounds unbounded on an options member: no check at all.</summary>
	[Range(long.MinValue, long.MaxValue)]
	public long? Anything { get; set; }

	/// <summary>A string length on an options member.</summary>
	[StringLength(5, MinimumLength = 2)]
	public string? Tag { get; set; }
}

internal static class NumericCliHandlers
{
	/// <summary>Numeric test: unbounded and half-open [Range] limits per type.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="atLeast">Only a lower bound: the upper one is Single.MaxValue.</param>
	/// <param name="atMost">Only an upper bound: the lower one is Int64.MinValue.</param>
	/// <param name="ratio">The int constructor on a double.</param>
	/// <param name="amount">Decimal bounds kept exact from the source text.</param>
	/// <param name="price">A decimal with double.MaxValue as its upper bound.</param>
	/// <param name="fee">Bounds parsed from text at build time.</param>
	/// <param name="any">Both bounds unbounded: no check at all.</param>
	/// <param name="count">An unsigned upper bound of uint.MaxValue.</param>
	/// <param name="level">A byte with only a lower bound.</param>
	/// <param name="half">A Half with only a lower bound.</param>
	/// <param name="low">A short with only an upper bound.</param>
	/// <param name="tiny">An sbyte with only a lower bound.</param>
	/// <param name="port">A ushort with both bounds.</param>
	/// <param name="huge">An Int128 with double.MaxValue as its upper bound.</param>
	/// <param name="whole">A BigInteger with both bounds.</param>
	/// <param name="cap">A decimal whose upper bound is decimal.MaxValue as text.</param>
	/// <param name="open">An int with int.MinValue and int.MaxValue: no check at all.</param>
	public static void NumericRanges(TestGlobalCliOptions g,
		[Range(4, Single.MaxValue)] float? atLeast = null,
		[Range(Int64.MinValue, 10)] long? atMost = null,
		[Range(0, 10)] double? ratio = null,
		[Range(0.1, 0.3)] decimal? amount = null,
		[Range(0, double.MaxValue)] decimal? price = null,
		[Range(typeof(decimal), "1.5", "2.25")] decimal? fee = null,
		[Range(double.MinValue, double.MaxValue)] double? any = null,
		[Range(1, uint.MaxValue)] uint? count = null,
		[Range(1, byte.MaxValue)] byte? level = null,
		[Range(-1, 65504)] Half? half = null,
		[Range(short.MinValue, 100)] short? low = null,
		[Range(-10, sbyte.MaxValue)] sbyte? tiny = null,
		[Range(10, 20)] ushort? port = null,
		[Range(0, double.MaxValue)] Int128? huge = null,
		[Range(-5, 5)] BigInteger? whole = null,
		[Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal? cap = null,
		[Range(int.MinValue, int.MaxValue)] int? open = null)
	{
		var inv = CultureInfo.InvariantCulture;
		Console.Out.WriteLine(string.Join("|",
			atLeast?.ToString(inv), atMost?.ToString(inv), ratio?.ToString(inv), amount?.ToString(inv), price?.ToString(inv),
			fee?.ToString(inv), any?.ToString(inv), count?.ToString(inv), level?.ToString(inv), half?.ToString(inv),
			low?.ToString(inv), tiny?.ToString(inv), port?.ToString(inv), huge?.ToString(inv), whole?.ToString(inv),
			cap?.ToString(inv), open?.ToString(inv)));
	}

	/// <summary>Numeric test: defaults that need typed literals.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="args">Defaults from property initializers.</param>
	/// <param name="rate">A decimal default.</param>
	/// <param name="scale">A float default.</param>
	/// <param name="gain">A nullable float default.</param>
	/// <param name="missing">A NaN default.</param>
	/// <param name="floor">A negative infinity default.</param>
	/// <param name="separator">A char default.</param>
	/// <param name="offset">A negative short default.</param>
	/// <param name="big">A ulong default.</param>
	/// <param name="tier">An enum default that isn't 0.</param>
	/// <param name="pick">NaN is one of the allowed values.</param>
	/// <param name="avoid">An int constant denied on a double.</param>
	/// <param name="count">An int default.</param>
	/// <param name="total">A negative long default.</param>
	/// <param name="mask">A uint default.</param>
	/// <param name="width">A ushort default.</param>
	/// <param name="shade">A byte default.</param>
	/// <param name="delta">A negative sbyte default.</param>
	/// <param name="most">decimal.MaxValue as a default.</param>
	/// <param name="negZero">Negative zero as a default.</param>
	/// <param name="maybeRate">A nullable decimal default.</param>
	/// <param name="maybeTier">A nullable enum without a value.</param>
	/// <param name="setTier">A nullable enum with a value.</param>
	/// <param name="access">A [Flags] combination with no member of its own.</param>
	/// <param name="crossLevel">An enum from another assembly, not its first member.</param>
	/// <param name="quote">A char default that C# escapes.</param>
	public static void NumericDefaults(TestGlobalCliOptions g,
		[AsParameters] NumericDefaultsArgs args,
		decimal rate = 1.5m,
		float scale = 1.5f,
		float? gain = 2.5f,
		double missing = double.NaN,
		double floor = double.NegativeInfinity,
		char separator = ',',
		short offset = -5,
		ulong big = ulong.MaxValue,
		FixtureLevel tier = FixtureLevel.High,
		[AllowedValues(double.NaN, 1.5)] double? pick = null,
		[DeniedValues(1)] double? avoid = null,
		int count = 7,
		long total = -5L,
		uint mask = 5U,
		ushort width = 8080,
		byte shade = 200,
		sbyte delta = -5,
		decimal most = 79228162514264337593543950335m,
		double negZero = -0.0,
		decimal? maybeRate = 2.5m,
		FixtureLevel? maybeTier = null,
		FixtureLevel? setTier = FixtureLevel.Mid,
		FixtureAccess access = FixtureAccess.Read | FixtureAccess.Write,
		CrossAssemblyLevel crossLevel = CrossAssemblyLevel.Warning,
		char quote = '\'')
	{
		var inv = CultureInfo.InvariantCulture;
		Console.Out.WriteLine(string.Join("|",
			args.Money.ToString(inv), args.Factor.ToString(inv), args.Limit.ToString(inv), args.Budget?.ToString(inv),
			rate.ToString(inv), scale.ToString(inv), gain?.ToString(inv), missing.ToString(inv), floor.ToString(inv),
			separator, offset.ToString(inv), big.ToString(inv), tier, pick?.ToString(inv), avoid?.ToString(inv)));
		Console.Out.WriteLine(string.Join("|",
			args.Retries.ToString(inv), args.Grade, args.Tick?.ToString(inv) ?? "none",
			count.ToString(inv), total.ToString(inv), mask.ToString(inv), width.ToString(inv), shade.ToString(inv),
			delta.ToString(inv), most.ToString(inv), negZero.ToString(inv), maybeRate?.ToString(inv),
			maybeTier?.ToString() ?? "null", setTier?.ToString() ?? "null", ((int)access).ToString(inv), crossLevel, quote));
	}

	/// <summary>Numeric test: allowed and denied values per type.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="ratio">Float allowed values written as doubles.</param>
	/// <param name="amount">Decimal allowed values written as doubles.</param>
	/// <param name="size">Short allowed values.</param>
	/// <param name="code">Byte denied values.</param>
	/// <param name="id">Long denied values written as ints.</param>
	/// <param name="ceiling">Infinity is denied.</param>
	public static void NumericChoices(TestGlobalCliOptions g,
		[AllowedValues(0.1, 0.25)] float? ratio = null,
		[AllowedValues(0.1, 0.3)] decimal? amount = null,
		[AllowedValues((short)-1, (short)1)] short? size = null,
		[DeniedValues((byte)0, (byte)255)] byte? code = null,
		[DeniedValues(0)] long? id = null,
		[DeniedValues(double.PositiveInfinity)] double? ceiling = null)
	{
		var inv = CultureInfo.InvariantCulture;
		Console.Out.WriteLine(string.Join("|",
			ratio?.ToString(inv), amount?.ToString(inv), size?.ToString(inv), code?.ToString(inv), id?.ToString(inv),
			ceiling?.ToString(inv)));
	}

	/// <summary>Prints the numeric namespace options.</summary>
	/// <param name="o">The namespace options.</param>
	public static void NumericNsShow(NumericNamespaceOptions o)
	{
		var inv = CultureInfo.InvariantCulture;
		Console.Out.WriteLine(string.Join("|",
			o.Fee.ToString(inv), o.Gain.ToString(inv), o.Missing.ToString(inv), o.Big.ToString(inv), o.Offset.ToString(inv),
			o.Tier, o.Ceiling?.ToString(inv) ?? "none", o.Step?.ToString(inv) ?? "none", o.Shards?.ToString(inv) ?? "none",
			o.Lucky?.ToString(inv) ?? "none", o.Ratio?.ToString(inv) ?? "none", o.Anything?.ToString(inv) ?? "none", o.Tag ?? "none",
			o.Quota?.ToString(inv) ?? "none", o.Zone ?? "none", o.Weight?.ToString(inv) ?? "none"));
	}
}
