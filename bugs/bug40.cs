// Bug 40 - Global and namespace options are duplicated into every command's `parameters`
// Group E: completion and __schema
//
// Run:      Bugs.exe __schema
// Actual:   "globalOptions" lists api-url, severity and port, and so does every command's "parameters"
//           (for example bug03); namespace options (bug28's prefix and timeout) repeat inside "bug28 show".
// Expected: each option once, at the level where it's declared. cli-schema v1 (spec/v1/README.md at 7842ac9)
//           defines globalOptions as "Flags available on every command" (L103), namespace options as "Flags
//           available on all commands within this namespace" (L259) and command parameters as "Command-specific
//           flags and positionals" (L231); its git and gh examples never repeat a global option in a command.

namespace Bugs;

internal static class Bug40
{
	// No code: observe any command's schema entry, e.g. bug01.
}
