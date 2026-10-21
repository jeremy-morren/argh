using System.Globalization;
using FluentAssertions;
using Nullean.Argh.Parsing;
using Nullean.Argh.Runtime;
using Nullean.Argh.Tests.Fixtures;
using Xunit;

namespace Nullean.Argh.Tests.Unit.Binding;

/// <summary>Parser options: <see cref="ArghRuntime.ParserOptions"/> and the <c>TryParseArgh</c> overloads that take them.</summary>
public class ParserOptionsTests
{
	private static readonly ParserOptions German = new() { Culture = CultureInfo.GetCultureInfo("de-DE") };

	[Fact]
	public void Defaults_are_the_invariant_culture_float_number_styles_and_round_trip_dates()
	{
		ArghRuntime.ParserOptions.Culture.Should().BeSameAs(CultureInfo.InvariantCulture);
		ArghRuntime.ParserOptions.NumberStyles.Should().Be(NumberStyles.Float);
		ArghRuntime.ParserOptions.DateTimeStyles.Should().Be(DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces);
	}

	[Fact]
	public void By_default_a_utc_date_stays_utc()
	{
		CultureArgs.TryParseArgh(["--amount", "1", "--when", "2024-06-15T14:30:00Z"], out var args).Should().BeTrue();

		args!.When.Should().Be(new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc));
		args.When!.Value.Kind.Should().Be(DateTimeKind.Utc);
	}

	[Fact]
	public void TryParseArgh_with_options_uses_their_date_time_styles()
	{
		var options = new ParserOptions { DateTimeStyles = DateTimeStyles.AdjustToUniversal };
		var ok = CultureArgs.TryParseArgh(
			["--amount", "1", "--when", "2024-06-15T14:30:00+02:00", "--at", "2024-06-15T14:30:00+02:00", "--day", "2024-06-15"],
			options, out var args);

		ok.Should().BeTrue();
		args!.When.Should().Be(new DateTime(2024, 6, 15, 12, 30, 0));
		args.When!.Value.Kind.Should().Be(DateTimeKind.Utc);
		args.At.Should().Be(new DateTimeOffset(2024, 6, 15, 12, 30, 0, TimeSpan.Zero));
		args.At!.Value.Offset.Should().Be(TimeSpan.Zero);
		args.Day.Should().Be(new DateOnly(2024, 6, 15), "DateOnly gets only the whitespace flags, which it accepts");
	}

	[Fact]
	public void TryParseArgh_with_options_parses_with_their_culture()
	{
		var ok = CultureArgs.TryParseArgh(["--amount", "1,5", "--day", "15.06.2024", "--tag", "x"], German, out var args);

		ok.Should().BeTrue();
		args!.Amount.Should().Be(1.5m);
		args.Day.Should().Be(new DateOnly(2024, 6, 15));
		args.Tag.Should().Be("x@de-DE", "the custom parser receives the options too");
	}

	[Fact]
	public void TryParseArgh_with_options_uses_their_number_styles()
	{
		var options = new ParserOptions { NumberStyles = NumberStyles.Integer | NumberStyles.AllowThousands };
		CultureArgs.TryParseArgh(["--amount", "1", "--count", "1,000", "--tag", "x"], options, out var args).Should().BeTrue();

		args!.Count.Should().Be(1000);
		args.Tag.Should().Be("x@invariant/Integer, AllowThousands");
	}

	[Fact]
	public void Allowing_thousands_accepts_group_separators_in_every_number()
	{
		var options = new ParserOptions { NumberStyles = NumberStyles.Float | NumberStyles.AllowThousands };
		CultureArgs.TryParseArghExact(["--amount", "1,000.5", "--count", "1,000", "--scale", "1,000.5"], options, out var args)
			.Should().BeTrue();

		args!.Amount.Should().Be(1000.5m);
		args.Count.Should().Be(1000);
		args.Scale.Should().Be(1000.5);
	}

	[Theory]
	[InlineData("--count", "1,000")]
	[InlineData("--scale", "1,000.5")]
	[InlineData("--amount", "1,000.5")]
	public void By_default_numbers_reject_a_group_separator(string flag, string value)
	{
		string[] args = flag == "--amount" ? [flag, value] : ["--amount", "1", flag, value];
		CultureArgs.TryParseArghExact(args, out _).Should().BeFalse();
	}

	[Fact]
	public void By_default_numbers_accept_an_exponent_and_surrounding_whitespace_nullable_or_not()
	{
		CultureArgs.TryParseArghExact(["--amount", "2.5e1", "--count", " 1e3 ", "--scale", "\t1.5E2\t"], out var args).Should().BeTrue();

		args!.Amount.Should().Be(25m);
		args.Count.Should().Be(1000);
		args.Scale.Should().Be(150);
	}

	[Theory]
	[InlineData("1.0", 1)]
	[InlineData("1e3", 1000)]
	[InlineData("10e-1", 1)]
	[InlineData(" 7 ", 7)]
	[InlineData("+7", 7)]
	public void By_default_int_accepts_a_whole_number_with_a_decimal_point_or_exponent(string value, int expected)
	{
		CultureArgs.TryParseArghExact(["--amount", "1", "--count", value], out var args).Should().BeTrue();
		args!.Count.Should().Be(expected);
	}

	[Theory]
	[InlineData("1.5")]
	[InlineData("15e-1")]
	[InlineData("1e10")]
	public void By_default_int_rejects_a_fraction_or_an_overflow(string value) =>
		CultureArgs.TryParseArghExact(["--amount", "1", "--count", value], out _).Should().BeFalse();

	[Fact]
	public void TryParseArgh_with_options_leaves_the_app_options_alone()
	{
		CultureArgs.TryParseArgh(["--amount", "1,5"], German, out _).Should().BeTrue();

		ArghRuntime.ParserOptions.Culture.Should().BeSameAs(CultureInfo.InvariantCulture);
		CultureArgs.TryParseArgh(["--amount", "1.5"], out var invariant).Should().BeTrue();
		invariant!.Amount.Should().Be(1.5m, "the invariant culture reads '.' as the decimal point");
	}

	[Fact]
	public void Type_based_and_exact_overloads_take_options()
	{
		typeof(CultureArgs).TryParseArgh<CultureArgs>(["--amount", "2,25"], German, out var lenient).Should().BeTrue();
		lenient!.Amount.Should().Be(2.25m);

		typeof(CultureArgs).TryParseArghExact<CultureArgs>(["--amount", "2,25"], German, out var exact).Should().BeTrue();
		exact!.Amount.Should().Be(2.25m);

		CultureArgs.TryParseArghExact(["--amount", "3,5"], German, out var member).Should().BeTrue();
		member!.Amount.Should().Be(3.5m);
	}
}

