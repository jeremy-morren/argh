// Bug 40 - Global and namespace options are duplicated into every command's `parameters`
// Group E: completion and __schema (design)
//
// Run:      Bugs.exe __schema
// Actual:   "globalOptions" lists api-url and severity, and so does every command's "parameters"
//           (for example bug01); namespace options (bug28's prefix) repeat inside "bug28 show".
// Expected: once. cli-schema v1 defines globalOptions as "available on every command", so consumers see
//           every flag twice.
//
// A design choice rather than a defect, but it should be decided.

namespace Bugs;

internal static class Bug40
{
	// No code: observe any command's schema entry, e.g. bug01.
}
