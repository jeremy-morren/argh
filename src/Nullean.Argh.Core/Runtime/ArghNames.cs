using System.ComponentModel;
using Nullean.Argh.Matching;
using Nullean.Argh.Parsing;

namespace Nullean.Argh.Runtime;

/// <summary>
/// Names looked up with a comparer from <see cref="ParserOptions"/>: the commands and namespaces of one scope
/// (<see cref="ArghCommandSet"/>) or the flags of one command (<see cref="ArghFlagSet"/>). Used by generated code; not
/// intended for app code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class ArghNameScope
{
	private protected ArghNameScope() { }

	/// <summary>Builds the lookup for <paramref name="options"/>; throws when two names collide under its comparer.</summary>
	internal abstract void Check(ParserOptions options);
}

/// <summary>
/// The command and namespace names of one scope (the app, or one namespace), matched with
/// <see cref="ParserOptions.CommandStringComparer"/>. Used by generated code; not intended for app code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ArghCommandSet : ArghNameScope
{
	/// <summary>How far a mistyped name can be from a name to be suggested, in edits.</summary>
	private const int SuggestDistance = 2;

	private readonly NameMap<string> _names;

	/// <summary>The scope's commands and namespaces; a name given twice is kept once.</summary>
	public ArghCommandSet(params string[] names)
	{
		if (names is null)
			throw new ArgumentNullException(nameof(names));
		var distinct = new List<string>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var name in names)
		{
			if (!string.IsNullOrEmpty(name) && seen.Add(name))
				distinct.Add(name);
		}

		Names = distinct;
		_names = new NameMap<string>(Build);
	}

	/// <summary>The names, as registered.</summary>
	public IReadOnlyList<string> Names { get; }

	/// <summary>The registered name <paramref name="token"/> matches, or <see langword="null"/>.</summary>
	public string? Find(string token) =>
		_names.For(ArghNames.CommandComparer).TryGetValue(token, out var name) ? name : null;

	/// <summary>The names closest to <paramref name="token"/>, for "did you mean", or none.</summary>
	public IReadOnlyList<string> Suggest(string token) =>
		FuzzyMatch.FindClosest(token, Names, SuggestDistance, ArghNames.CommandComparer);

	internal override void Check(ParserOptions options) => _names.For(options.CommandStringComparer);

	private Dictionary<string, string> Build(StringComparer comparer)
	{
		var map = new Dictionary<string, string>(comparer);
		foreach (var name in Names)
		{
			if (map.TryGetValue(name, out var first))
			{
				throw new InvalidOperationException(
					$"ParserOptions.CommandStringComparer treats the commands or namespaces '{first}' and '{name}' as the same name. " +
					"Rename one, or use a comparer that tells them apart, such as StringComparer.Ordinal.");
			}

			map.Add(name, name);
		}

		return map;
	}
}

/// <summary>Matches command and option names with the configured comparers. Used by generated code; not intended for app code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ArghNames
{
	/// <summary>The current <see cref="ParserOptions.CommandStringComparer"/>.</summary>
	public static StringComparer CommandComparer => ArghRuntime.ParserOptions.CommandStringComparer;

	/// <summary>The current <see cref="ParserOptions.ArgumentStringComparer"/>.</summary>
	public static StringComparer ArgumentComparer => ArghRuntime.ParserOptions.ArgumentStringComparer;

	/// <summary>Adds <paramref name="scope"/> to <paramref name="scopes"/>, for <see cref="Check"/>, and returns it.</summary>
	public static T Register<T>(List<ArghNameScope> scopes, T scope) where T : ArghNameScope
	{
		scopes.Add(scope);
		return scope;
	}

	/// <summary>
	/// Fails fast when the configured comparers make two commands, namespaces or flags in one scope the same: throws an
	/// <see cref="InvalidOperationException"/> that names them.
	/// </summary>
	public static void Check(IReadOnlyList<ArghNameScope> scopes)
	{
		var options = ArghRuntime.ParserOptions;
		foreach (var scope in scopes)
			scope.Check(options);
	}

	/// <summary><c>-h</c>, or <c>--help</c> under <see cref="ArgumentComparer"/>.</summary>
	public static bool IsHelp(string arg) => arg == "-h" || IsFlag(arg, "--help");

	/// <summary><c>--version</c> under <see cref="ArgumentComparer"/>.</summary>
	public static bool IsVersion(string arg) => IsFlag(arg, "--version");

	/// <summary>
	/// Whether <paramref name="arg"/> is the option <paramref name="spelling"/>: a long one (<c>--name</c>) under
	/// <see cref="ArgumentComparer"/>, a short one (<c>-n</c>) exactly, since short options always match case.
	/// </summary>
	public static bool IsFlag(string arg, string spelling) =>
		IsShortSpelling(spelling) ? arg == spelling : ArgumentComparer.Equals(arg, spelling);

	/// <summary>Whether <paramref name="spelling"/> is a short option, such as <c>-v</c>.</summary>
	internal static bool IsShortSpelling(string spelling) => spelling.Length == 2 && spelling[0] == '-' && spelling[1] != '-';

	/// <summary>Whether <paramref name="candidate"/> starts with <paramref name="prefix"/> under <paramref name="comparer"/>.</summary>
	internal static bool StartsWith(string candidate, string prefix, StringComparer comparer) =>
		candidate.Length >= prefix.Length && comparer.Equals(candidate.Substring(0, prefix.Length), prefix);
}

/// <summary>A name lookup built for a comparer the first time it's used with it, and rebuilt when the comparer changes.</summary>
internal sealed class NameMap<T>(Func<StringComparer, Dictionary<string, T>> build)
{
	private Built? _built;

	public Dictionary<string, T> For(StringComparer comparer)
	{
		var built = _built;
		if (built is null || !ReferenceEquals(built.Comparer, comparer))
			_built = built = new Built(comparer, build(comparer));
		return built.Map;
	}

	private sealed class Built(StringComparer comparer, Dictionary<string, T> map)
	{
		public StringComparer Comparer { get; } = comparer;

		public Dictionary<string, T> Map { get; } = map;
	}
}
