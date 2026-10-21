// Bug 80 - A base type's validator is skipped when the derived type has one
// Group C: runtime, dispatch and hosting
//
// Both validators below are registered: the base one rejects --level < 0, the derived one rejects an empty --name.
// Run:      Bugs.exe bug80 --level -1 --name x
// Actual:   level:-1 name:x      exit 0        (only the derived validator ran)
// Expected: Error: --level: must not be negative   exit 2
//
// Registering only the base validator does cover the derived type, so the lookup walks the hierarchy but stops
// at the first type that has a validator.
// Cause: ArghValidation.cs:60-73 returns after the first match.

using Nullean.Argh;
using Nullean.Argh.Builder;
using Nullean.Argh.Validation;

namespace Bugs;

/// <summary>Base options.</summary>
public class Bug80Base
{
	/// <summary>A level; must not be negative.</summary>
	public int Level { get; set; }
}

/// <summary>Derived options.</summary>
public sealed class Bug80Derived : Bug80Base
{
	/// <summary>A name; must not be empty.</summary>
	public string Name { get; set; } = "";
}

internal static class Bug80
{
	/// <summary>Binds the derived DTO.</summary>
	[NoOptionsInjection]
	public static void Run([AsParameters] Bug80Derived o) => Console.Out.WriteLine($"level:{o.Level} name:{o.Name}");

	public static void Register(IArghRootBuilder app)
	{
		ArghValidation.Register<Bug80Base>(o =>
			o.Level >= 0 ? [] : [new ArghValidationError("must not be negative", nameof(Bug80Base.Level))]);
		ArghValidation.Register<Bug80Derived>(o =>
			o.Name.Length > 0 ? [] : [new ArghValidationError("must not be empty", nameof(Bug80Derived.Name))]);
		app.Map("bug80", Run);
	}
}
