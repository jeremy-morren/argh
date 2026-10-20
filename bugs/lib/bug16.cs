// Bug 16 (library side) - DTO with a `required` member, bound by bugs/bug16.cs from another assembly.

#if BUG16
namespace Bugs.Lib;

/// <summary>External DTO with a required member.</summary>
public sealed class Bug16Req
{
	/// <summary>Required name.</summary>
	public required string Name { get; init; }

	/// <summary>Count with a default.</summary>
	public int Count { get; init; } = 4;
}
#endif
