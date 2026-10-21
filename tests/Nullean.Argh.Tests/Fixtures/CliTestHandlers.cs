using System.Collections.Generic;
using System.Linq;
using System.Threading;
using External.Ns.Options;
using Nullean.Argh;
using Nullean.Argh.Parsing;
using Nullean.Argh.Tests.ReferencedDtos;

namespace Nullean.Argh.Tests.Fixtures;

internal enum TestColor
{
	/// <summary>The color red.</summary>
	Red,
	/// <summary>The color blue.</summary>
	Blue
}

internal enum TestPalette
{
	/// <summary>A warm red hue.</summary>
	[EnumValue("fire-red")]
	FireRed,
	/// <summary>A deep ocean blue.</summary>
	[EnumValue("ocean-blue")]
	OceanBlue,
	/// <summary>Lush green.</summary>
	Green
}

/// <summary>Sample record bound via <see cref="AsParametersAttribute"/>.</summary>
/// <param name="Env">Deployment environment name.</param>
/// <param name="Port">Listen port.</param>
internal sealed record DeployCliArgs(string Env, int Port);

/// <summary><see cref="AsParametersAttribute"/> record with injected <see cref="CancellationToken"/> (must follow all CLI-bound members).</summary>
internal sealed record AsParamsWithCtArgs(string Env, int Port, CancellationToken Ct);

/// <summary>Record with nullable value types for <see cref="AsParametersAttribute"/> binding coverage.</summary>
internal sealed record NullableNumericAsParamsArgs(int? Rps, int? MaxPages);

/// <summary>AsParameters: <see cref="FixtureSeverity"/> with default + <see cref="FixtureConfigSource"/>? (CS0136: one <c>out</c> per enum, not a shared <c>__ev</c>).</summary>
internal sealed class MultiEnumAsParamsArgs
{
	public FixtureSeverity Severity { get; set; } = FixtureSeverity.Information;
	public FixtureConfigSource? ConfigSource { get; set; }
}

/// <summary>Record with optional <see cref="Uri"/> for <see cref="AsParametersAttribute"/> binding coverage.</summary>
internal sealed record OptionalUriAsParamsArgs(Uri? Endpoint);

/// <summary>Record with <see cref="IReadOnlySet{T}"/> for set-binding coverage.</summary>
/// <param name="TagIds">Set of integer tag IDs.</param>
internal sealed record TagSetArgs(IReadOnlySet<int> TagIds);

/// <summary>Record with property-targeted <see cref="CollectionSyntaxAttribute"/> on supported collection members.</summary>
internal sealed record CollectionSyntaxAsParamsArgs(
	[property: CollectionSyntax(Separator = ",")] IReadOnlySet<int> TagIds,
	[property: CollectionSyntax(Separator = "|")] List<string> Labels,
	[property: CollectionSyntax(Separator = ";")] int[] Ports);

/// <summary>Init-property [AsParameters] DTO with optional collection syntax separator.</summary>
internal sealed class OptionalCollectionSyntaxAsParamsArgs
{
	[CollectionSyntax(Separator = ",")]
	public IReadOnlySet<int>? TagIds { get; init; }

	public string? Output { get; init; }
}

/// <summary>Init-property [AsParameters] DTO with a property-level custom parser.</summary>
internal sealed class PointAsParamsArgs
{
	[ArgumentParser(typeof(CliTestHandlers.PointParser))]
	public CliTestHandlers.Point Point { get; init; }
}

/// <summary>[AsParameters] DTO whose values parse differently depending on the culture.</summary>
internal sealed class CultureArgs
{
	public decimal Amount { get; init; }
	public DateTime? When { get; init; }
	public DateOnly? Day { get; init; }
	public TimeSpan? Wait { get; init; }
	public DateTimeOffset? At { get; init; }
	public List<decimal>? Amounts { get; init; }
	public int? Count { get; init; }
	public double? Scale { get; init; }

	[ArgumentParser(typeof(CliTestHandlers.CultureNameParser))]
	public string? Tag { get; init; }
}

/// <summary>[AsParameters] DTO with <c>Point?</c> members: one parsed through <c>IArgumentParser&lt;Point&gt;</c>, one through <c>IArgumentParser&lt;Point?&gt;</c>.</summary>
internal sealed class OptionalPointArgs
{
	[ArgumentParser(typeof(CliTestHandlers.PointParser))]
	public CliTestHandlers.Point? Near { get; init; }

