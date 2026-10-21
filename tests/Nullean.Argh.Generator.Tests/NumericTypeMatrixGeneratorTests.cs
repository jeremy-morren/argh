using Xunit;
using static Nullean.Argh.Generator.Tests.NumericGeneratorHarness;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// Each numeric type as a method parameter, an <c>[AsParameters]</c> member and a global options member, plain and
/// nullable, with a default, bounded, half-open and unbounded <c>[Range]</c>, and <c>[AllowedValues]</c>/<c>[DeniedValues]</c>:
/// the generated code compiles and every literal carries the type.
/// </summary>
public class NumericTypeMatrixGeneratorTests
{
	/// <summary>
	/// <paramref name="type"/>, a default in source and as emitted, the [Range] limits that mean "unbounded", the bound
	/// literals of <c>[Range(1, 5)]</c>, the literal for the allowed value <c>1</c>, the equality the check uses, and the bound
	/// literals when the other side is the type's limit (which picks RangeAttribute's <c>double</c> constructor when the limit isn't an <c>int</c>).
	/// </summary>
	public static TheoryData<string, string, string, string, string, string, string, string, string> Types => new()
	{
		{ "int", "7", "7", "int.MinValue", "int.MaxValue", "1|5", "1", "==", "1|5" },
		{ "long", "7L", "7L", "long.MinValue", "long.MaxValue", "1|5", "1L", "==", "1d|5d" },
		{ "short", "-7", "(short)(-7)", "short.MinValue", "short.MaxValue", "1|5", "(short)1", "==", "1|5" },
		{ "ushort", "7", "(ushort)7", "ushort.MinValue", "ushort.MaxValue", "1|5", "(ushort)1", "==", "1|5" },
		{ "byte", "7", "(byte)7", "byte.MinValue", "byte.MaxValue", "1|5", "(byte)1", "==", "1|5" },
		{ "sbyte", "-7", "(sbyte)(-7)", "sbyte.MinValue", "sbyte.MaxValue", "1|5", "(sbyte)1", "==", "1|5" },
		{ "uint", "7U", "7U", "uint.MinValue", "uint.MaxValue", "1|5", "1U", "==", "1d|5d" },
		{ "ulong", "7UL", "7UL", "ulong.MinValue", "ulong.MaxValue", "1|5", "1UL", "==", "1d|5d" },
		{ "float", "0.5f", "0.5f", "float.MinValue", "float.MaxValue", "1|5", "1f", "Equals", "1d|5d" },
		{ "double", "0.5", "0.5d", "double.MinValue", "double.MaxValue", "1|5", "1d", "Equals", "1d|5d" },
		{ "decimal", "0.5m", "0.5m", "double.MinValue", "double.MaxValue", "1m|5m", "1m", "Equals", "1m|5m" },
	};

	private static string Source(string type, string sourceDefault, string min, string max) => Usings + $$"""
		namespace App
		{
			public sealed class Opts
			{
				public {{type}} OptDefault { get; set; } = {{sourceDefault}};
				public {{type}}? OptNullable { get; set; } = {{sourceDefault}};
				[Range(1, 5)] public {{type}}? OptRange { get; set; }
				[AllowedValues(1, 2)] public {{type}}? OptAllowed { get; set; }
			}

			public sealed class Dto
			{
				public {{type}} DtoDefault { get; init; } = {{sourceDefault}};
				[Range(1, {{max}})] public {{type}}? DtoAtLeast { get; init; }
				[DeniedValues(3)] public {{type}}? DtoDenied { get; init; }
			}

			public sealed record Rec({{type}} RecDefault = {{sourceDefault}}, [Range({{min}}, 5)] {{type}}? RecAtMost = null);

			public static class Commands
			{
				public static void Run(Opts opts,
					[AsParameters] Dto dto,
					[AsParameters] Rec rec,
					{{type}} plain = {{sourceDefault}},
					{{type}}? nullable = {{sourceDefault}},
					{{type}}? none = null,
					[Range(1, 5)] {{type}} bounded = 3,
					[Range(1, {{max}})] {{type}}? atLeast = null,
					[Range({{min}}, 5)] {{type}}? atMost = null,
					[Range({{min}}, {{max}})] {{type}}? unbounded = null,
					[AllowedValues(1, 2)] {{type}}? allowed = null,
					[DeniedValues(3)] {{type}} denied = 4) { }
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app)
				{
					app.UseGlobalOptions<Opts>();
					app.Map("run", Commands.Run);
				}
			}
		}
		""";

