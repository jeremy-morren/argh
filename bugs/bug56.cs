// Bug 56 - Handlers in a referenced assembly get no documentation at all
// Group D: help and XML docs
//
// Run:      Bugs.exe bug56 --help
// Actual:   no summary, no description for --name, and its -n short option is gone
//           (the handler lives in bugs/lib/bug56.cs, whose project generates an XML doc file)
// Expected: the library's docs, as an [AsParameters] DTO from the same library already gets
//
// Cause: GetDocumentationXmlFromMetadataReference (Documentation.cs:432) reads the doc file for DTOs only;
//        CommandModel.cs:81, 116 has no metadata-XML fallback for handlers. The compiled-XML path in
//        Documentation.ParseMethod is dead for in-source handlers too (the <member> wrapper makes
//        root.Element("summary") at :388 always null), so the trivia path always runs.

using Bugs.Lib;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug56
{
	public static void Register(IArghRootBuilder app) => app.Map<Bug56Commands>();
}