	[ArgumentParser(typeof(CliTestHandlers.NullablePointParser))]
	public CliTestHandlers.Point? Either { get; init; }
}

/// <summary>[AsParameters] DTO with two members that use the same custom parser.</summary>
internal sealed class TwoPointsArgs
{
	[ArgumentParser(typeof(CliTestHandlers.PointParser))]
	public CliTestHandlers.Point From { get; init; }

	[ArgumentParser(typeof(CliTestHandlers.PointParser))]
	public CliTestHandlers.Point To { get; init; }
}

/// <summary>Init-property [AsParameters] DTO with each special scalar type, plain (required) and nullable.</summary>
internal sealed class ScalarAsParamsArgs
{
	public TimeSpan Span { get; init; }
	public TimeSpan? SpanOpt { get; init; }
	public DateTime Stamp { get; init; }
	public DateTime? StampOpt { get; init; }
	public DateTimeOffset Offset { get; init; }
	public DateTimeOffset? OffsetOpt { get; init; }
	public DateOnly Day { get; init; }
	public DateOnly? DayOpt { get; init; }
	public TimeOnly Clock { get; init; }
	public TimeOnly? ClockOpt { get; init; }
	public Uri Link { get; init; } = null!;
	public Uri? LinkOpt { get; init; }
	public FileInfo Doc { get; init; } = null!;
	public FileInfo? DocOpt { get; init; }
	public DirectoryInfo Folder { get; init; } = null!;
	public DirectoryInfo? FolderOpt { get; init; }
}

/// <summary>Init-only bound object for XML documentation in help output.</summary>
internal sealed class PropDocBoundArgs
{
	/// <summary>Argh_help_doc_alpha_unique.</summary>
	public string Alpha { get; init; } = "";

	/// <summary>Argh_help_doc_beta_unique.</summary>
	public int Beta { get; init; }
}

internal sealed class ParamCommentRecord
{
	/// <summary>Argh_help_doc_gamma_unique.</summary>
	public string Gamma { get; init; } = "";
}

internal static class CliTestHandlers
{
	/// <summary>Greet someone by name.</summary>
	/// <remarks>See <see cref="CliRegistrationModule.DocLambdaEcho"/>; set <paramref name="name"/>.</remarks>
	/// <example>hello --name world</example>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="name">The name to greet.</param>
	[MiddlewareAttribute<TestsPerCommandMiddleware>]
	public static void Hello(TestGlobalCliOptions g, string name)
	{
		var mode = string.IsNullOrEmpty(g.Mode) ? "" : ":" + g.Mode;
		Console.Out.WriteLine($"ok:{name}{mode}");
	}

	/// <summary>Greet by name without a global-options handler parameter (-m still parses).</summary>
	/// <param name="name">The name.</param>
	[NoOptionsInjection]
	public static void NinHello(string name) =>
		Console.Out.WriteLine($"nin:{name}");

	/// <summary>Enum and short options.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="color">-c,--colour, Pick a color.</param>
	/// <param name="name">-n,--name, Display name</param>
	public static void EnumCmd(TestGlobalCliOptions g, TestColor color, string name) =>
		Console.Out.WriteLine($"ok:{color}:{name}");

	/// <summary>Enum with custom CLI value strings via EnumValueAttribute.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="palette">Pick a palette color using kebab-case CLI values.</param>
	public static void EnumValueCmd(TestGlobalCliOptions g, TestPalette palette) =>
		Console.Out.WriteLine($"enum-value:{palette}");

	public static void Deploy(TestGlobalCliOptions g, [AsParameters("app")] DeployCliArgs args) =>
		Console.Out.WriteLine($"deploy:{args.Env}:{args.Port}");

	public static void AsParamsWithCt(TestGlobalCliOptions g, [AsParameters("run")] AsParamsWithCtArgs args) =>
		Console.Out.WriteLine($"as-params-ct:{args.Env}:{args.Port}:{args.Ct.CanBeCanceled}");

	public static void NullableNumericAsParams(TestGlobalCliOptions g, [AsParameters("labs")] NullableNumericAsParamsArgs args) =>
		Console.Out.WriteLine($"nullable-numeric:{args.Rps?.ToString() ?? "null"}:{args.MaxPages?.ToString() ?? "null"}");

