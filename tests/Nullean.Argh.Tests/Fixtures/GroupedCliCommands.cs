using Nullean.Argh;

namespace Nullean.Argh.Tests.Fixtures;

/// <summary>Enum used by <see cref="TestGlobalCliOptions"/> for global-option default parsing tests.</summary>
internal enum FixtureSeverity
{
	Trace,
	Information,
	Warning
}

/// <summary>Second enum for <see cref="CliTestHandlers.MultiEnumAsParamsArgs"/> (mixed nullability, CS0136 fix).</summary>
internal enum FixtureConfigSource
{
	File,
	Environment
}

/// <summary>Global CLI flags (tests).</summary>
internal class TestGlobalCliOptions
{
	/// <summary>-v</summary>
	public bool Verbose { get; set; }

	/// <summary>Enum default for global-flag parsing regression.</summary>
	public FixtureSeverity Severity { get; set; } = FixtureSeverity.Information;

	/// <summary>-m, Test-only global mode label (non-bool global short-option coverage).</summary>
	public string Mode { get; set; } = "";

	/// <summary>API URL for env-binding global options tests.</summary>
	[Env("TEST_API_URL")]
	[Hidden]
	public string? ApiUrl { get; set; }
}

/// <summary>Storage command namespace options; must inherit global options type.</summary>
internal sealed class TestStorageCommandNamespaceOptions : TestGlobalCliOptions
{
	public string Prefix { get; set; } = "";
}

/// <summary>Namespace options with an env-bound property for integration tests.</summary>
internal sealed class EnvTestNamespaceOptions : TestGlobalCliOptions
{
	/// <summary>Namespace-scoped key from env var fallback.</summary>
	[Env("TEST_NS_KEY")]
	public string? Key { get; set; }
}

/// <summary>Namespace options with each special scalar type, plain (required) and nullable.</summary>
internal sealed class ScalarNamespaceOptions : TestGlobalCliOptions
{
	public TimeSpan Span { get; set; }
	public TimeSpan? SpanOpt { get; set; }
	public DateTime Stamp { get; set; }
	public DateTime? StampOpt { get; set; }
	public DateTimeOffset Offset { get; set; }
	public DateTimeOffset? OffsetOpt { get; set; }
	public DateOnly Day { get; set; }
	public DateOnly? DayOpt { get; set; }
	public TimeOnly Clock { get; set; }
	public TimeOnly? ClockOpt { get; set; }
	public Uri Link { get; set; } = null!;
	public Uri? LinkOpt { get; set; }
	public FileInfo Doc { get; set; } = null!;
	public FileInfo? DocOpt { get; set; }
	public DirectoryInfo Folder { get; set; } = null!;
	public DirectoryInfo? FolderOpt { get; set; }
}

/// <summary>Commands under <c>storage</c>. Nested <see cref="BlobCommands"/> must be registered explicitly via <c>MapNamespace&lt;BlobCommands&gt;</c>.</summary>
internal sealed class StorageCliCommands
{
	public static void List(TestStorageCommandNamespaceOptions o)
	{
		var tail = string.IsNullOrEmpty(o.Mode) ? "" : ":" + o.Mode;
		Console.Out.WriteLine("storage-list" + tail);
	}

	public sealed class BlobCommands
	{
		public static void Upload(TestStorageCommandNamespaceOptions o) => Console.Out.WriteLine("blob-upload");
	}
}

internal interface IDiProbeService
{
	string Marker { get; }
}

internal sealed class DiProbeService : IDiProbeService
{
	public string Marker => "from-di";
}

/// <summary>Instance command type for DI resolution tests (<c>ArghServices.ServiceProvider</c>).</summary>
internal sealed class DiProbeCommands(IDiProbeService svc)
{
	public void Ping(TestGlobalCliOptions g) =>
		Console.Out.WriteLine($"probe:{svc.Marker}");
}

/// <summary>Commands whose CLI name is overridden via <c>[CommandName]</c>.</summary>
internal sealed class CommandNameOverrideCommands
{
	[CommandName("renamed-cmd", "rc")]
	public static void OriginalMethodName(TestGlobalCliOptions g) =>
		Console.Out.WriteLine("marker:renamed-cmd");
}

/// <summary>Commands whose CLI names and aliases are not ASCII, set via <c>[CommandName]</c>.</summary>
internal sealed class UnicodeNameCommands
{
	/// <summary>Accented command name.</summary>
	[CommandName("café", "kafé")]
	public static void Cafe(TestGlobalCliOptions g) =>
		Console.Out.WriteLine("marker:unicode-cafe");

	/// <summary>CJK command name.</summary>
	[CommandName("日本語")]
	public static void Nihongo(TestGlobalCliOptions g) =>
		Console.Out.WriteLine("marker:unicode-nihongo");
}
