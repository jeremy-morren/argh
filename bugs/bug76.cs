// Bug 76 - A bare number is a TimeSpan in days, and the compact duration format is undocumented and incomplete
// Group A: parsing and binding
//
// Run:      Bugs.exe bug76 --duration 10
// Actual:   duration:10.00:00:00 seconds:864000     (TimeSpan.TryParse fallback: 10 days)
// Expected: an error, or 10 seconds; Go's time.ParseDuration and Rust's humantime both reject a bare number
//
// Accepted: 10s, 90m, 1d, 00:30, 1:2 (1 h 2 min), 1.00:00:00.
// Rejected: 1.5h, 1h30m, -5m, 10ms, 1w, 0.5s, +5s, PT10S   (ParseDuration / humantime accept the first four)
// Help shows only <timeSpan>; docs/ never mentions the compact form. Code: src/Nullean.Argh.Core/ArghTimeSpan.cs.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug76
{
	/// <summary>A duration.</summary>
	[NoOptionsInjection]
	public static void Run(TimeSpan duration) =>
		Console.Out.WriteLine($"duration:{duration} seconds:{duration.TotalSeconds}");

	public static void Register(IArghRootBuilder app) => app.Map("bug76", Run);
}