	public static void MultiEnumAsParams(TestGlobalCliOptions g, [AsParameters("mix")] MultiEnumAsParamsArgs args) =>
		Console.Out.WriteLine($"multi-enum:{args.Severity}:{args.ConfigSource?.ToString() ?? "null"}");

	public static void OptionalUriAsParams(TestGlobalCliOptions g, [AsParameters] OptionalUriAsParamsArgs args) =>
		Console.Out.WriteLine($"optional-uri:{args.Endpoint?.ToString() ?? "null"}");

	public static void PropDocAsParams(TestGlobalCliOptions g, [AsParameters] PropDocBoundArgs args) =>
		Console.Out.WriteLine($"prop-doc:{args.Alpha}:{args.Beta}");

	public static void ParamCommentRecordCmd(TestGlobalCliOptions g, [AsParameters] ParamCommentRecord r) =>
		Console.Out.WriteLine($"param-comment:{r.Gamma}");

	public static void Tags(TestGlobalCliOptions g, List<string> tags) =>
		Console.Out.WriteLine("tags:" + string.Join(",", tags));

	/// <summary>Regression: braces in XML docs must not become C# interpolation in generated help.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="description">Supports {version} and {owner} placeholders.</param>
	public static void BraceDoc(TestGlobalCliOptions g, string? description = null) =>
		Console.Out.WriteLine("brace-doc:" + (description ?? ""));

	// For global enum re-parse after command test
	public static void SeverityCmd(TestGlobalCliOptions g) =>
		Console.Out.WriteLine($"severity:{g.Severity}");

	// For bool? test
	public static void DryRunCmd(TestGlobalCliOptions g, bool? dryRun = null) =>
		Console.Out.WriteLine($"dry-run:{dryRun?.ToString().ToLower() ?? "null"}");

	// For int parsing test
	public static void CountCmd(TestGlobalCliOptions g, int count) =>
		Console.Out.WriteLine($"count:{count}");

	// For FileInfo test
	public static void FileCmd(TestGlobalCliOptions g, FileInfo file) =>
		Console.Out.WriteLine($"file:{file.Name}");

	// For DirectoryInfo test
	public static void DirCmd(TestGlobalCliOptions g, DirectoryInfo dir) =>
		Console.Out.WriteLine($"dir:{dir.Name}");

	// For Uri test
	public static void UriCmd(TestGlobalCliOptions g, Uri uri) =>
		Console.Out.WriteLine($"uri:{uri.Host}");

	/// <param name="g">Injected global CLI options.</param>
	/// <param name="duration">Duration flag.</param>
	/// <param name="on">A calendar date.</param>
	public static void TemporalCmd(TestGlobalCliOptions g, TimeSpan duration, DateOnly on) =>
		Console.Out.WriteLine($"temporal:{duration.TotalSeconds}:{on:yyyy-MM-dd}");

	/// <param name="g">Injected global CLI options.</param>
	/// <param name="at">A time of day.</param>
	/// <param name="until">An optional time of day.</param>
	public static void TimeOnlyCmd(TestGlobalCliOptions g, TimeOnly at, TimeOnly? until = null) =>
		Console.Out.WriteLine($"time-only:{at:HH:mm:ss}:{(until is { } u ? u.ToString("HH:mm:ss") : "none")}");

	public static void AsParamsScalars(TestGlobalCliOptions g, [AsParameters] ScalarAsParamsArgs a) =>
		WriteScalars(a.Span, a.SpanOpt, a.Stamp, a.StampOpt, a.Offset, a.OffsetOpt, a.Day, a.DayOpt,
			a.Clock, a.ClockOpt, a.Link, a.LinkOpt, a.Doc, a.DocOpt, a.Folder, a.FolderOpt);

	public static void ScalarsNsShow(ScalarNamespaceOptions o) =>
		WriteScalars(o.Span, o.SpanOpt, o.Stamp, o.StampOpt, o.Offset, o.OffsetOpt, o.Day, o.DayOpt,
			o.Clock, o.ClockOpt, o.Link, o.LinkOpt, o.Doc, o.DocOpt, o.Folder, o.FolderOpt);

