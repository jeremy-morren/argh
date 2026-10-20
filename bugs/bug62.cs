// Bug 62 - Completion lists flags inconsistently after a value flag, `--`, an unknown flag or a positional
// Group E: completion and __schema
//
//   context          | after value flag | after --  | after unknown | after positional
//   root             | flags            | nothing   | nothing       | nothing
//   namespace bug29  | nothing          | nothing   | nothing       | nothing
//   command bug62    | flags            | FLAGS     | FLAGS         | FLAGS
//
// Run:      Bugs.exe __complete bash -- bug62 -- -          -> --help -h --name ...   (expected: nothing after --)
//           Bugs.exe __complete bash -- bug62 --bogus -     -> flags                   (expected: nothing)
//           Bugs.exe __complete bash -- bug62 pos -         -> flags
//           Bugs.exe __complete bash -- -- -                -> nothing (root)
//           Bugs.exe __complete bash -- bug29 --prefix v -  -> nothing (namespace; command-level gives flags)
// Expected: the same rule at every level, following the parser state (clap's dynamic engine, cobra's __complete):
//           nothing after --, the flag's values after a value flag, flags otherwise.
//
// Cause: Completion.cs:110-113 assigns pos = ConsumeCommandTail(...) and then lists the command's flags
//        unconditionally; the root and namespace paths return at :95-96 as soon as a committed token starts with '-'.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug62
{
	/// <summary>Command with a flag and a positional.</summary>
	/// <param name="pos">A positional.</param>
	/// <param name="name">A name.</param>
	[NoOptionsInjection]
	public static void Run([Argument] string pos = "", string name = "") => Console.Out.WriteLine($"{name} {pos}");

	public static void Register(IArghRootBuilder app) => app.Map("bug62", Run);
}
