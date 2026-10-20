// Bug 47 - <see cref="M(System.String)"/> renders as "String)"
// Group D: help and XML docs
//
// Run:      Bugs.exe --help | grep bug47       and       Bugs.exe bug47 --help
// Actual:   bug47   Writes with String) to the console.
// Expected: bug47   Writes with WriteLine to the console.   (or WriteLine(string))
//
// The cref must spell the parameter type with a dot, as compiled XML-doc crefs always do (M:...(System.String));
// the C# keyword form, WriteLine(string), happens to render correctly.
// Cause: CrefShortName takes the last '.' before stripping '(': Documentation.cs:783 and
//        XmlDocumentationRenderer.cs:262.

using Nullean.Argh;
using Nullean.Argh.Builder;

namespace Bugs;

internal static class Bug47
{
	/// <summary>Writes with <see cref="System.Console.WriteLine(System.String)"/> to the console.</summary>
	[NoOptionsInjection]
	public static void Run() => Console.Out.WriteLine("bug47");

	public static void Register(IArghRootBuilder app) => app.Map("bug47", Run);
}