	// One "name=value" line per member, in a culture-independent form; null prints "none".
	private static void WriteScalars(params object?[] values)
	{
		string[] names = ["span", "span-opt", "stamp", "stamp-opt", "offset", "offset-opt", "day", "day-opt",
			"clock", "clock-opt", "link", "link-opt", "doc", "doc-opt", "folder", "folder-opt"];
		for (var i = 0; i < names.Length; i++)
		{
			var text = values[i] switch
			{
				null => "none",
				TimeSpan t => t.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
				DateTime d => d.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
				DateTimeOffset d => d.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
				DateOnly d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
				TimeOnly t => t.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
				FileSystemInfo f => f.Name,
				var v => v.ToString(),
			};
			Console.Out.WriteLine($"{names[i]}={text}");
		}
	}
	/// <summary>XML docs with non-ASCII text in the remarks and parameters.</summary>
	/// <remarks>Remarks: naïve café, 日本語, 😀. Entities: &#233; &#x1F680;.</remarks>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="speed">Speed — fast or slow, 速い, 🚀.</param>
	public static void UnicodeDocs(TestGlobalCliOptions g, string speed) =>
		Console.Out.WriteLine($"speed:{speed}");

	// For custom parser test
	public static void PointCmd(TestGlobalCliOptions g, [ArgumentParser(typeof(PointParser))] Point point) =>
		Console.Out.WriteLine($"point:{point.X},{point.Y}");

	/// <summary>Parameters named like generated locals, and two of each parse kind in one method.</summary>
	public static void LocalNames(TestGlobalCliOptions g,
		[ArgumentParser(typeof(PointParser))] Point start, [ArgumentParser(typeof(PointParser))] Point end,
		string? args = null, string? flags = null, string? positionals = null, string? i = null, string? a = null,
		string? eq = null, string? ct = null, string? fooBar = null, string? foo_bar = null,
		TimeSpan? wait = null, TimeSpan? delay = null, Uri? home = null, Uri? mirror = null) =>
		Console.Out.WriteLine(string.Join("|",
			$"{start.X},{start.Y}", $"{end.X},{end.Y}", args, flags, positionals, i, a, eq, ct, fooBar, foo_bar,
			wait?.TotalSeconds, delay?.TotalSeconds, home?.Host, mirror?.Host));

	/// <summary>Two members of one DTO that use the same custom parser.</summary>
	public static void LocalNamesDto(TestGlobalCliOptions g, [AsParameters] TwoPointsArgs args) =>
		Console.Out.WriteLine($"{args.From.X},{args.From.Y}|{args.To.X},{args.To.Y}");

	/// <summary>Culture test: numeric allowed values, denied values and a range.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="rate">One of a fixed list, completed and shown in the parsing culture.</param>
	/// <param name="step">Anything but one value.</param>
	/// <param name="ratio">A bounded value.</param>
	public static void CultureChoices(TestGlobalCliOptions g,
		[System.ComponentModel.DataAnnotations.AllowedValues(1.5, 2.5, 10)] double rate = 1.5,
		[System.ComponentModel.DataAnnotations.DeniedValues(0.5)] double? step = null,
		[System.ComponentModel.DataAnnotations.Range(0.5, 9.5)] double? ratio = null)
	{
		var inv = System.Globalization.CultureInfo.InvariantCulture;
		Console.Out.WriteLine(string.Join("|", rate.ToString(inv), step?.ToString(inv) ?? "none", ratio?.ToString(inv) ?? "none"));
	}

	/// <summary>Culture test: values whose parsing depends on the culture.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="args">Culture-sensitive values.</param>
	/// <param name="envAmount">A decimal read from <c>ARGH_TEST_AMOUNT</c>.</param>
	/// <param name="ratio">A double with a default, which help shows in the parsing culture.</param>
	public static void CultureCmd(TestGlobalCliOptions g, [AsParameters] CultureArgs args, [Env("ARGH_TEST_AMOUNT")] decimal? envAmount = null, double ratio = 1.5)
	{
		var inv = System.Globalization.CultureInfo.InvariantCulture;
		Console.Out.WriteLine(string.Join("|",
			args.Amount.ToString(inv),
			args.When?.ToString("yyyy-MM-dd HH:mm", inv) ?? "none",
			args.Day?.ToString("yyyy-MM-dd", inv) ?? "none",
			args.Wait?.TotalSeconds.ToString(inv) ?? "none",
			args.At?.ToString("o", inv) ?? "none",
			args.Amounts is null ? "none" : string.Join(",", args.Amounts.Select(a => a.ToString(inv))),
			envAmount?.ToString(inv) ?? "none",
			args.Tag ?? "none",
			args.Count?.ToString(inv) ?? "none",
			args.Scale?.ToString(inv) ?? "none",
			ratio.ToString(inv)));
	}

