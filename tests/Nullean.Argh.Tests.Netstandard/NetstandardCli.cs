using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Nullean.Argh;
using Nullean.Argh.Middleware;
using Nullean.Argh.Parsing;

namespace Nullean.Argh.Tests.Netstandard;

/// <summary>Entry point for the netstandard2.0 fixture CLI. The hosts call it, so this assembly's generated code runs.</summary>
public static class NetstandardCli
{
	/// <summary>Registers the fixture commands and runs them.</summary>
	public static Task<int> RunAsync(string[] args)
	{
		var app = new ArghApp();
		app.UseGlobalOptions<NsGlobalOptions>();
		app.UseMiddleware<NsMiddleware>();
		app.Map("echo", NsHandlers.Echo);
		app.Map("scalars", NsHandlers.Scalars);
		app.Map("lists", NsHandlers.Lists);
		app.Map("point", NsHandlers.Point);
		app.Map("dto", NsHandlers.Dto);
		app.Map("validate", NsHandlers.Validate);
		app.Map("checks", NsHandlers.Checks);
		app.Map("env", NsHandlers.Env);
		app.Map("files", NsHandlers.Files);
		app.Map("copy", NsHandlers.Copy);
		app.Map("sets", NsHandlers.Sets);
		app.Map("pause", NsHandlers.Pause);
		app.Map("lambda", (string text) => Console.Out.WriteLine("lambda:" + text));
		app.Map("oblivious", NsObliviousHandlers.Oblivious);
		app.Map("oblivious-dto", NsObliviousHandlers.ObliviousDto);
		app.MapRoot(NsHandlers.Root);
		app.MapNamespace<NsStorageCommands>("storage", g =>
		{
			g.UseNamespaceOptions<NsStorageOptions>();
			g.MapNamespace<NsBlobCommands>("blob");
		});
		return app.RunAsync(args);
	}
}

/// <summary>Global options.</summary>
public class NsGlobalOptions
{
	/// <summary>-v, Verbose output.</summary>
	public bool Verbose { get; set; }

	/// <summary>Output colour.</summary>
	public NsColour Colour { get; set; } = NsColour.OceanBlue;
}

/// <summary>Options for the storage namespace.</summary>
public class NsStorageOptions : NsGlobalOptions
{
	/// <summary>Key prefix.</summary>
	public string Prefix { get; set; } = "";
}

/// <summary>A colour with CLI names.</summary>
public enum NsColour
{
	/// <summary>Red.</summary>
	[EnumValue("fire-red")] FireRed,

	/// <summary>Blue.</summary>
	[EnumValue("ocean-blue")] OceanBlue,

	/// <summary>Green.</summary>
	Green
}

/// <summary>A point parsed from <c>x,y</c>.</summary>
public readonly struct NsPoint
{
	/// <summary>Creates a point.</summary>
	public NsPoint(int x, int y)
	{
		X = x;
		Y = y;
	}

	/// <summary>X.</summary>
	public int X { get; }

	/// <summary>Y.</summary>
	public int Y { get; }
}

/// <summary>Parses <c>x,y</c> into a <see cref="NsPoint"/>.</summary>
public sealed class NsPointParser : IArgumentParser<NsPoint>
{
	/// <inheritdoc />
	public bool TryParse(string raw, out NsPoint value)
	{
		var parts = raw.Split(',');
		if (parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
		{
			value = new NsPoint(x, y);
			return true;
		}

		value = default;
		return false;
	}
}

/// <summary>Options bound with [AsParameters].</summary>
public sealed class NsDtoArgs
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = "";

	/// <summary>Optional count.</summary>
	public int? Count { get; set; }

	/// <summary>Optional wait.</summary>
	public TimeSpan? Wait { get; set; }
}

/// <summary>Writes a marker line before each command.</summary>
public sealed class NsMiddleware : ICommandMiddleware
{
	/// <inheritdoc />
	public async ValueTask InvokeAsync(CommandContext context, CommandMiddlewareDelegate next)
	{
		Console.Error.WriteLine("[ns:middleware]");
		await next(context);
	}
}

/// <summary>Commands.</summary>
public static class NsHandlers
{
	/// <summary>Echoes a message.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="message">-m, The message.</param>
	/// <param name="times">How many times.</param>
	public static void Echo(NsGlobalOptions g, string message, int times = 1)
	{
		for (var i = 0; i < times; i++)
			Console.Out.WriteLine($"echo:{message}:{g.Colour}:{g.Verbose}");
	}

	/// <summary>Special scalar types.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="wait">A duration.</param>
	/// <param name="when">A date and time.</param>
	/// <param name="at">A date and time with an offset.</param>
	/// <param name="link">A URI.</param>
	/// <param name="file">A file.</param>
	/// <param name="dir">A directory.</param>
	/// <param name="ratio">A double.</param>
	/// <param name="big">A long.</param>
	/// <param name="amount">A decimal.</param>
	/// <param name="note">An optional note.</param>
	/// <param name="limit">An optional int.</param>
	/// <param name="flag">An optional bool.</param>
	public static void Scalars(NsGlobalOptions g, TimeSpan wait, DateTime when, DateTimeOffset at, Uri link,
		FileInfo file, DirectoryInfo dir, double ratio, long big, decimal amount,
		string? note = null, int? limit = null, bool? flag = null) =>
		Console.Out.WriteLine(string.Join("|",
			wait.TotalSeconds.ToString(CultureInfo.InvariantCulture), when.ToString("o"), at.ToString("o"), link.Host,
			file.Name, dir.Name, ratio.ToString(CultureInfo.InvariantCulture), big.ToString(CultureInfo.InvariantCulture),
			amount.ToString(CultureInfo.InvariantCulture), note ?? "none",
			limit?.ToString(CultureInfo.InvariantCulture) ?? "none", flag?.ToString() ?? "none"));

