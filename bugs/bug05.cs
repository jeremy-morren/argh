// Bug 05 - An empty value for a required FileInfo or DirectoryInfo crashes with a stack trace
// Group A: parsing and binding
//
// Run:      Bugs.exe bug05 --file ""
// Actual:   Unhandled exception. System.ArgumentException: The path is empty. (Parameter 'path')  exit 1
// Expected: a parse error, exit 2   (clap PathBufValueParser: "a value is required for '--file <FILE>' but none was supplied")
//
// Also: --file "  ", and --dir "" for DirectoryInfo. The optional FileInfo? path guards against it.
// Cause: Emit.Runner.cs:1217-1228 (FileInfo) and :1260-1271 (DirectoryInfo) call new FileInfo(raw!)
//        with no IsNullOrWhiteSpace guard; the optional branches at :1197 and :1240 have one.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug05
{
	/// <summary>Takes a required file and an optional directory.</summary>
	[NoOptionsInjection]
	public static void Run(FileInfo file, DirectoryInfo? dir = null) =>
		Console.Out.WriteLine($"file:{file.Name} dir:{dir?.Name}");

	public static void Register(IArghRootBuilder app) => app.Map("bug05", Run);
}