	public static void OptionalPointCmd(TestGlobalCliOptions g, [AsParameters] OptionalPointArgs args,
		[ArgumentParser(typeof(PointParser))] Point? at = null) =>
		Console.Out.WriteLine($"{Show(at)}|{Show(args.Near)}|{Show(args.Either)}");

	private static string Show(Point? point) => point is { } p ? $"{p.X},{p.Y}" : "none";

	public static void PointAsParamsCmd(TestGlobalCliOptions g, [AsParameters] PointAsParamsArgs args) =>
		Console.Out.WriteLine($"point-as-params:{args.Point.X},{args.Point.Y}");

	// Whole-collection [ArgumentParser] on IReadOnlySet<T> (parser returns the whole set)
	public static void TagSetParser(TestGlobalCliOptions g, [ArgumentParser(typeof(StringSetParser))] IReadOnlySet<string> tags) =>
		Console.Out.WriteLine("tag-set-parser:" + string.Join(",", tags.OrderBy(x => x)));

	public static void TagSetParserOpt(TestGlobalCliOptions g, [ArgumentParser(typeof(StringSetParser))] IReadOnlySet<string>? tags) =>
		Console.Out.WriteLine("tag-set-parser-opt:" + (tags is null ? "null" : string.Join(",", tags.OrderBy(x => x))));

	// IReadOnlySet<T> handlers
	public static void TagSet(TestGlobalCliOptions g, IReadOnlySet<int> tagIds) =>
		Console.Out.WriteLine("tag-set:" + string.Join(",", tagIds.OrderBy(x => x)));

	public static void ColorSet(TestGlobalCliOptions g, IReadOnlySet<TestColor> colors) =>
		Console.Out.WriteLine("color-set:" + string.Join(",", colors.OrderBy(x => (int)x)));

	public static void OptTagSet(TestGlobalCliOptions g, IReadOnlySet<int>? tagIds) =>
		Console.Out.WriteLine(
			"opt-tag-set:"
			+ (tagIds is null ? "null" : tagIds.Count == 0 ? "empty" : string.Join(",", tagIds.OrderBy(x => x))));

	public static void AsParamsTagSet(TestGlobalCliOptions g, [AsParameters] TagSetArgs args) =>
		Console.Out.WriteLine("as-params-tag-set:" + string.Join(",", args.TagIds.OrderBy(x => x)));

	public static void AsParamsCollectionSyntax(TestGlobalCliOptions g, [AsParameters("cs")] CollectionSyntaxAsParamsArgs args) =>
		Console.Out.WriteLine(
			"as-params-collection-syntax:"
			+ string.Join(",", args.TagIds.OrderBy(x => x))
			+ ":"
			+ string.Join(",", args.Labels)
			+ ":"
			+ string.Join(",", args.Ports));

	public static void AsParamsOptionalCollectionSyntax(TestGlobalCliOptions g, [AsParameters("ocs")] OptionalCollectionSyntaxAsParamsArgs args) =>
		Console.Out.WriteLine(
			"as-params-optional-collection-syntax:"
			+ (args.TagIds is null ? "null" : args.TagIds.Count == 0 ? "empty" : string.Join(",", args.TagIds.OrderBy(x => x)))
			+ ":"
			+ (args.Output ?? "null"));

	public static void AsParamsReferencedDto(TestGlobalCliOptions g, [AsParameters] IsolatedBuildOptions options) =>
		Console.Out.WriteLine($"as-params-referenced:{options.Path ?? "null"}:{options.Output ?? "null"}:{options.Source?.ToString() ?? "null"}");

	// Regression: a synchronous handler returning int must propagate that value as the process exit code.
	[NoOptionsInjection]
	public static int ExitCodeCmd(int code)
	{
		Console.Out.WriteLine($"exit-code:{code}");
		return code;
	}

	internal readonly record struct Point(int X, int Y);