/// <summary>These set process-wide parser options, so they don't run in parallel with other tests.</summary>
[Collection("Console")]
public class UseParserOptionsTests
{
	[Fact]
	public void UseCulture_on_the_app_sets_the_culture()
	{
		try
		{
			new ArghApp().UseCulture(CultureInfo.GetCultureInfo("de-DE"));
			ArghRuntime.Culture.Name.Should().Be("de-DE");
			CultureArgs.TryParseArgh(["--amount", "1,5"], out var args).Should().BeTrue();
			args!.Amount.Should().Be(1.5m);
		}
		finally
		{
			ArghRuntime.ParserOptions = null!;
		}
	}

	[Fact]
	public void UseParserOptions_sets_them_and_UseCulture_keeps_the_styles()
	{
		try
		{
			new ArghApp().UseParserOptions(new ParserOptions
			{
				NumberStyles = NumberStyles.Integer | NumberStyles.AllowThousands, DateTimeStyles = DateTimeStyles.AdjustToUniversal,
			});
			new ArghApp().UseCulture(CultureInfo.GetCultureInfo("de-DE"));

			ArghRuntime.ParserOptions.Culture.Name.Should().Be("de-DE");
			ArghRuntime.ParserOptions.NumberStyles.Should().Be(NumberStyles.Integer | NumberStyles.AllowThousands);
			ArghRuntime.ParserOptions.DateTimeStyles.Should().Be(DateTimeStyles.AdjustToUniversal);
		}
		finally
		{
			ArghRuntime.ParserOptions = null!;
		}
	}

	[Fact]
	public void Setting_null_restores_the_defaults()
	{
		ArghRuntime.ParserOptions = new ParserOptions { Culture = CultureInfo.GetCultureInfo("de-DE"), NumberStyles = NumberStyles.Any };
		ArghRuntime.ParserOptions = null!;

		ArghRuntime.ParserOptions.Culture.Should().BeSameAs(CultureInfo.InvariantCulture);
		ArghRuntime.ParserOptions.NumberStyles.Should().Be(NumberStyles.Float);
	}

	[Theory]
	[InlineData("fr-FR", "[default: 1,5]")]
	[InlineData("fa-IR", "[default: 1\u066B5]")]
	[InlineData("", "[default: 1.5]")]
	public async Task Help_formats_numeric_defaults_with_the_culture(string culture, string expected)
	{
		var previous = Console.Out;
		var output = new StringWriter();
		try
		{
			ArghRuntime.Culture = culture.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture);
			Console.SetOut(output);
			(await ArghRuntime.RunAsync(["culture-cmd", "--help"])).Should().Be(0);
		}
		finally
		{
			Console.SetOut(previous);
			ArghRuntime.ParserOptions = null!;
		}

		output.ToString().Should().Contain(expected);
	}
}
