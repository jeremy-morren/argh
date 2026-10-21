// Bug 85 - The incremental generator's cache never hits: every edit re-runs the whole emit
// Group B: generator - uncompilable output and missing diagnostics (performance)
//
// Not reproducible from this scratch app: it shows up in an IDE (the generator re-runs on every keystroke)
// or in a GeneratorDriver test with step tracking on, where the `Argh.AnalyzedInvocation` step reports
// `Modified` after an unrelated edit such as adding a comment. Session argh-a5 has such a test, skipped,
// in IncrementalCachingTests on its netstandard-fixes branch.
//
// Cause: AIMapCommand (CliParserGenerator.Models.cs:206-209) carries `ImmutableArray<CommandModel> Commands`,
// and the nested models hold more ImmutableArrays. ImmutableArray<T>.Equals is reference equality of the
// backing array, and the generator has no equatable wrapper and no custom Equals, so two runs never produce
// equal records and the collected invocations and the main emit re-run every time. CLAUDE.md says these
// records are "symbol-free ... that Roslyn can cache per invocation"; they are symbol-free but not cacheable.
//
// Fix: an EquatableArray<T> wrapper (structural equality and hash) used throughout the models, as the
// System.Text.Json source generator does.

namespace Bugs;

internal static class Bug85
{
	// No code: see the comment above.
}