	[Theory]
	[MemberData(nameof(Types))]
	public void Generated_code_compiles(string type, string sourceDefault, string emitted, string min, string max, string bounds, string allowed, string equality, string oneSided)
	{
		_ = (emitted, bounds, allowed, equality, oneSided);
		Generate(Source(type, sourceDefault, min, max), out var output, out var diagnostics);
		Assert.Empty(diagnostics);
		AssertCompiles(output);
	}

	[Theory]
	[MemberData(nameof(Types))]
	public void Defaults_carry_the_type_everywhere(string type, string sourceDefault, string emitted, string min, string max, string bounds, string allowed, string equality, string oneSided)
	{
		_ = (bounds, allowed, equality, oneSided);
		var generated = Generate(Source(type, sourceDefault, min, max), out _, out _);
		Assert.Contains($"{type} __p_plain = {emitted};", generated);
		Assert.Contains($"{type}? __p_nullable = {emitted};", generated);
		Assert.Contains($"{type}? __p_none = default;", generated);
		Assert.Contains($"__p_dto__DtoDefault = {emitted};", generated);
		Assert.Contains($"__p_rec__RecDefault = {emitted};", generated);
		Assert.Contains($"= {emitted};", generated.Substring(generated.IndexOf("OptDefault", StringComparison.Ordinal)));
	}

	[Theory]
	[MemberData(nameof(Types))]
	public void Range_limits_carry_the_type_and_drop_unbounded_sides(string type, string sourceDefault, string emitted, string min, string max, string bounds, string allowed, string equality, string oneSided)
	{
		_ = (emitted, allowed, equality);
		var generated = Generate(Source(type, sourceDefault, min, max), out _, out _);
		var lo = bounds.Split('|')[0];
		var hi = bounds.Split('|')[1];
		Assert.Contains($"__p_bounded < {lo} || __p_bounded > {hi}", generated);
		var oneLo = oneSided.Split('|')[0];
		var oneHi = oneSided.Split('|')[1];
		Assert.Contains($"if (__p_atLeast.HasValue && (__p_atLeast.Value < {oneLo}))", generated);
		Assert.Contains($"if (__p_atMost.HasValue && (__p_atMost.Value > {oneHi}))", generated);
		Assert.Contains($"(__p_dto__DtoAtLeast.Value < {oneLo}))", generated);
		Assert.Contains($"(__p_rec__RecAtMost.Value > {oneHi}))", generated);
		Assert.DoesNotContain("__p_unbounded.Value <", generated);
		Assert.DoesNotContain("__p_unbounded.Value >", generated);
	}

	[Theory]
	[MemberData(nameof(Types))]
	public void Value_lists_use_the_parameter_type_and_Equals_for_floating_point(string type, string sourceDefault, string emitted, string min, string max, string bounds, string allowed, string equality, string oneSided)
	{
		_ = (emitted, bounds, oneSided);
		var generated = Generate(Source(type, sourceDefault, min, max), out _, out _);
		var denied = allowed.Replace("1", "3");
		if (equality == "Equals")
		{
			Assert.Contains($"!__p_allowed.Value.Equals({allowed})", generated);
			Assert.Contains($"__p_denied.Equals({denied})", generated);
			Assert.DoesNotContain("__p_allowed.Value ==", generated);
			Assert.DoesNotContain("__p_allowed.Value !=", generated);
		}
		else
		{
			Assert.Contains($"!(__p_allowed.Value == {allowed})", generated);
			Assert.Contains($"(__p_denied == {denied})", generated);
		}
	}

	public static TheoryData<string, string> GenericMathTypes => new()
	{
		{ "Half", "global::System.Half" },
		{ "Int128", "global::System.Int128" },
		{ "UInt128", "global::System.UInt128" },
		{ "BigInteger", "global::System.Numerics.BigInteger" },
	};

