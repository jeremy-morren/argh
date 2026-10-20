// Bug 01 - An environment variable overrides a global flag given before the command
// Group A: parsing and binding
//
// Run:      BUGS_API_URL=fromenv Bugs.exe --api-url cli bug01
// Actual:   api-url:fromenv
// Expected: api-url:cli          (clap: CLI wins over env; viper: a changed flag wins over env)
//
// The same flag after the command works: Bugs.exe bug01 --api-url cli  ->  api-url:cli
// Cause: CliParserGenerator.Emit.Parsing.cs:420-433 reads the env var before the pre-parsed global.

using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug01
{
	/// <summary>Prints the global --api-url value.</summary>
	public static void Run(GlobalOpts g) => Console.Out.WriteLine($"api-url:{g.ApiUrl}");

	public static void Register(IArghRootBuilder app) => app.Map("bug01", Run);
}
