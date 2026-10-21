using System.Globalization;
using Microsoft.CodeAnalysis;
using Xunit;
using static Nullean.Argh.Generator.Tests.NumericGeneratorHarness;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// Every number the generator writes into C# carries its exact type, <c>[Range]</c> bounds at a type's own limits are
/// unbounded, and <c>[Range(typeof(T), "min", "max")]</c> text is parsed at build time with the invariant culture.
/// </summary>
public class NumericLiteralGeneratorTests
{
	private const string LiteralsSource = Usings + """
		namespace App
		{
			public enum Level { Low = 0, Mid = 5, High = 10 }

			public static class Commands
			{
				public static void Run(
					decimal dec = 1.5m, float flt = 1.5f, double dbl = 1.5, long lng = 5L, ulong ulg = 5UL, uint uin = 5U,
					int num = 5, byte byt = 5, sbyte sby = -5, short sho = -5, ushort ush = 5, float? nfl = 2.5f,
					double nan = double.NaN, double inf = double.PositiveInfinity, float ninf = float.NegativeInfinity,
					double big = 1e20, double third = 1.0 / 3, char chr = 'x', Level level = Level.High, Level combo = (Level)7,
					[Range(0.1, 0.3)] decimal? rangeDec = null,
					[Range(1.5, 9.5)] float? rangeFlt = null,
					[Range(0, 10)] double? rangeInt = null,
					[Range(4, Single.MaxValue)] float? atLeast = null,
					[Range(Int64.MinValue, 10)] long? atMost = null,
					[Range(0, double.MaxValue)] decimal? decMin = null,
					[Range(double.MinValue, double.MaxValue)] double? unbounded = null,
					[Range(typeof(decimal), "0.10", "2.5")] decimal? typedDec = null,
					[Range(typeof(double), "0.5", "1.7976931348623157E+308")] double? typedOpen = null,
					[AllowedValues(double.NaN, 1.5)] double? allowNan = null,
					[AllowedValues(1, 2)] decimal? allowDec = null,
					[DeniedValues(3)] long? denyLong = null) { }
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app) => app.Map("run", Commands.Run);
			}
		}
		""";

	[Theory]
	[InlineData("dec", "1.5m")]
	[InlineData("flt", "1.5f")]
	[InlineData("dbl", "1.5d")]
	[InlineData("lng", "5L")]
	[InlineData("ulg", "5UL")]
	[InlineData("uin", "5U")]
	[InlineData("num", "5")]
	[InlineData("byt", "(byte)5")]
	[InlineData("sby", "(sbyte)(-5)")]
	[InlineData("sho", "(short)(-5)")]
	[InlineData("ush", "(ushort)5")]
	[InlineData("nfl", "2.5f")]
	[InlineData("nan", "global::System.Double.NaN")]
	[InlineData("inf", "global::System.Double.PositiveInfinity")]
	[InlineData("ninf", "global::System.Single.NegativeInfinity")]
	[InlineData("big", "1E+20d")]
	[InlineData("third", "0.3333333333333333d")]
	[InlineData("chr", "'x'")]
	[InlineData("level", "global::App.Level.High")]
	[InlineData("combo", "(global::App.Level)(7)")]
	public void Defaults_carry_their_exact_type(string parameter, string literal)
	{
		var generated = Generate(LiteralsSource, out _, out _);
		Assert.Contains($"__p_{parameter} = {literal};", generated);
	}

	[Theory]
	[InlineData("< 0.1m")]
	[InlineData("> 0.3m")]
	[InlineData("< 1.5d")]
	[InlineData("> 9.5d")]
	[InlineData("< 0.10m")]
	[InlineData("> 2.5m")]
	[InlineData(".Equals(global::System.Double.NaN)")]
	[InlineData(".Equals(1.5d)")]
	[InlineData(".Equals(1m)")]
	[InlineData(".Equals(2m)")]
	[InlineData("== 3L)")]
	public void Range_bounds_and_value_lists_carry_their_exact_type(string fragment)
	{
		var generated = Generate(LiteralsSource, out _, out _);
		Assert.Contains(fragment, generated);
	}

	[Theory]
	[InlineData("atLeast", "__p_atLeast.Value < 4d")]
	[InlineData("atMost", "__p_atMost.Value > 10d")]
	[InlineData("decMin", "__p_decMin.Value < 0m")]
	[InlineData("typedOpen", "__p_typedOpen.Value < 0.5d")]
	public void A_bound_at_the_type_limit_is_not_checked(string parameter, string onlyCheck)
	{
		var generated = Generate(LiteralsSource, out _, out _);
		Assert.Contains($"if (__p_{parameter}.HasValue && ({onlyCheck}))", generated);
	}

