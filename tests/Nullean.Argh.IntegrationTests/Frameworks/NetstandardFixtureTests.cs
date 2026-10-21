using System.Runtime.InteropServices;
using System.Text.Json;
using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using ProcNet;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Frameworks;

/// <summary>
/// Runs the Nullean.Argh.Tests.Netstandard fixture on each supported runtime, through hosts that load the intended
/// builds of the fixture and of the Argh libraries. Each command must behave the same everywhere.
/// </summary>
/// <remarks>
/// A host whose runtime isn't installed is skipped. Set <c>ARGH_REQUIRE_ALL_FRAMEWORKS=1</c> (CI does) to fail instead.
/// The .NET Framework host only runs on Windows, so it is skipped elsewhere regardless.
/// </remarks>
public class NetstandardFixtureTests
{
	/// <summary>The net10.0 host that loads the netstandard2.0 builds of the fixture and the Argh libraries.</summary>
	public const string Netstandard20 = "netstandard2.0";

	/// <summary>The host each other host is compared with.</summary>
	private const string Baseline = "net10.0";

	private static readonly string[] Frameworks = [Netstandard20, "net472", "netcoreapp3.1", "net5.0", "net6.0", "net8.0", "net10.0"];

	/// <summary>Suffix for a host that loads the fixture built with nullable reference types disabled.</summary>
	private const string NullableDisabled = " (nullable disabled)";

	/// <summary>Every framework, with the nullable-enabled fixture and with the nullable-disabled one.</summary>
	private static readonly string[] Hosts = Frameworks.SelectMany(static f => new[] { f, f + NullableDisabled }).ToArray();

	private static string FrameworkOf(string host) =>
		host.EndsWith(NullableDisabled, StringComparison.Ordinal) ? host.Substring(0, host.Length - NullableDisabled.Length) : host;

	private const string ModuleInitializer = "System.Runtime.CompilerServices.ModuleInitializerAttribute";
	private const string NotNullWhen = "System.Diagnostics.CodeAnalysis.NotNullWhenAttribute";

	/// <summary>What each host must report: runtime prefix, fixture and Argh.Core builds, and the polyfills emitted.</summary>
	private static readonly Dictionary<string, (string Runtime, string Fixture, string Core, string[] Polyfills)> Expected = new()
	{
		[Netstandard20] = (".NET 10.", ".NETStandard,Version=v2.0", ".NETStandard,Version=v2.0", [ModuleInitializer, NotNullWhen]),
		["net472"] = (".NET Framework 4.", ".NETStandard,Version=v2.0", ".NETStandard,Version=v2.0", [ModuleInitializer, NotNullWhen]),
		["netcoreapp3.1"] = (".NET Core 3.1.", ".NETStandard,Version=v2.1", ".NETStandard,Version=v2.0", [ModuleInitializer]),
		["net5.0"] = (".NET 5.", ".NETStandard,Version=v2.1", ".NETStandard,Version=v2.0", [ModuleInitializer]),
		["net6.0"] = (".NET 6.", ".NETStandard,Version=v2.1", ".NETStandard,Version=v2.0", [ModuleInitializer]),
		["net8.0"] = (".NET 8.", ".NETCoreApp,Version=v8.0", ".NETCoreApp,Version=v8.0", []),
		["net10.0"] = (".NET 10.", ".NETCoreApp,Version=v10.0", ".NETCoreApp,Version=v10.0", []),
	};

	private static readonly bool RequireAllFrameworks = Environment.GetEnvironmentVariable("ARGH_REQUIRE_ALL_FRAMEWORKS") == "1";

	/// <summary>
	/// Installed Microsoft.NETCore.App versions, read from the shared-framework folder next to the running runtime
	/// (<c>dotnet/shared/Microsoft.NETCore.App/&lt;version&gt;</c>), which is the one <c>dotnet exec</c> uses.
	/// </summary>
	private static readonly string[] InstalledRuntimes =
		Directory.GetDirectories(Path.GetDirectoryName(Path.GetDirectoryName(typeof(object).Assembly.Location)!)!)
			.Select(static d => Path.GetFileName(d)!)
			.ToArray();

