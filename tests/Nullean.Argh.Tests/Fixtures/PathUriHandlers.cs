using Nullean.Argh;

namespace Nullean.Argh.Tests.Fixtures;

/// <summary><see cref="UriOptionsAttribute"/> on <see cref="AsParametersAttribute"/> members.</summary>
internal sealed record UriKindDtoArgs(
	Uri Site,
	[UriOptions(UriKind.Relative)] Uri? Path = null,
	[UriOptions(UriKind.RelativeOrAbsolute)] Uri? Link = null);

/// <summary>Namespace options with paths and URIs, each with an environment variable.</summary>
internal sealed class PathUriNamespaceOptions : TestGlobalCliOptions
{
	/// <summary>A file.</summary>
	[Env("ARGH_TEST_NS_DOC")]
	public FileInfo? Doc { get; set; }

	/// <summary>A relative URI.</summary>
	[Env("ARGH_TEST_NS_BASE")]
	[UriOptions(UriKind.Relative)]
	public Uri? Base { get; set; }
}

/// <summary>Files, directories and URIs in each binding position.</summary>
internal static class PathUriHandlers
{
	/// <summary>Files and directories as a positional, flags, collections and an environment variable.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="source">The source file.</param>
	/// <param name="target">A directory.</param>
	/// <param name="doc">A file from a flag or ARGH_TEST_DOC.</param>
	/// <param name="files">Files.</param>
	/// <param name="dirs">Comma-separated directories.</param>
	public static void PathInputs([Argument] FileInfo source, TestGlobalCliOptions g, DirectoryInfo? target = null,
		[Env("ARGH_TEST_DOC", TreatEmptyAsUnset = false)] FileInfo? doc = null, List<FileInfo>? files = null,
		[CollectionSyntax(Separator = ",")] DirectoryInfo[]? dirs = null) =>
		Console.Out.WriteLine(
			$"paths:[{source.Name}] target:[{target?.Name ?? "none"}] doc:[{doc?.Name ?? "none"}] " +
			$"files:[{(files is null ? "none" : string.Join(";", files.Select(static f => f.Name)))}] " +
			$"dirs:[{(dirs is null ? "none" : string.Join(";", dirs.Select(static d => d.Name)))}]");

	/// <summary>URIs of each kind as a positional, flags, a collection and an environment variable.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="endpoint">An absolute URI.</param>
	/// <param name="mirror">An optional absolute URI.</param>
	/// <param name="path">A relative URI.</param>
	/// <param name="link">A relative or absolute URI.</param>
	/// <param name="home">An absolute URI from a flag or ARGH_TEST_HOME.</param>
	/// <param name="links">Relative or absolute URIs.</param>
	public static void UriInputs([Argument] Uri endpoint, TestGlobalCliOptions g, Uri? mirror = null,
		[UriOptions(UriKind.Relative)] Uri? path = null, [UriOptions(UriKind.RelativeOrAbsolute)] Uri? link = null,
		[Env("ARGH_TEST_HOME")] Uri? home = null, [UriOptions(UriKind.RelativeOrAbsolute)] List<Uri>? links = null) =>
		Console.Out.WriteLine(
			$"uris:[{endpoint}] mirror:[{mirror?.ToString() ?? "none"}] path:[{path?.ToString() ?? "none"}] " +
			$"link:[{link?.ToString() ?? "none"}] home:[{home?.ToString() ?? "none"}] " +
			$"links:[{(links is null ? "none" : string.Join(";", links))}]");

	/// <summary>Relative URIs as a variadic positional.</summary>
	/// <param name="paths">Relative URIs.</param>
	[NoOptionsInjection]
	public static void UriVariadic([Argument][UriOptions(UriKind.Relative)] params Uri[] paths) =>
		Console.Out.WriteLine("uri-variadic:" + string.Join(";", paths.Select(static p => p.ToString())));

	/// <summary>URIs of each kind on an [AsParameters] DTO.</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="args">The URIs.</param>
	public static void UriDto(TestGlobalCliOptions g, [AsParameters] UriKindDtoArgs args) =>
		Console.Out.WriteLine($"uri-dto:[{args.Site}] path:[{args.Path?.ToString() ?? "none"}] link:[{args.Link?.ToString() ?? "none"}]");

	/// <summary>Shows the namespace's file and URI.</summary>
	/// <param name="o">The namespace options.</param>
	public static void NsShow(PathUriNamespaceOptions o) =>
		Console.Out.WriteLine($"path-uri-ns:doc:[{o.Doc?.Name ?? "none"}] base:[{o.Base?.ToString() ?? "none"}]");
}
