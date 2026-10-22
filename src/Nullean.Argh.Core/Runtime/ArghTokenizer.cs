using System.ComponentModel;

namespace Nullean.Argh.Runtime;

/// <summary>What <see cref="ArghTokenizer"/> found wrong with the command line. Used by generated code; not intended for app code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum ArghTokenErrorKind
{
	/// <summary>A long option no flag has; <see cref="ArghTokenError.Name"/> is its name without <c>--</c>.</summary>
	UnknownOption,

	/// <summary>A short option no flag has.</summary>
	UnknownShortOption,

	/// <summary>A flag that takes a value has none: it's last, or the next argument is another option.</summary>
	MissingValue,

	/// <summary>A flag that takes one value was given more than once (see <see cref="RepeatedScalarBehaviour"/>).</summary>
	Repeated,

	/// <summary>A value on a <c>--no-</c> flag, which takes none.</summary>
	UnexpectedValue,
}

/// <summary>A command-line error <see cref="ArghTokenizer"/> found. Used by generated code; not intended for app code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ArghTokenError
{
	internal ArghTokenError(ArghTokenErrorKind kind, string message, string name)
	{
		Kind = kind;
		Message = message;
		Name = name;
	}

	/// <summary>What's wrong.</summary>
	public ArghTokenErrorKind Kind { get; }

	/// <summary>The error line, starting with <c>Error: </c>.</summary>
	public string Message { get; }

	/// <summary>
	/// For <see cref="ArghTokenErrorKind.UnknownOption"/> the option's name as given, without <c>--</c>; for
	/// <see cref="ArghTokenErrorKind.UnknownShortOption"/> the option as given; otherwise the long name of the flag.
	/// </summary>
	public string Name { get; }

	/// <summary>The long name of the flag the error is about, for its help row, or <see langword="null"/> for an unknown option.</summary>
	public string? FlagName => Kind is ArghTokenErrorKind.UnknownOption or ArghTokenErrorKind.UnknownShortOption ? null : Name;
}

/// <summary>The flags, values and positional arguments <see cref="ArghTokenizer"/> read. Used by generated code; not intended for app code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ArghTokens
{
	/// <summary>
	/// The value of each flag that takes one, and each <c>bool</c> flag, by its long name: <see langword="null"/> for a
	/// switch given without a value, and <c>"false"</c> for a <c>--no-</c> flag.
	/// </summary>
	public Dictionary<string, string?> Flags { get; } = new(StringComparer.Ordinal);

	/// <summary>Every value given for each collection flag, by its long name.</summary>
	public Dictionary<string, List<string>> MultiFlags { get; } = new(StringComparer.Ordinal);

	/// <summary>The positional arguments, including every argument after <c>--</c>.</summary>
	public List<string> Positionals { get; } = [];

	/// <summary>The index of the first argument not read: where reading stopped, or the argument that has the <see cref="Error"/>.</summary>
	public int Next { get; internal set; }

	/// <summary><c>--help</c> or <c>-h</c> was given, before any error.</summary>
	public bool HelpRequested { get; internal set; }

	/// <summary>The first error, or <see langword="null"/>.</summary>
	public ArghTokenError? Error { get; internal set; }
}