	private static string HostPath(string host)
	{
		var framework = FrameworkOf(host);
		var suffix = framework != host ? "-nullable-disabled" : "";
		if (framework == Netstandard20)
			return Path.Combine(AppContext.BaseDirectory, "netstandard-host" + suffix, "Nullean.Argh.Tests.NetstandardHost.dll");

		// fw/<configuration>_<tfm>; only the current configuration is copied.
		var dir = Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "fw" + suffix), "*_" + framework).Single();
		return Path.Combine(dir, "Nullean.Argh.Tests.FrameworkHost" + (framework == "net472" ? ".exe" : ".dll"));
	}

	/// <summary>Skips the test when the host's runtime isn't available, or fails if every framework is required.</summary>
	private static void RequireHost(string host)
	{
		var framework = FrameworkOf(host);
		if (framework == "net472")
		{
			Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), ".NET Framework hosts only run on Windows.");
			return;
		}

		var runtime = framework switch
		{
			Netstandard20 => "10.0.",
			"netcoreapp3.1" => "3.1.",
			_ => framework.Substring("net".Length) + ".",
		};
		if (InstalledRuntimes.Any(v => v.StartsWith(runtime, StringComparison.Ordinal)))
			return;

		var reason = $"The Microsoft.NETCore.App {runtime}x runtime that the {framework} host needs isn't installed.";
		if (RequireAllFrameworks)
			Assert.Fail(reason + " ARGH_REQUIRE_ALL_FRAMEWORKS=1 requires every host to run.");
		Skip.If(true, reason);
	}

	private static ProcessCaptureResult Run(string framework, params string[] args) =>
		Run(framework, environment: null, args);

	private static ProcessCaptureResult Run(string framework, IReadOnlyDictionary<string, string>? environment, params string[] args)
	{
		RequireHost(framework);
		return CliHostRunner.RunHost(HostPath(framework), environment, args);
	}

	private static string Stdout(ProcessCaptureResult result) => CliHostRunner.StdoutText(result);

	public static TheoryData<string> AllFrameworks()
	{
		var data = new TheoryData<string>();
		foreach (var framework in Hosts)
			data.Add(framework);
		return data;
	}

	private static readonly (string[] Args, string Expected)[] CommandCases =
	[
		(["echo", "-m", "hi", "--times=2"], "echo:hi:OceanBlue:False\necho:hi:OceanBlue:False\n"),
		(["--colour", "fire-red", "-v", "echo", "--message", "hi"], "echo:hi:FireRed:True\n"),
		([
			"scalars", "--wait", "5m", "--when", "2024-06-15T14:30:00Z", "--at", "2024-06-15T14:30:00+10:00",
			"--link", "https://example.com", "--file", "a.txt", "--dir", "out", "--ratio", "1.5", "--big", "9000000000",
			"--amount=2.25", "--limit=7", "--flag=true", "--note", "n",
		], "300|2024-06-15T14:30:00.0000000Z|2024-06-15T14:30:00.0000000+10:00|example.com|a.txt|out|1.5|9000000000|2.25|n|7|True\n"),
		(["lists", "--ids=1,2,3", "--tags", "a", "--tags", "b", "--colours", "green", "--colours", "fire-red"],
			"ids:1,2,3|tags:a,b|colours:Green,FireRed\n"),
		(["point", "--at", "3,4"], "point:3,4\n"),
		(["dto", "--name", "n", "--count", "5", "--wait", "90s"], "dto:n|5|90\n"),
		(["validate", "--port", "80", "--name", "ab"], "validate:80:ab\n"),
		([
			"checks", "--slug", "abc", "--email", "a@b.example", "--site", "https://site.example", "--code", "ab",
			"--tag", "abc", "--window", "5m", "--endpoint", "https://api.example", "--doc", "notes.txt",
		], "checks:abc\n"),
		(["copy", "src", "dst", "--exclude", "x", "--exclude", "y"], "copy:src>dst|x,y\n"),
		(["sets", "--keys", "b", "--keys", "a", "--nums", "1", "--nums", "2"], "sets:a,b|1,2\n"),
		(["pause", "--ms", "1"], "pause:1\n"),
		(["lambda", "--text", "hi"], "lambda:hi\n"),
		([], "root\n"),
		(["storage", "--prefix", "p", "list", "--limit", "3"], "storage-list:p:3:OceanBlue\n"),
		(["storage", "--prefix", "p", "blob", "upload", "thing"], "blob-upload:p:thing\n"),
		(["__complete", "bash", "--", "stor"], "storage\n"),
		// Unannotated reference types: oblivious in both builds, and in the shared file too when nullable is disabled.
		(["oblivious", "--name", "n"], "oblivious:n|none|none|none|none\n"),
		([
			"oblivious", "--name", "n", "--note", "x", "--link", "https://l.example", "--file", "f.txt", "--tags", "a", "--tags", "b",
		], "oblivious:n|x|l.example|f.txt|a,b\n"),
		(["oblivious-dto", "--name", "n"], "oblivious-dto:n|preset\n"),
	];

	public static TheoryData<string, string[], string> Commands()
	{
		var data = new TheoryData<string, string[], string>();
		foreach (var framework in Hosts)
		{
			foreach (var (args, expected) in CommandCases)
				data.Add(framework, args, expected);
		}

		return data;
	}

	private static readonly string[][] InvalidCases =
	[
		["validate", "--port", "0", "--name", "ab"],
		["checks", "--slug", "ABC"],
		["scalars", "--wait", "soon"],
		// Required because they have no default value, whether non-nullable or oblivious.
		["echo"],
		["oblivious"],
		["oblivious-dto"],
	];

	public static TheoryData<string, string[]> InvalidCommands()
	{
		var data = new TheoryData<string, string[]>();
		foreach (var framework in Hosts)
		{
			foreach (var args in InvalidCases)
				data.Add(framework, args);
		}

		return data;
	}

	public static TheoryData<string, string[]> HelpCommands()
	{
		var data = new TheoryData<string, string[]>();
		foreach (var framework in Hosts)
		{
			data.Add(framework, ["--help"]);
			data.Add(framework, ["scalars", "--help"]);
			data.Add(framework, ["storage", "--help"]);
			data.Add(framework, ["oblivious", "--help"]);
			data.Add(framework, ["oblivious-dto", "--help"]);
		}

		return data;
	}

	[SkippableTheory]
	[MemberData(nameof(AllFrameworks))]
	public void Host_loads_the_intended_runtime_and_builds(string framework)
	{
		var lines = Stdout(Run(framework, "--framework-info")).Split('\n', StringSplitOptions.RemoveEmptyEntries);
		var expected = Expected[FrameworkOf(framework)];

		lines.Should().HaveCount(3);
		lines[0].Should().StartWith("runtime: " + expected.Runtime);
		lines[1].Should().Be("fixture: " + expected.Fixture);
		lines[2].Should().Be("core: " + expected.Core);
	}

	[SkippableTheory]
	[MemberData(nameof(AllFrameworks))]
	public void Polyfills_are_emitted_only_where_the_framework_lacks_the_attributes(string framework)
	{
		var lines = Stdout(Run(framework, "--list-polyfills")).Split('\n', StringSplitOptions.RemoveEmptyEntries);
		lines.Should().Equal(Expected[FrameworkOf(framework)].Polyfills);
	}

	[SkippableTheory]
	[MemberData(nameof(Commands))]
	public void Command_output_is_the_same_on_every_framework(string framework, string[] args, string expected)
	{
		var result = Run(framework, args);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		Stdout(result).Should().Be(expected);
	}

	[SkippableTheory]
	[MemberData(nameof(InvalidCommands))]
	public void Invalid_values_exit_2(string framework, string[] args) =>
		Run(framework, args).ExitCode.Should().Be(2);

	[SkippableTheory]
	[MemberData(nameof(AllFrameworks))]
	public void Environment_variable_binds(string framework)
	{
		var result = Run(framework, new Dictionary<string, string> { ["NS_FIXTURE_TOKEN"] = "tok" }, "env");
		result.ExitCode.Should().Be(0);
		Stdout(result).Should().Be("env:tok\n");
	}

	[SkippableTheory]
	[MemberData(nameof(AllFrameworks))]
	public void Existing_file_check_accepts_a_real_file(string framework)
	{
		var result = Run(framework, "files", "--input", HostPath(framework));
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		Stdout(result).Should().Be($"files:{Path.GetFileName(HostPath(framework))}:none\n");
	}

	[SkippableTheory]
	[MemberData(nameof(AllFrameworks))]
	public void Completion_after_a_namespace_matches_the_baseline(string framework)
	{
		var output = Stdout(Run(framework, "__complete", "bash", "--", "storage", ""));
		output.Should().Contain("blob\n").And.Contain("list\n");
		output.Should().Be(Stdout(Run(Baseline, "__complete", "bash", "--", "storage", "")));
	}

	[SkippableTheory]
	[MemberData(nameof(HelpCommands))]
	public void Help_matches_the_baseline(string framework, string[] args)
	{
		var result = Run(framework, args);
		result.ExitCode.Should().Be(0);
		Stdout(result).Should().Contain("Usage:");
		Stdout(result).Should().Be(Stdout(Run(Baseline, args)));
	}

	[SkippableTheory]
	[MemberData(nameof(AllFrameworks))]
	public void Schema_matches_the_baseline(string framework)
	{
		var output = Stdout(Run(framework, "__schema"));
		using (var doc = JsonDocument.Parse(output))
			doc.RootElement.GetProperty("commands").GetArrayLength().Should().BeGreaterThan(10);
		output.Should().Be(Stdout(Run(Baseline, "__schema")));
	}
}