	[Theory]
	[MemberData(nameof(GenericMathTypes))]
	public void Generic_math_types_cast_their_range_bounds(string type, string fq)
	{
		var source = Usings + $$"""
			namespace App
			{
				public static class Commands
				{
					public static void Run(
						[Range(1, 5)] {{type}}? bounded = null,
						[Range(1, double.MaxValue)] {{type}}? atLeast = null,
						[Range(double.MinValue, double.MaxValue)] {{type}}? unbounded = null) { }
				}

				public static class Program
				{
					public static void Configure(IArghBuilder app) => app.Map("run", Commands.Run);
				}
			}
			""";
		var generated = Generate(source, out var output, out var diagnostics);
		Assert.Empty(diagnostics);
		AssertCompiles(output);
		Assert.Contains($"__p_bounded.Value < (({fq})(1)) || __p_bounded.Value > (({fq})(5))", generated);
		Assert.Contains($"if (__p_atLeast.HasValue && (__p_atLeast.Value < (({fq})(1d))))", generated);
		Assert.DoesNotContain("__p_unbounded.Value <", generated);
	}

	[Fact]
	public void Half_allowed_values_cast_and_compare_with_Equals()
	{
		const string source = Usings + """
			namespace App
			{
				public static class Commands
				{
					public static void Run([AllowedValues(0.5, double.NaN)] Half? half = null) { }
				}

				public static class Program
				{
					public static void Configure(IArghBuilder app) => app.Map("run", Commands.Run);
				}
			}
			""";
		var generated = Generate(source, out var output, out var diagnostics);
		Assert.Empty(diagnostics);
		AssertCompiles(output);
		Assert.Contains("!__p_half.Value.Equals((global::System.Half)(0.5d))", generated);
		Assert.Contains("!__p_half.Value.Equals((global::System.Half)(global::System.Double.NaN))", generated);
	}

	[Theory]
	[InlineData("double", "double.NaN", "global::System.Double.NaN")]
	[InlineData("double", "double.PositiveInfinity", "global::System.Double.PositiveInfinity")]
	[InlineData("double", "double.NegativeInfinity", "global::System.Double.NegativeInfinity")]
	[InlineData("double", "double.Epsilon", "5E-324d")]
	[InlineData("double", "double.MaxValue", "1.7976931348623157E+308d")]
	[InlineData("double", "-0.0", "-0d")]
	[InlineData("double", "0.1 + 0.2", "0.30000000000000004d")]
	[InlineData("double", "1e-7", "1E-07d")]
	[InlineData("float", "float.NaN", "global::System.Single.NaN")]
	[InlineData("float", "float.PositiveInfinity", "global::System.Single.PositiveInfinity")]
	[InlineData("float", "float.MaxValue", "3.4028235E+38f")]
	[InlineData("float", "-0.0f", "-0f")]
	[InlineData("float", "0.1f", "0.1f")]
	[InlineData("float", "16777217", "16777216f")]
	[InlineData("decimal", "decimal.MaxValue", "79228162514264337593543950335m")]
	[InlineData("decimal", "decimal.MinValue", "-79228162514264337593543950335m")]
	[InlineData("decimal", "0.10m", "0.10m")]
	[InlineData("decimal", "-0.0000000000000000000000000001m", "-0.0000000000000000000000000001m")]
	[InlineData("decimal", "5", "5m")]
	[InlineData("long", "long.MinValue", "-9223372036854775808L")]
	[InlineData("ulong", "ulong.MaxValue", "18446744073709551615UL")]
	[InlineData("int", "int.MinValue", "-2147483648")]
	[InlineData("sbyte", "sbyte.MinValue", "(sbyte)(-128)")]
	[InlineData("short", "short.MinValue", "(short)(-32768)")]
	[InlineData("char", "'x'", "'x'")]
	[InlineData("char", "'\\\\'", "'\\\\'")]
	[InlineData("char", "'\\0'", "'\\0'")]
	public void Edge_defaults_round_trip_as_valid_literals(string type, string sourceDefault, string emitted)
	{
		var source = Usings + $$"""
			namespace App
			{
				public sealed class Opts
				{
					public {{type}} Opt { get; set; } = {{sourceDefault}};
				}

				public static class Commands
				{
					public static void Run(Opts opts, {{type}} value = {{sourceDefault}}) { }
				}

				public static class Program
				{
					public static void Configure(IArghBuilder app)
					{
						app.UseGlobalOptions<Opts>();
						app.Map("run", Commands.Run);
					}
				}
			}
			""";
		var generated = Generate(source, out var output, out var diagnostics);
		Assert.Empty(diagnostics);
		AssertCompiles(output);
		Assert.Contains($"{type} __p_value = {emitted};", generated);
		Assert.Contains($"= {emitted};", generated.Substring(generated.IndexOf("Opt", StringComparison.Ordinal)));
	}
}
