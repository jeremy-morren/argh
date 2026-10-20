// Bug 60 - Reserved meta-command names can be mapped as commands; AGH0013 never fires
// Group B: generator - uncompilable output and missing diagnostics
//
// Build:    dotnet build bugs -c Release -p:DefineConstants=BUG60     (compiles silently)
// Run:      Bugs.exe __schema foo       -> USER __schema RAN       (IsSchemaInvocation needs exactly one argument)
//           Bugs.exe __complete         -> the intrinsic's usage error; the user command is unreachable
//           Bugs.exe --help             -> lists __schema, __complete, __completion and __argh_root as commands
//           Bugs.exe __schema           -> the schema's "commands" array contains them next to "reservedMetaCommands"
// Expected: an AGH0013 error for each reserved name
//
// Cause: AGH0013 ReservedCommandNameRoot (Diagnostics.cs:123) is defined but never created.

#if BUG60
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug60
{
	/// <summary>User command named like a reserved meta-command.</summary>
	[NoOptionsInjection]
	public static void Run() => Console.Out.WriteLine("USER __schema RAN");

	public static void Register(IArghRootBuilder app)
	{
		app.Map("__schema", Run);
		app.Map("__complete", Run);
		app.Map("__completion", Run);
		app.Map("__argh_root", Run);
	}
}
#endif