	internal sealed class PointParser : IArgumentParser<Point>
	{
		public bool TryParse(string raw, ParserOptions options, out Point value)
		{
			value = default;
			var parts = raw.Split(',');
			if (parts.Length == 2 && int.TryParse(parts[0], options.NumberStyles, options.Culture, out var x) && int.TryParse(parts[1], options.NumberStyles, options.Culture, out var y))
			{ value = new Point(x, y); return true; }
			return false;
		}
	}

	/// <summary>
	/// Implements both <c>IArgumentParser&lt;Point&gt;</c>, which rejects every value, and <c>IArgumentParser&lt;Point?&gt;</c>,
	/// which parses like <see cref="PointParser"/> and maps <c>none</c> to null. A <c>Point?</c> must use the second.
	/// </summary>
	internal sealed class NullablePointParser : IArgumentParser<Point>, IArgumentParser<Point?>
	{
		bool IArgumentParser<Point>.TryParse(string raw, ParserOptions options, out Point value)
		{
			value = default;
			return false;
		}

		bool IArgumentParser<Point?>.TryParse(string raw, ParserOptions options, out Point? value)
		{
			value = null;
			if (raw == "none")
				return true;
			if (!new PointParser().TryParse(raw, options, out var point))
				return false;
			value = point;
			return true;
		}
	}

	/// <summary>Returns the raw value with the name of the culture it was given, and the number styles unless they're the default, so tests can see which options reached the parser.</summary>
	internal sealed class CultureNameParser : IArgumentParser<string>
	{
		public bool TryParse(string raw, ParserOptions options, out string value)
		{
			value = raw + "@" + (options.Culture.Name.Length == 0 ? "invariant" : options.Culture.Name) + (options.NumberStyles == System.Globalization.NumberStyles.Float ? "" : "/" + options.NumberStyles);
			return true;
		}
	}

	internal sealed class StringSetParser : IArgumentParser<IReadOnlySet<string>>
	{
		public bool TryParse(string raw, ParserOptions options, out IReadOnlySet<string> value)
		{
			value = new HashSet<string>(raw.Split(',', StringSplitOptions.RemoveEmptyEntries));
			return true;
		}
	}
}

/// <summary>Handlers under <c>billing tools</c> for AGH0022 nested-namespace regression.</summary>
internal static class BillingToolsHandlers
{
	public static void Status(TestGlobalCliOptions g) =>
		Console.Out.WriteLine("billing-tools-status");
}

/// <summary>Handlers under <c>support tools</c> for AGH0022 nested-namespace regression.</summary>
internal static class SupportToolsHandlers
{
	public static void Status(TestGlobalCliOptions g) =>
		Console.Out.WriteLine("support-tools-status");
}

/// <summary>Release stage with an <c>[Experimental]</c> member, which the generated parse switch references.</summary>
internal enum FixtureStage
{
	Stable,

	[System.Diagnostics.CodeAnalysis.Experimental("ARGHTEST001")]
	Preview
}

/// <summary>Handlers for obsolete and experimental APIs used by generated code.</summary>
internal static class ExperimentalApiHandlers
{
	/// <summary>Uses an experimental enum member through the generated code.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="stage">Release stage.</param>
	public static void ExperimentalApi(TestGlobalCliOptions g, FixtureStage stage = FixtureStage.Stable) =>
		Console.Out.WriteLine($"stage:{stage}");
}

/// <summary>Handlers for environment variable binding tests.</summary>
internal static class EnvBindingHandlers
{
	/// <summary>Echo a token from either the flag or an env var fallback.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="token">-t, --token, The token value.</param>
	public static void EnvCmd(TestGlobalCliOptions g, [Env("MY_APP_TOKEN")] string? token = null) =>
		Console.Out.WriteLine($"env-token:{token}");

	/// <summary>Echo a required value that can come from an env var.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="secret">The required secret value.</param>
	public static void EnvRequired(TestGlobalCliOptions g, [Env("MY_APP_SECRET")] string secret) =>
		Console.Out.WriteLine($"env-secret:{secret}");

	/// <summary>Echo a boolean flag that supports env var fallback.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="verbose">--verbose, Enable verbose mode.</param>
	public static void EnvBool(TestGlobalCliOptions g, [Env("MY_APP_VERBOSE")] bool verbose = false) =>
		Console.Out.WriteLine($"env-verbose:{verbose}");