/// <summary>
/// Reads the options and arguments on a command line the way clap does, for generated code. Not intended for app code.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>--name value</c>, <c>--name=value</c>, <c>-n value</c>, <c>-nvalue</c> and <c>-n=value</c> give a value; short
/// flags combine, so <c>-vn value</c> and <c>-vnvalue</c> are <c>-v -n value</c>.</item>
/// <item>The argument after a flag is its value unless it's an option itself, known or not (<c>--name --help</c> is
/// missing a value, as in clap); a negative number such as <c>-5</c>, <c>-1.5e3</c> or <c>-0,5</c> is a value (or a
/// positional argument) unless a short flag has its digit, as with clap's <c>allow_negative_numbers</c>.
/// <c>--name=--help</c> gives any value.</item>
/// <item>A flag that takes one value, or a <c>bool</c> flag, given more than once follows the <see cref="RepeatedScalarBehaviour"/>;
/// a collection flag keeps each value.</item>
/// <item>Every argument after <c>--</c> is positional.</item>
/// </list>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ArghTokenizer
{
	/// <summary>
	/// Reads a command's arguments: its options, wherever they are, and its positional arguments. Stops at
	/// <c>--help</c> or <c>-h</c> when <paramref name="help"/>, and at the first error.
	/// </summary>
	/// <param name="args">The arguments after the command.</param>
	/// <param name="flags">The flags the command reads.</param>
	/// <param name="given">Flags the options parsers already read before the command, to check repeats against.</param>
	/// <param name="lenient">Skip unknown options (and the value after one), and don't stop at them.</param>
	/// <param name="help">Whether <c>--help</c> and <c>-h</c> ask for help, rather than being unknown options.</param>
	public static ArghTokens ReadCommand(string[] args, ArghFlagSet flags, IReadOnlyDictionary<string, string?>? given = null,
		bool lenient = false, bool help = true)
	{
		var reader = new Reader(args, args.Length, flags, deferTo: null, given, record: null, lenient ? Mode.Lenient : Mode.Command, help);
		reader.ReadCommand();
		return reader.Tokens;
	}

	/// <summary>
	/// Reads the options at <c>args[start]</c> onwards, up to the first argument that isn't one of them (a command, a
	/// positional, <c>--</c>, <c>-</c>, <c>--help</c>, <c>-h</c> or <c>--version</c>), or to <paramref name="end"/>.
	/// <see cref="ArghTokens.Next"/> is where it stopped.
	/// </summary>
	/// <param name="args">The command line.</param>
	/// <param name="start">Where the options start.</param>
	/// <param name="flags">The flags of these options.</param>
	/// <param name="deferTo">
	/// Flags of a command that reads the rest of the line (the default command): an option of theirs stops reading
	/// rather than being unknown.
	/// </param>
	/// <param name="given">The flags read before, to check repeats against; each flag read here is added.</param>
	/// <param name="end">Where to stop, or -1 for the end of <paramref name="args"/>.</param>
	/// <param name="complete">For shell completion: a flag given more than once is read like any other.</param>
	public static ArghTokens ReadOptions(string[] args, int start, ArghFlagSet flags, ArghFlagSet? deferTo = null,
		Dictionary<string, string?>? given = null, int end = -1, bool complete = false)
	{
		var reader = new Reader(args, end < 0 || end > args.Length ? args.Length : end, flags, deferTo, given, given,
			complete ? Mode.Complete : Mode.Options, help: true);
		reader.ReadOptions(start);
		return reader.Tokens;
	}

	/// <summary>
	/// Whether <paramref name="token"/> starts like a negative number: <c>-</c> and a digit, or <c>-</c>, a decimal
	/// separator and a digit. That covers <c>-5</c>, <c>-1.5e3</c>, <c>-.5</c>, and <c>-0,5</c> in a culture that writes a
	/// decimal comma; whether it is a number is for the value's parser to say.
	/// </summary>
	public static bool IsNegativeNumber(string token)
	{
		if (token.Length < 2 || token[0] != '-')
			return false;
		var first = token[1];
		if (first is >= '0' and <= '9')
			return true;
		return first is '.' or ',' && token.Length > 2 && token[2] is >= '0' and <= '9';
	}

	private enum Mode
	{
		/// <summary>A command's arguments.</summary>
		Command,

		/// <summary>A command's arguments, skipping unknown options.</summary>
		Lenient,

		/// <summary>The options before a command.</summary>
		Options,

		/// <summary>The options before a command, for shell completion: repeats aren't checked.</summary>
		Complete,
	}

	private enum Step
	{
		/// <summary>Read on.</summary>
		Continue,

		/// <summary>Stop before this argument: it's for the command that reads the rest.</summary>
		Stop,

		/// <summary>Stop: an error, or help.</summary>
		Fail,
	}

	private sealed class Reader(
		string[] args,
		int end,
		ArghFlagSet flags,
		ArghFlagSet? deferTo,
		IReadOnlyDictionary<string, string?>? given,
		Dictionary<string, string?>? record,
		Mode mode,
		bool help)
	{
		public ArghTokens Tokens { get; } = new();

		private bool IsOptions => mode is Mode.Options or Mode.Complete;

		public void ReadCommand()
		{
			var i = 0;
			while (i < end)
			{
				var a = args[i];
				if (a == "--")
				{
					for (i++; i < end; i++)
						Tokens.Positionals.Add(args[i]);
					break;
				}

				if (help && a is "--help" or "-h")
				{
					Tokens.HelpRequested = true;
					Tokens.Next = i;
					return;
				}

				var step = IsLong(a) ? ReadLong(ref i) : IsShort(a) ? ReadShort(ref i) : Positional(a, ref i);
				if (step != Step.Continue)
					return;
			}

			Tokens.Next = end;
		}

		public void ReadOptions(int start)
		{
			var i = start;
			while (i < end)
			{
				var a = args[i];
				if (a is "--help" or "-h" or "--version" or "--" or "-")
					break;
				Step step;
				if (IsLong(a))
					step = ReadLong(ref i);
				else if (IsShort(a))
					step = ReadShort(ref i);
				else
					break;
				if (step == Step.Fail)
					return;
				if (step == Step.Stop)
					break;
			}

			Tokens.Next = i;
		}

		private Step Positional(string a, ref int i)
		{
			Tokens.Positionals.Add(a);
			i++;
			return Step.Continue;
		}

		private static bool IsLong(string a) => a.Length > 2 && a[0] == '-' && a[1] == '-';

		/// <summary>Short options, unless it's a negative number no short flag starts.</summary>
		private bool IsShort(string a) => a.Length > 1 && a[0] == '-' && a[1] != '-' && !(IsNegativeNumber(a) && !HasShort(a[1]));

		private bool HasShort(char c) => flags.HasShort(c) || (deferTo?.HasShort(c) ?? false);

		private Step ReadLong(ref int i)
		{
			var a = args[i];
			var eq = a.IndexOf('=', 2);
			var name = eq < 0 ? a.Substring(2) : a.Substring(2, eq - 2);
			var value = eq < 0 ? null : a.Substring(eq + 1);
			if (!flags.TryGetLong(name, out var flag, out var negated))
			{
				if (IsOptions && deferTo is not null && deferTo.TryGetLong(name, out _, out _))
					return Step.Stop;
				if (mode == Mode.Lenient)
				{
					i++;
					if (value is null && i < end && !args[i].StartsWith("-", StringComparison.Ordinal))
						i++;
					return Step.Continue;
				}

				return Fail(i, ArghTokenErrorKind.UnknownOption, $"Error: unknown option '--{name}'.", name);
			}

			if (!flag.TakesValue)
			{
				if (negated)
				{
					if (value is not null)
						return Fail(i, ArghTokenErrorKind.UnexpectedValue, $"Error: --{name} doesn't take a value.", flag.Name);
					value = "false";
				}

				return Set(i++, flag, value);
			}

			if (value is not null)
				return Set(i++, flag, value);
			return ReadNextValue(ref i, flag, []);
		}

		private Step ReadShort(ref int i)
		{
			var a = args[i];
			var pending = new List<(ArghFlag Flag, string? Value)>();
			for (var j = 1; j < a.Length; j++)
			{
				var c = a[j];
				if (help && c == 'h')
				{
					// Help in the options before a command is for the command to show.
					if (IsOptions)
						return Step.Stop;
					Tokens.HelpRequested = true;
					Tokens.Next = i;
					return Step.Fail;
				}

				if (!flags.TryGetShort(c, out var flag))
				{
					if (IsOptions && deferTo is not null && deferTo.HasShort(c))
						return Step.Stop;
					if (mode == Mode.Lenient)
					{
						i++;
						if (a.Length == 2 && i < end && !args[i].StartsWith("-", StringComparison.Ordinal))
							i++;
						return Commit(i, pending);
					}

					return Fail(i, ArghTokenErrorKind.UnknownShortOption, $"Error: unknown short option '-{c}'.", "-" + c);
				}

				var rest = j + 1 < a.Length ? a.Substring(j + 1) : null;
				if (!flag.TakesValue)
				{
					// -v=false gives the switch a value; -vn is two flags.
					if (rest is not null && rest[0] == '=')
					{
						pending.Add((flag, rest.Substring(1)));
						break;
					}

					pending.Add((flag, null));
					continue;
				}

				// -nvalue or -n=value; or -n value
				if (rest is not null)
				{
					pending.Add((flag, rest[0] == '=' ? rest.Substring(1) : rest));
					break;
				}

				return ReadNextValue(ref i, flag, pending);
			}

			var result = Commit(i, pending);
			if (result == Step.Continue)
				i++;
			return result;
		}

		/// <summary>
		/// The value of <paramref name="flag"/>, given at <c>args[i]</c>, is the next argument, unless that's an option:
		/// a known one means the value is missing; an unknown one is read next and reported as unknown, as clap does.
		/// <paramref name="pending"/> are the flags before it in the same argument.
		/// </summary>
		private Step ReadNextValue(ref int i, ArghFlag flag, List<(ArghFlag, string?)> pending)
		{
			var next = i + 1;
			if (next < end && IsValue(args[next]))
			{
				pending.Add((flag, args[next]));
				var step = Commit(i, pending);
				if (step == Step.Continue)
					i = next + 1;
				return step;
			}

			if (next < end && mode != Mode.Lenient && IsUnknownOption(args[next]))
			{
				var step = Commit(i, pending);
				if (step == Step.Continue)
					i = next;
				return step;
			}

			return Fail(i, ArghTokenErrorKind.MissingValue, $"Error: a value is required for --{flag.Name} but none was supplied.", flag.Name);
		}

		/// <summary>The argument after a flag that takes a value is that value unless it's an option.</summary>
		private bool IsValue(string a)
		{
			if (a.Length < 2 || a[0] != '-')
				return true;
			if (a[1] == '-')
				return false;
			return IsNegativeNumber(a) && !HasShort(a[1]);
		}

		/// <summary>An option (as <see cref="IsValue"/> isn't) that no flag has.</summary>
		private bool IsUnknownOption(string a)
		{
			if (a == "--")
				return false;
			if (a[1] == '-')
			{
				var eq = a.IndexOf('=', 2);
				var name = eq < 0 ? a.Substring(2) : a.Substring(2, eq - 2);
				if (help && name is "help" or "version")
					return false;
				return !flags.TryGetLong(name, out _, out _) && !(deferTo?.TryGetLong(name, out _, out _) ?? false);
			}

			return !(help && a[1] == 'h') && !HasShort(a[1]);
		}

		private Step Commit(int i, List<(ArghFlag Flag, string? Value)> pending)
		{
			foreach (var (flag, value) in pending)
			{
				if (Set(i, flag, value) == Step.Fail)
					return Step.Fail;
			}

			return Step.Continue;
		}

		/// <summary>Stores a flag's value; a flag that takes one value, or a switch, given before follows the repeat rule.</summary>
		private Step Set(int i, ArghFlag flag, string? value)
		{
			if (flag.Kind == ArghFlagKind.Collection)
			{
				if (!Tokens.MultiFlags.TryGetValue(flag.Name, out var list))
					Tokens.MultiFlags[flag.Name] = list = [];
				list.Add(value ?? "");
				return Step.Continue;
			}

			if (Tokens.Flags.TryGetValue(flag.Name, out var previous) || (given?.TryGetValue(flag.Name, out previous) ?? false))
			{
				var behaviour = mode == Mode.Complete
					? RepeatedScalarBehaviour.TakeLast
					: flag.Repeated ?? ArghRuntime.ParserOptions.RepeatedScalarBehaviour;
				if (behaviour == RepeatedScalarBehaviour.Reject)
					return Fail(i, ArghTokenErrorKind.Repeated, $"Error: the argument --{flag.Name} cannot be used multiple times.", flag.Name);
				if (behaviour == RepeatedScalarBehaviour.AllowIdentical && !Same(flag, previous, value))
				{
					return Fail(i, ArghTokenErrorKind.Repeated,
						$"Error: the argument --{flag.Name} cannot be used multiple times with different values: '{Shown(flag, previous)}' and '{Shown(flag, value)}'.",
						flag.Name);
				}
			}

			Tokens.Flags[flag.Name] = value;
			if (record is not null)
				record[flag.Name] = value;
			return Step.Continue;
		}

		/// <summary>Two values of a flag are the same text, or for a switch the same boolean (a bare switch is <see langword="true"/>).</summary>
		private static bool Same(ArghFlag flag, string? a, string? b)
		{
			if (flag.IsSwitch && ArghBool.TryParseBoolish(a ?? "true", out var x) && ArghBool.TryParseBoolish(b ?? "true", out var y))
				return x == y;
			return string.Equals(a, b, StringComparison.Ordinal);
		}

		private static string Shown(ArghFlag flag, string? value) => value ?? (flag.IsSwitch ? "true" : "");

		private Step Fail(int i, ArghTokenErrorKind kind, string message, string name)
		{
			Tokens.Error = new ArghTokenError(kind, message, name);
			Tokens.Next = i;
			return Step.Fail;
		}
	}
}
