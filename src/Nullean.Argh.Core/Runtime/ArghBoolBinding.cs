using System.ComponentModel;

namespace Nullean.Argh.Runtime;

/// <summary>
/// Binds <c>bool</c> and <c>bool?</c> values for generated code: command-line values through
/// <see cref="ArghBool.TryParseBoolish"/>, environment variables through <see cref="ArghBool.ParseFalsey"/>.
/// Called from generated code; not intended for app code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ArghBoolBinding
{
	/// <summary>
	/// The value a bool flag got on the command line: <c>--name</c> or <c>--name=value</c>, and for a
	/// <paramref name="negatable"/> (<c>bool?</c>) flag <c>--no-name</c>, which is <see langword="false"/> and wins over
	/// <c>--name</c>. A bare switch is stored with a <see langword="null"/> value. <paramref name="value"/> is
	/// <see langword="null"/> when the flag wasn't given.
	/// </summary>
	/// <returns>
	/// <see langword="false"/>, with the message in <paramref name="error"/>, when a value isn't a boolean or
	/// <c>--no-name</c> has a value (<c>--no-name=true</c>), which it doesn't take, like git.
	/// </returns>
	public static bool TryGetFlag(IReadOnlyDictionary<string, string?> flags, string name, bool negatable, out bool? value, out string? error)
	{
		value = null;
		error = null;
		if (flags.TryGetValue(name, out var raw))
		{
			if (!TryParseFlagValue(name, raw, out var b, out error))
				return false;
			value = b;
		}

		if (negatable && flags.TryGetValue("no-" + name, out var negatedRaw))
		{
			if (negatedRaw is not null)
			{
				error = $"Error: --no-{name} doesn't take a value.";
				return false;
			}

			value = false;
		}

		return true;
	}

	/// <summary>
	/// The value of a bool's environment variable, or <see langword="null"/> when it's unset, or empty and
	/// <paramref name="treatEmptyAsUnset"/>. Any value other than a false spelling (or empty) is <see langword="true"/>.
	/// </summary>
	public static bool? FromEnvironment(string variable, bool treatEmptyAsUnset)
	{
		var raw = Environment.GetEnvironmentVariable(variable);
		if (raw is null || (raw.Length == 0 && treatEmptyAsUnset))
			return null;
		return ArghBool.ParseFalsey(raw);
	}

	/// <summary>
	/// <see cref="FromEnvironment"/> as <c>"true"</c> or <c>"false"</c>, for a value that's otherwise read as command-line
	/// text, such as a positional argument.
	/// </summary>
	public static string? EnvironmentText(string variable, bool treatEmptyAsUnset) =>
		FromEnvironment(variable, treatEmptyAsUnset) switch
		{
			true => "true",
			false => "false",
			null => null
		};

	/// <summary>
	/// The error for a command-line value that isn't a boolean; <paramref name="argName"/> is how the error names the
	/// argument, such as <c>--force</c> or <c>&lt;first&gt;</c>.
	/// </summary>
	public static string InvalidValueError(string argName, string? raw) =>
		$"Error: invalid bool for {argName}: '{raw}'. Expected true/false, yes/no, y/n, t/f, on/off or 1/0.";

	private static bool TryParseFlagValue(string name, string? raw, out bool value, out string? error)
	{
		error = null;
		// A bare switch has no value of its own.
		if (raw is null)
		{
			value = true;
			return true;
		}

		if (ArghBool.TryParseBoolish(raw, out value))
			return true;

		error = InvalidValueError("--" + name, raw);
		return false;
	}
}
