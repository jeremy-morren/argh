using System.ComponentModel;

namespace Nullean.Argh.Runtime;

/// <summary>How a flag takes its value. Used by generated code; not intended for app code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum ArghFlagKind
{
	/// <summary>A <c>bool</c>: <c>--name</c> or <c>-n</c>, or <c>--name=value</c> or <c>-n=value</c>.</summary>
	Switch,

	/// <summary>A <c>bool?</c>: a <see cref="Switch"/> that also has <c>--no-name</c>, which is <see langword="false"/> and takes no value.</summary>
	NegatableSwitch,

	/// <summary>
	/// One value: <c>--name value</c>, <c>--name=value</c>, <c>-n value</c>, <c>-nvalue</c> or <c>-n=value</c>.
	/// Given more than once, the <see cref="RepeatedScalarBehaviour"/> applies.
	/// </summary>
	Value,

	/// <summary>A collection: one value each time it's given, as many times as it's given.</summary>
	Collection,
}

/// <summary>A flag a command or options type declares. Used by generated code; not intended for app code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ArghFlag
{
	/// <summary>A flag named <paramref name="name"/> (without <c>--</c>), with its other long names and its short name, if any.</summary>
	/// <param name="name">The long name, without <c>--</c>: the key values are stored under.</param>
	/// <param name="kind">How the flag takes its value.</param>
	/// <param name="shortName">The short name, or <c>'\0'</c> for none.</param>
	/// <param name="aliases">Other long names, without <c>--</c>.</param>
	/// <param name="repeated">What happens when it's given more than once, over <see cref="Parsing.ParserOptions.RepeatedScalarBehaviour"/>.</param>
	public ArghFlag(string name, ArghFlagKind kind, char shortName = '\0', string[]? aliases = null, RepeatedScalarBehaviour? repeated = null)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		Kind = kind;
		ShortName = shortName;
		Aliases = aliases ?? [];
		Repeated = repeated;
	}

	/// <summary>The long name, without <c>--</c>.</summary>
	public string Name { get; }

	/// <summary>How the flag takes its value.</summary>
	public ArghFlagKind Kind { get; }

	/// <summary>The short name, or <c>'\0'</c> for none.</summary>
	public char ShortName { get; }

	/// <summary>Other long names, without <c>--</c>.</summary>
	public IReadOnlyList<string> Aliases { get; }

	/// <summary>What happens when the flag is given more than once, or <see langword="null"/> for <see cref="Parsing.ParserOptions.RepeatedScalarBehaviour"/>.</summary>
	public RepeatedScalarBehaviour? Repeated { get; }

	internal bool TakesValue => Kind is ArghFlagKind.Value or ArghFlagKind.Collection;

	internal bool IsSwitch => Kind is ArghFlagKind.Switch or ArghFlagKind.NegatableSwitch;
}

/// <summary>
/// The flags one command, or one options type, reads: looked up by long name (with
/// <see cref="Parsing.ParserOptions.ArgumentStringComparer"/>, and with <c>--no-</c> forms for a
/// <see cref="ArghFlagKind.NegatableSwitch"/>) and by short name, which always matches case. Generated code builds one per
/// command and options parser and passes it to <see cref="ArghTokenizer"/>; not intended for app code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ArghFlagSet : ArghNameScope
{
	private readonly NameMap<LongName> _long;
	private readonly Dictionary<char, ArghFlag> _short = new();

	/// <summary>
	/// The set of <paramref name="flags"/>; when two use the same name, the first keeps it. Two flags whose names differ
	/// but are the same under the comparer make the lookup throw an <see cref="InvalidOperationException"/>.
	/// </summary>
	public ArghFlagSet(params ArghFlag[] flags)
	{
		Flags = flags ?? throw new ArgumentNullException(nameof(flags));
		foreach (var flag in flags)
		{
			if (flag.ShortName != '\0' && !_short.ContainsKey(flag.ShortName))
				_short.Add(flag.ShortName, flag);
		}

		_long = new NameMap<LongName>(BuildLong);
	}

	/// <summary>The flags, in the order given.</summary>
	public IReadOnlyList<ArghFlag> Flags { get; }

	/// <summary>The flag with the long name <paramref name="name"/> (without <c>--</c>); <paramref name="negated"/> for a <c>--no-</c> form.</summary>
	internal bool TryGetLong(string name, out ArghFlag flag, out bool negated)
	{
		if (_long.For(ArghNames.ArgumentComparer).TryGetValue(name, out var found))
		{
			flag = found.Flag;
			negated = found.Negated;
			return true;
		}

		flag = null!;
		negated = false;
		return false;
	}

	/// <summary>The flag with the short name <paramref name="c"/>.</summary>
	internal bool TryGetShort(char c, out ArghFlag flag) => _short.TryGetValue(c, out flag!);

	internal bool HasShort(char c) => _short.ContainsKey(c);

	internal override void Check(Parsing.ParserOptions options) => _long.For(options.ArgumentStringComparer);

	private Dictionary<string, LongName> BuildLong(StringComparer comparer)
	{
		var map = new Dictionary<string, LongName>(comparer);
		foreach (var flag in Flags)
		{
			AddLong(map, flag.Name, flag, negated: false);
			foreach (var alias in flag.Aliases)
				AddLong(map, alias, flag, negated: false);
		}

		// The --no- forms last, so a flag that is itself named no-x keeps its name.
		foreach (var flag in Flags)
		{
			if (flag.Kind != ArghFlagKind.NegatableSwitch)
				continue;
			AddLong(map, "no-" + flag.Name, flag, negated: true);
			foreach (var alias in flag.Aliases)
				AddLong(map, "no-" + alias, flag, negated: true);
		}

		return map;
	}

	private static void AddLong(Dictionary<string, LongName> map, string name, ArghFlag flag, bool negated)
	{
		if (string.IsNullOrEmpty(name))
			return;
		if (!map.TryGetValue(name, out var first))
		{
			map.Add(name, new LongName(flag, negated, name));
			return;
		}

		// The same name given twice, another spelling of the same flag, or a --no- form: the first keeps it.
		if (negated || first.Negated || ReferenceEquals(first.Flag, flag) || string.Equals(first.Name, name, StringComparison.Ordinal))
			return;
		throw new InvalidOperationException(
			$"ParserOptions.ArgumentStringComparer treats the options --{first.Name} and --{name} as the same name, but they are different flags. " +
			"Rename one, or use a comparer that tells them apart, such as StringComparer.Ordinal.");
	}

	private readonly record struct LongName(ArghFlag Flag, bool Negated, string Name);
}
