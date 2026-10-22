using System.Globalization;

namespace Nullean.Argh.Tests.Fixtures;

/// <summary>Short flags that combine, values that look like options, and flags given more than once.</summary>
internal static class FlagTokenHandlers
{
	/// <summary>Switches, values and a collection, most with short names.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="quiet">-q, Quiet.</param>
	/// <param name="force">-f, Force.</param>
	/// <param name="name">-n, A name.</param>
	/// <param name="count">-c, A count that may be negative.</param>
	/// <param name="ratio">A ratio that may be negative.</param>
	/// <param name="tags">-t, Tags; repeatable.</param>
	/// <param name="color">Colour the output, or not.</param>
	public static void Shorts(TestGlobalCliOptions g, bool quiet = false, bool force = false, string name = "", int count = 0,
		double ratio = 0, string[]? tags = null, bool? color = null) =>
		Console.Out.WriteLine(string.Join(" ",
			$"quiet:{quiet}", $"force:{force}", $"name:{name}", $"count:{count.ToString(CultureInfo.InvariantCulture)}",
			$"ratio:{ratio.ToString(CultureInfo.InvariantCulture)}", $"tags:{string.Join(",", tags ?? [])}",
			$"color:{color?.ToString() ?? "null"}", $"verbose:{g.Verbose}", $"mode:{g.Mode}"));

	/// <summary>Flags with their own rule for being given more than once.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="last">The last value wins.</param>
	/// <param name="same">The same value may be given again.</param>
	/// <param name="strict">Never given twice, whatever the parser options say.</param>
	/// <param name="loud">-l, A switch that may be given again.</param>
	/// <param name="plain">Follows the parser options.</param>
	public static void Repeats(TestGlobalCliOptions g,
		[RepeatedScalar(RepeatedScalarBehaviour.TakeLast)] string last = "",
		[RepeatedScalar(RepeatedScalarBehaviour.AllowIdentical)] string same = "",
		[RepeatedScalar(RepeatedScalarBehaviour.Reject)] string strict = "",
		[RepeatedScalar(RepeatedScalarBehaviour.AllowIdentical)] bool loud = false,
		string plain = "") =>
		Console.Out.WriteLine($"last:{last} same:{same} strict:{strict} loud:{loud} plain:{plain} mode:{g.Mode}");

	/// <summary>An [AsParameters] record with a repeat rule on a member.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="o">The record.</param>
	public static void RepeatsDto(TestGlobalCliOptions g, [AsParameters] RepeatOptions o) =>
		Console.Out.WriteLine($"level:{o.Level} tag:{o.Tag}");

	/// <summary>Namespace options with a repeat rule on a member.</summary>
	/// <param name="o">The namespace options.</param>
	public static void RepeatsNs(RepeatNamespaceOptions o) =>
		Console.Out.WriteLine($"region:{o.Region} stage:{o.Stage} mode:{o.Mode}");
}

/// <summary>Members with and without their own repeat rule.</summary>
/// <param name="Level">A level; the last value wins.</param>
/// <param name="Tag">A tag.</param>
internal sealed record RepeatOptions([property: RepeatedScalar(RepeatedScalarBehaviour.TakeLast)] string Level = "", string Tag = "");

/// <summary>Namespace options with a member whose last value wins.</summary>
internal sealed class RepeatNamespaceOptions : TestGlobalCliOptions
{
	/// <summary>A region; the last value wins.</summary>
	[RepeatedScalar(RepeatedScalarBehaviour.TakeLast)]
	public string Region { get; set; } = "";

	/// <summary>A stage.</summary>
	public string Stage { get; set; } = "";
}