	/// <summary>Echo global option api-url which is env-bound to TEST_API_URL.</summary>
	/// <param name="g">Injected global CLI options.</param>
	public static void EnvGlobal(TestGlobalCliOptions g) =>
		Console.Out.WriteLine($"env-global-api-url:{g.ApiUrl}");

	/// <summary>Echo namespace option key which is env-bound to TEST_NS_KEY.</summary>
	/// <param name="o">Namespace-scoped options with env binding.</param>
	public static void EnvNs(EnvTestNamespaceOptions o) =>
		Console.Out.WriteLine($"env-ns-key:{o.Key}");
}

/// <summary>
/// Cross-assembly [AsParameters] handler.
/// Tests that non-nullable init properties with C# initializers are not required when the
/// [AsParameters] DTO type comes from a referenced assembly.
/// </summary>
internal static class CrossAssemblyHandlers
{
	public static void Echo(TestGlobalCliOptions g, [AsParameters] CrossAssemblyAsParamsDto o) =>
		Console.Out.WriteLine($"cross-assembly:{o.Level}:{o.Tag}");

	/// <summary>Echo verbose and tag from an [AsParameters] DTO in an unrelated external namespace.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="opts">External-namespace [AsParameters] DTO (External.Ns.Options.ExternalNsGlobalOptions).</param>
	public static void ExternalNsEcho(TestGlobalCliOptions g, [AsParameters] ExternalNsGlobalOptions opts) =>
		Console.Out.WriteLine($"ext-ns-as-params:{opts.Verbose}:{opts.Tag}");
}