	[Fact]
	public void Both_bounds_unbounded_emits_no_check()
	{
		var generated = Generate(LiteralsSource, out _, out _);
		Assert.DoesNotContain("__p_unbounded.Value <", generated);
		Assert.DoesNotContain("__p_unbounded.Value >", generated);
		Assert.DoesNotContain("1.7976931348623157E+308", generated);
	}

	[Fact]
	public void Generated_code_compiles()
	{
		Generate(LiteralsSource, out var output, out var diagnostics);
		Assert.Empty(diagnostics);
		AssertCompiles(output);
	}

	[Fact]
	public void Typed_range_text_is_parsed_with_the_invariant_culture()
	{
		const string source = Usings + """
			namespace App
			{
				public static class Commands
				{
					public static void Run([Range(typeof(double), "1.5", "2.5")] double? ratio = null) { }
				}

				public static class Program
				{
					public static void Configure(IArghBuilder app) => app.Map("run", Commands.Run);
				}
			}
			""";
		var previous = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
			var generated = Generate(source, out _, out var diagnostics);
			Assert.Empty(diagnostics);
			Assert.Contains("< 1.5d", generated);
			Assert.Contains("> 2.5d", generated);
		}
		finally
		{
			CultureInfo.CurrentCulture = previous;
		}
	}

	[Theory]
	[InlineData("typeof(decimal), \"not a number\", \"2\"", "\"not a number\"", "\"not a number\" isn't a valid decimal")]
	[InlineData("typeof(decimal), \"1\", \"1.2.2\"", "\"1.2.2\"", "\"1.2.2\" isn't a valid decimal")]
	[InlineData("typeof(decimal), \"1,5\", \"2\"", "\"1,5\"", "\"1,5\" isn't a valid decimal (bounds are parsed with the invariant culture")]
	[InlineData("typeof(int), \"1\", \"1.5\"", "\"1.5\"", "\"1.5\" isn't a valid int")]
	[InlineData("typeof(int), \"1,000\", \"2000\"", "\"1,000\"", "\"1,000\" isn't a valid int (bounds are parsed with the invariant culture, e.g. \"1e3\")")]
	[InlineData("typeof(long), \"1e-1\", \"2\"", "\"1e-1\"", "\"1e-1\" isn't a valid long")]
	[InlineData("typeof(byte), \"0\", \"256\"", "\"256\"", "\"256\" isn't a valid byte")]
	[InlineData("typeof(double), \"\", \"2\"", "\"\"", "\"\" isn't a valid double")]
	[InlineData("typeof(DateTime), \"2020-01-01\", \"2021-01-01\"", "typeof(DateTime)", "typeof(System.DateTime) isn't a numeric type")]
	[InlineData("typeof(string), \"a\", \"b\"", "typeof(string)", "typeof(string) isn't a numeric type")]
	public void Unusable_typed_range_reports_AGH0038_at_the_argument(string arguments, string offending, string reason)
	{
		var source = Usings + $$"""
			namespace App
			{
				public static class Commands
				{
					public static void Run([Range({{arguments}})] decimal amount = 1m) { }
				}

				public static class Program
				{
					public static void Configure(IArghBuilder app) => app.Map("run", Commands.Run);
				}
			}
			""";
		Generate(source, out _, out var diagnostics, requireOutput: false);

		AssertAgh0038(Assert.Single(diagnostics), source, offending, "amount", reason);
	}

	[Fact]
	public void Both_bounds_unparsable_reports_each_one()
	{
		const string source = Usings + """
			namespace App
			{
				public static class Commands
				{
					public static void Run([Range(typeof(decimal), "low", "high")] decimal amount = 1m) { }
				}

				public static class Program
				{
					public static void Configure(IArghBuilder app) => app.Map("run", Commands.Run);
				}
			}
			""";
		Generate(source, out _, out var diagnostics, requireOutput: false);

		Assert.Collection(diagnostics.OrderBy(d => d.Location.SourceSpan.Start),
			d => AssertAgh0038(d, source, "\"low\"", "amount", "\"low\" isn't a valid decimal"),
			d => AssertAgh0038(d, source, "\"high\"", "amount", "\"high\" isn't a valid decimal"));
	}

	[Fact]
	public void Typed_range_on_a_non_numeric_parameter_reports_AGH0038()
	{
		const string source = Usings + """
			namespace App
			{
				public static class Commands
				{
					public static void Run([Range(typeof(int), "1", "5")] string name = "x") { }
				}

				public static class Program
				{
					public static void Configure(IArghBuilder app) => app.Map("run", Commands.Run);
				}
			}
			""";
		Generate(source, out _, out var diagnostics, requireOutput: false);

		AssertAgh0038(Assert.Single(diagnostics), source, "typeof(int)", "name", "needs a numeric parameter, not string");
	}

	[Theory]
	[InlineData("public sealed record Args([Range(typeof(long), \"x\", \"5\")] long? Count);")]
	[InlineData("public sealed class Args { [Range(typeof(long), \"x\", \"5\")] public long? Count { get; init; } }")]
	public void Typed_range_on_a_dto_member_reports_AGH0038(string dto)
	{
		var source = Usings + $$"""
			namespace App
			{
				{{dto}}

				public static class Commands
				{
					public static void Run([AsParameters] Args args) { }
				}

				public static class Program
				{
					public static void Configure(IArghBuilder app) => app.Map("run", Commands.Run);
				}
			}
			""";
		Generate(source, out _, out var diagnostics, requireOutput: false);

		AssertAgh0038(Assert.Single(diagnostics), source, "\"x\"", "Count", "\"x\" isn't a valid long");
	}

	[Theory]
	[InlineData("public sealed class Opts { [Range(typeof(int), \"one\", \"5\")] public int? Limit { get; set; } }", "\"one\"", "Limit", "\"one\" isn't a valid int")]
	[InlineData("public sealed class Opts { [Range(typeof(int), \"1\", \"5\")] public string? Limit { get; set; } }", "typeof(int)", "Limit", "needs a numeric parameter, not string")]
	[InlineData("public sealed class Opts { [Range(typeof(Guid), \"a\", \"b\")] public int? Limit; }", "typeof(Guid)", "Limit", "typeof(System.Guid) isn't a numeric type")]
	[InlineData("public class BaseOpts { [Range(typeof(decimal), \"1\", \"x\")] public decimal? Limit { get; set; } } public sealed class Opts : BaseOpts { }", "\"x\"", "Limit", "\"x\" isn't a valid decimal")]
	public void Typed_range_on_a_global_options_member_reports_AGH0038(string options, string offending, string member, string reason)
	{
		var source = Usings + $$"""
			namespace App
			{
				{{options}}

				public static class Commands
				{
					public static void Run(Opts opts) { }
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
		Generate(source, out _, out var diagnostics, requireOutput: false);

		AssertAgh0038(Assert.Single(diagnostics), source, offending, member, reason);
	}

	[Fact]
	public void Typed_range_on_a_namespace_options_member_reports_AGH0038()
	{
		const string source = Usings + """
			namespace App
			{
				public class GlobalOpts { }

				public sealed class NsOpts : GlobalOpts
				{
					[Range(typeof(double), "0,5", "1.5")]
					public double? Ratio { get; set; }
				}

				public static class Commands
				{
					public static void Run(NsOpts opts) { }
				}

				public static class Program
				{
					public static void Configure(IArghBuilder app)
					{
						app.UseGlobalOptions<GlobalOpts>();
						app.MapNamespace("ns", "A namespace.", g =>
						{
							g.UseNamespaceOptions<NsOpts>();
							g.Map("run", Commands.Run);
						});
					}
				}
			}
			""";
		Generate(source, out _, out var diagnostics, requireOutput: false);

		AssertAgh0038(Assert.Single(diagnostics), source, "\"0,5\"", "Ratio", "\"0,5\" isn't a valid double");
	}

	[Fact]
	public void Options_members_are_validated_after_they_are_merged()
	{
		const string source = Usings + """
			namespace App
			{
				public sealed class Opts
				{
					[Range(1, int.MaxValue)] public int? Limit { get; set; }
					[AllowedValues(0.5, double.NaN)] public double? Ratio { get; set; }
					[DeniedValues("x")] public string? Name { get; set; }
					[Range(int.MinValue, int.MaxValue)] public int? Anything { get; set; }
				}

				public static class Commands
				{
					public static void Run(Opts opts) { }
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
		Assert.Contains("if (__ropt___p_Limit.HasValue && (__ropt___p_Limit.Value < 1))", generated);
		Assert.Contains("!__ropt___p_Ratio.Value.Equals(global::System.Double.NaN)", generated);
		Assert.Contains("string.Equals(__denied___ropt___p_Name, \"x\", global::System.StringComparison.Ordinal)", generated);
		Assert.Contains("Error: --limit: value must be at least", generated);
		Assert.DoesNotContain("__ropt___p_Anything.Value <", generated);
		// The checks run on the merged values, before the options object is handed to the command.
		Assert.True(generated.IndexOf("__ropt___p_Limit.Value < 1", StringComparison.Ordinal)
			< generated.IndexOf("Commands.Run(", StringComparison.Ordinal));
	}

	[Fact]
	public void Typed_range_with_valid_text_reports_nothing_and_keeps_exact_literals()
	{
		const string source = Usings + """
			namespace App
			{
				public static class Commands
				{
					public static void Run(
						[Range(typeof(decimal), "-0.0000000000000000000000000001", "79228162514264337593543950334")] decimal? tiny = null,
						[Range(typeof(long), "-9007199254740993", "9007199254740993")] long? wide = null,
						[Range(typeof(float), "0.1", "3.4028235E+38")] float? single = null,
						[Range(typeof(double), "0.1", "0.30000000000000004")] double? precise = null,
						[Range(typeof(int), "1", "10")] double? mixed = null,
						[Range(typeof(decimal), "0.5", "1.5")] double? decimalOnDouble = null) { }
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
		Assert.Contains("__p_tiny.Value < -0.0000000000000000000000000001m || __p_tiny.Value > 79228162514264337593543950334m", generated);
		Assert.Contains("__p_wide.Value < -9007199254740993L || __p_wide.Value > 9007199254740993L", generated);
		Assert.Contains("(__p_single.Value < 0.1f))", generated);
		Assert.Contains("__p_precise.Value < 0.1d || __p_precise.Value > 0.30000000000000004d", generated);
		Assert.Contains("__p_mixed.Value < 1 || __p_mixed.Value > 10", generated);
		Assert.Contains("__p_decimalOnDouble.Value < 0.5d || __p_decimalOnDouble.Value > 1.5d", generated);
	}

	[Fact]
	public void Typed_range_text_takes_float_number_styles_for_every_numeric_type()
	{
		const string source = Usings + """
			namespace App
			{
				public static class Commands
				{
					public static void Run(
						[Range(typeof(int), "1e3", "1.0E4")] int? count = null,
						[Range(typeof(int), "1.0", "5")] int? small = null,
						[Range(typeof(long), "-2.50e1", "1e15")] long? wide = null,
						[Range(typeof(byte), "1.0e1", "2e2")] byte? tiny = null,
						[Range(typeof(decimal), "1e-2", "1E3")] decimal? money = null) { }
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
		Assert.Contains("__p_count.Value < 1000 || __p_count.Value > 10000", generated);
		Assert.Contains("__p_small.Value < 1 || __p_small.Value > 5", generated);
		Assert.Contains("__p_wide.Value < -25L || __p_wide.Value > 1000000000000000L", generated);
		Assert.Contains("__p_tiny.Value < (byte)10 || __p_tiny.Value > (byte)200", generated);
		Assert.Contains("__p_money.Value < 0.01m || __p_money.Value > 1000m", generated);
	}

	[Fact]
	public void Enum_and_char_defaults_name_their_value_and_keep_nullability()
	{
		const string source = Usings + """
			namespace App
			{
				public enum Level { Low = 0, Mid = 5, High = 10 }

				[Flags]
				public enum Access { None = 0, Read = 1, Write = 2 }

				public sealed class Opts
				{
					public Level Tier { get; set; } = Level.High;
					public Access Rights { get; set; } = Access.Read | Access.Write;
				}

				public sealed record Args(Level Grade = Level.Mid, Level? Maybe = null);

				public static class Commands
				{
					public static void Run(Opts opts, [AsParameters] Args args, Level? none = null, Level? some = Level.Mid,
						Access both = Access.Read | Access.Write, char quote = '\'', char tab = '\t') { }
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
		Assert.Contains("global::App.Level? __p_none = default;", generated);
		Assert.Contains("global::App.Level? __p_some = global::App.Level.Mid;", generated);
		Assert.Contains("global::App.Access __p_both = (global::App.Access)(3);", generated);
		Assert.Contains("__p_args__Grade = global::App.Level.Mid;", generated);
		Assert.Contains("global::App.Level? __p_args__Maybe = default;", generated);
		Assert.Contains("__p_quote = '\\'';", generated);
		Assert.Contains("__p_tab = '\\t';", generated);
		Assert.Contains("[default: high]", generated);
		Assert.Contains("[default: 3]", generated);
	}

	private static void AssertAgh0038(Diagnostic diagnostic, string source, string offending, string parameter, string reason)
	{
		Assert.Equal("AGH0038", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		Assert.Equal("Program.cs", diagnostic.Location.GetLineSpan().Path);
		Assert.Equal(offending, source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
		var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
		Assert.StartsWith($"[Range] on '{parameter}' can't be checked: ", message);
		Assert.Contains(reason, message);
	}
}