	/// <summary>Collections.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="ids">Comma-separated ids.</param>
	/// <param name="tags">Repeatable tags.</param>
	/// <param name="colours">Repeatable colours.</param>
	public static void Lists(NsGlobalOptions g, [CollectionSyntax(Separator = ",")] List<int> ids, List<string> tags, List<NsColour> colours) =>
		Console.Out.WriteLine($"ids:{string.Join(",", ids)}|tags:{string.Join(",", tags)}|colours:{string.Join(",", colours)}");

	/// <summary>A custom-parsed point.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="at">The point.</param>
	public static void Point(NsGlobalOptions g, [ArgumentParser(typeof(NsPointParser))] NsPoint at) =>
		Console.Out.WriteLine($"point:{at.X},{at.Y}");

	/// <summary>An [AsParameters] DTO.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="args">The options.</param>
	public static void Dto(NsGlobalOptions g, [AsParameters] NsDtoArgs args) =>
		Console.Out.WriteLine(
			$"dto:{args.Name}|{args.Count?.ToString(CultureInfo.InvariantCulture) ?? "none"}|{args.Wait?.TotalSeconds.ToString(CultureInfo.InvariantCulture) ?? "none"}");

	/// <summary>Validation attributes.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="port">A port.</param>
	/// <param name="name">A name.</param>
	public static void Validate(NsGlobalOptions g, [Range(1, 65535)] int port, [StringLength(10, MinimumLength = 2)] string name) =>
		Console.Out.WriteLine($"validate:{port}:{name}");

	/// <summary>The other validation attributes netstandard2.0 has.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="slug">Lower-case letters.</param>
	/// <param name="email">An email address.</param>
	/// <param name="site">A URL.</param>
	/// <param name="code">At least two characters.</param>
	/// <param name="tag">At most five characters.</param>
	/// <param name="window">Between one second and one hour.</param>
	/// <param name="endpoint">An https URI.</param>
	/// <param name="doc">A .txt file.</param>
	public static void Checks(NsGlobalOptions g, [RegularExpression("^[a-z]+$")] string slug,
		[EmailAddress] string? email = null, [Url] string? site = null, [MinLength(2)] string? code = null,
		[MaxLength(5)] string? tag = null, [TimeSpanRange("1s", "1h")] TimeSpan? window = null,
		[UriScheme("https")] Uri? endpoint = null, [FileExtensions(Extensions = "txt")] FileInfo? doc = null) =>
		Console.Out.WriteLine($"checks:{slug}");

	/// <summary>A value read from an environment variable.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="token">The token.</param>
	public static void Env(NsGlobalOptions g, [Env("NS_FIXTURE_TOKEN")] string? token = null) =>
		Console.Out.WriteLine($"env:{token ?? "none"}");

	/// <summary>File-system checks.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="input">An existing file.</param>
	/// <param name="output">A directory under the user profile.</param>
	public static void Files(NsGlobalOptions g, [Existing] FileInfo input, [ExpandUserProfile] DirectoryInfo? output = null) =>
		Console.Out.WriteLine($"files:{input.Name}:{output?.Name ?? "none"}");

	/// <summary>Positional arguments and an array flag.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="source">The source.</param>
	/// <param name="target">The target.</param>
	/// <param name="exclude">Patterns to skip.</param>
	public static void Copy([Argument] string source, [Argument] string target, NsGlobalOptions g, string[] exclude) =>
		Console.Out.WriteLine($"copy:{source}>{target}|{string.Join(",", exclude)}");

	/// <summary>Enumerable and read-only list collections.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="keys">Unique keys.</param>
	/// <param name="nums">Numbers.</param>
	public static void Sets(NsGlobalOptions g, IEnumerable<string> keys, IReadOnlyList<int> nums)
	{
		var sorted = new List<string>(keys);
		sorted.Sort(StringComparer.Ordinal);
		Console.Out.WriteLine($"sets:{string.Join(",", sorted)}|{string.Join(",", nums)}");
	}

	/// <summary>An async command with a cancellation token.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="ms">How long to wait.</param>
	/// <param name="ct">Cancellation.</param>
	public static async Task Pause(NsGlobalOptions g, int ms, System.Threading.CancellationToken ct)
	{
		await Task.Delay(ms, ct);
		Console.Out.WriteLine($"pause:{ms}");
	}

	/// <summary>Runs when no command is given.</summary>
	/// <param name="g">Global options.</param>
	public static void Root(NsGlobalOptions g) => Console.Out.WriteLine("root");
}

/// <summary>Blob commands.</summary>
public sealed class NsBlobCommands
{
	/// <summary>Uploads a blob.</summary>
	/// <param name="o">Namespace options.</param>
	/// <param name="name">The blob name.</param>
	public static void Upload([Argument] string name, NsStorageOptions o) =>
		Console.Out.WriteLine($"blob-upload:{o.Prefix}:{name}");
}

/// <summary>Storage commands.</summary>
public sealed class NsStorageCommands
{
	/// <summary>Lists keys.</summary>
	/// <param name="o">Namespace options.</param>
	/// <param name="limit">How many.</param>
	public static void List(NsStorageOptions o, int limit = 10) =>
		Console.Out.WriteLine($"storage-list:{o.Prefix}:{limit}:{o.Colour}");
}
