// Bug 56 (library side) - documented handler class in a referenced assembly; registered by bugs/bug56.cs.

using Nullean.Argh;

namespace Bugs.Lib;

/// <summary>Commands documented in another assembly.</summary>
public sealed class Bug56Commands
{
	/// <summary>Greets someone from the library.</summary>
	/// <param name="name">-n, Who to greet.</param>
	[NoOptionsInjection]
	public static void Bug56(string name) => Console.Out.WriteLine($"hello {name}");
}