/// <summary>An order number such as <c>ORD-42</c>, parsed through its public <c>IParsable</c> members.</summary>
internal readonly record struct OrderId(int Number) : IParsable<OrderId>
{
	public static OrderId Parse(string s, IFormatProvider? provider) =>
		TryParse(s, provider, out var id) ? id : throw new FormatException($"'{s}' is not an order id.");

	public static bool TryParse([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s, IFormatProvider? provider, out OrderId result)
	{
		result = default;
		if (s is null || !s.StartsWith("ORD-", StringComparison.Ordinal)
		    || !int.TryParse(s.AsSpan(4), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n))
			return false;
		result = new OrderId(n);
		return true;
	}

	public override string ToString() => "ORD-" + Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>A code that implements <c>IParsable</c> explicitly, so it has no public static <c>TryParse</c>.</summary>
internal sealed class ExplicitCode : IParsable<ExplicitCode>
{
	private ExplicitCode(string value) => Value = value;

	public string Value { get; }

	static ExplicitCode IParsable<ExplicitCode>.Parse(string s, IFormatProvider? provider) => new(s.ToUpperInvariant());

	static bool IParsable<ExplicitCode>.TryParse([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s, IFormatProvider? provider,
		[System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out ExplicitCode result)
	{
		result = s is { Length: 3 } ? new ExplicitCode(s.ToUpperInvariant()) : null;
		return result is not null;
	}
}

/// <summary>A token that implements <c>ISpanParsable</c>; Argh calls its string overload.</summary>
internal readonly record struct SpanToken(string Text) : ISpanParsable<SpanToken>
{
	public static SpanToken Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

	public static bool TryParse([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s, IFormatProvider? provider, out SpanToken result) =>
		TryParse(s.AsSpan(), provider, out result);

	public static SpanToken Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
		TryParse(s, provider, out var t) ? t : throw new FormatException("Empty token.");

	public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SpanToken result)
	{
		result = s.IsEmpty ? default : new SpanToken("tok:" + s.ToString());
		return !s.IsEmpty;
	}
}

/// <summary>An amount whose parsing follows the provider, so it reads <c>1,5</c> under de-DE.</summary>
internal readonly record struct Amount(decimal Value) : IParsable<Amount>
{
	public static Amount Parse(string s, IFormatProvider? provider) => new(decimal.Parse(s, System.Globalization.NumberStyles.Number, provider));

	public static bool TryParse([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s, IFormatProvider? provider, out Amount result)
	{
		var ok = decimal.TryParse(s, System.Globalization.NumberStyles.Number, provider, out var v);
		result = new Amount(v);
		return ok;
	}
}

/// <summary>Converts an order id with an [ArgumentParser], which must win over the type's own IParsable.</summary>
internal sealed class OrderIdOffsetParser : IArgumentParser<OrderId>
{
	public bool TryParse(string raw, Nullean.Argh.Parsing.ParserOptions options, out OrderId value)
	{
		var ok = int.TryParse(raw, System.Globalization.NumberStyles.Integer, options.Culture, out var n);
		value = new OrderId(n + 1000);
		return ok;
	}
}

/// <summary>[AsParameters] DTO with IParsable members.</summary>
internal sealed class ParsableArgs
{
	public OrderId Order { get; init; }
	public Guid? Id { get; init; }
	[CollectionSyntax(Separator = ",")]
	public List<OrderId> Batch { get; init; } = [];
}

/// <summary>Namespace options with IParsable members.</summary>
internal sealed class ParsableNamespaceOptions : TestGlobalCliOptions
{
	public OrderId? Order { get; set; }
	public Guid? Id { get; set; }
}

/// <summary>Handlers for types bound through IParsable.</summary>
internal static class ParsableHandlers
{
	/// <summary>Types parsed through IParsable, without an [ArgumentParser].</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="order">An order id.</param>
	/// <param name="code">A code with an explicit IParsable implementation.</param>
	/// <param name="token">An ISpanParsable token.</param>
	/// <param name="id">A Guid.</param>
	/// <param name="small">A short.</param>
	/// <param name="letter">A char.</param>
	/// <param name="amount">A culture-dependent amount.</param>
	/// <param name="maybe">An optional order id.</param>
	/// <param name="more">Repeatable order ids.</param>
	/// <param name="fromEnv">An order id read from ARGH_TEST_ORDER.</param>
	public static void Parsables(TestGlobalCliOptions g, OrderId order, ExplicitCode code, SpanToken token, Guid id,
		short small = 7, char letter = 'x', Amount? amount = null, OrderId? maybe = null, List<OrderId>? more = null,
		[Env("ARGH_TEST_ORDER")] OrderId? fromEnv = null) =>
		Console.Out.WriteLine(string.Join("|",
			order, code.Value, token.Text, id, small.ToString(System.Globalization.CultureInfo.InvariantCulture), letter,
			amount?.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none", maybe?.ToString() ?? "none",
			more is null ? "none" : string.Join(",", more), fromEnv?.ToString() ?? "none"));

	/// <summary>IParsable members on an [AsParameters] DTO.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="args">IParsable members.</param>
	public static void ParsableDto(TestGlobalCliOptions g, [AsParameters] ParsableArgs args) =>
		Console.Out.WriteLine($"{args.Order}|{args.Id?.ToString() ?? "none"}|{string.Join(",", args.Batch)}");

	/// <summary>An [ArgumentParser] on an IParsable type wins.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="order">A number the parser turns into an order id.</param>
	public static void ParsableWithParser(TestGlobalCliOptions g, [ArgumentParser(typeof(OrderIdOffsetParser))] OrderId order) =>
		Console.Out.WriteLine(order.ToString());

	/// <summary>IParsable members on namespace options.</summary>
	/// <param name="o">Namespace options.</param>
	public static void ParsableNsShow(ParsableNamespaceOptions o) =>
		Console.Out.WriteLine($"{o.Order?.ToString() ?? "none"}|{o.Id?.ToString() ?? "none"}");

	/// <summary>Numeric types parsed through IParsable and INumberBase.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="b">A byte.</param>
	/// <param name="sb">An sbyte.</param>
	/// <param name="us">A ushort.</param>
	/// <param name="ui">A uint.</param>
	/// <param name="ul">A ulong.</param>
	/// <param name="h">A Half.</param>
	/// <param name="i128">An Int128.</param>
	/// <param name="big">A BigInteger.</param>
	public static void NumericParsables(TestGlobalCliOptions g, byte? b = null, sbyte? sb = null, ushort? us = null, uint? ui = null,
		ulong? ul = null, Half? h = null, Int128? i128 = null, System.Numerics.BigInteger? big = null)
	{
		var inv = System.Globalization.CultureInfo.InvariantCulture;
		var values = new (string Name, IFormattable? Value)[]
			{ ("b", b), ("sb", sb), ("us", us), ("ui", ui), ("ul", ul), ("h", h), ("i128", i128), ("big", big) };
		var set = values.Where(v => v.Value is not null).Select(v => v.Name + "=" + v.Value!.ToString(null, inv)).ToList();
		Console.Out.WriteLine(set.Count == 0 ? "none" : string.Join("|", set));
	}
}
