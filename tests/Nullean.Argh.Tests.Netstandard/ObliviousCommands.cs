#nullable disable
using System;
using System.IO;

namespace Nullean.Argh.Tests.Netstandard;

// Written the way code reads in a project without nullable reference types: no `?` on reference types, and `= null`
// for an optional one. Both fixture builds compile this file oblivious, so they must treat it the same way.

/// <summary>Commands whose reference-type parameters have no nullable annotations.</summary>
public static class NsObliviousHandlers
{
	/// <summary>Reference-type parameters without annotations.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="name">Required, because it has no default value.</param>
	/// <param name="note">Optional, because it defaults to null.</param>
	/// <param name="link">An optional URI.</param>
	/// <param name="file">An optional file.</param>
	/// <param name="tags">Optional tags.</param>
	public static void Oblivious(NsGlobalOptions g, string name, string note = null, Uri link = null, FileInfo file = null, string[] tags = null) =>
		Console.Out.WriteLine(
			$"oblivious:{name}|{note ?? "none"}|{(link == null ? "none" : link.Host)}|{(file == null ? "none" : file.Name)}|{(tags == null ? "none" : string.Join(",", tags))}");

	/// <summary>An [AsParameters] DTO without annotations.</summary>
	/// <param name="g">Global options.</param>
	/// <param name="args">The options.</param>
	public static void ObliviousDto(NsGlobalOptions g, [AsParameters] NsObliviousArgs args) =>
		Console.Out.WriteLine($"oblivious-dto:{args.Name}|{args.Note ?? "none"}");
}

/// <summary>Options without nullable annotations.</summary>
public sealed class NsObliviousArgs
{
	/// <summary>Required, because it has no initializer.</summary>
	public string Name { get; set; }

	/// <summary>Optional, because it has an initializer.</summary>
	public string Note { get; set; } = "preset";
}
