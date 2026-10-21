// Bug 91 - Negative range bounds render ambiguously in help: [range: -5-5]
// Group D: help and XML docs
//
// Run:      Bugs.exe bug91 --help
// Actual:   --offset <int>  ... [range: -5-5]
// Expected: an unambiguous form, for example [range: -5..5] (clap prints ranges as 1..=10) or [range: -5 to 5]
//
// The error message is fine ("value must be between -5 and 5"); only the help hint is ambiguous.
// Reported by session argh-a5 while building range-unbounded (#90).

using System.ComponentModel.DataAnnotations;
using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug91
{
	/// <summary>A range with a negative lower bound.</summary>
	/// <param name="offset">Between -5 and 5.</param>
	[NoOptionsInjection]
	public static void Run([Range(-5, 5)] int offset = 0) => Console.Out.WriteLine($"offset:{offset}");

	public static void Register(IArghRootBuilder app) => app.Map("bug91", Run);
}
