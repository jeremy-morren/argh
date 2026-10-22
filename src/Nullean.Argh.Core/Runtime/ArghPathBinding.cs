using System.ComponentModel;

namespace Nullean.Argh.Runtime;

/// <summary>
/// Binds <see cref="FileInfo"/>, <see cref="DirectoryInfo"/> and <see cref="Uri"/> values for generated code. An empty
/// value is an error; anything else goes to the type's constructor, and what it rejects is an error with its message.
/// Called from generated code; not intended for app code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ArghPathBinding
{
	/// <summary>
	/// A <see cref="FileInfo"/> for <paramref name="raw"/>, after expanding <c>~</c> when <paramref name="expandUserProfile"/>.
	/// <paramref name="argName"/> is how the error names the argument, such as <c>--file</c> or <c>&lt;file&gt;</c>.
	/// </summary>
	public static bool TryFile(string argName, string raw, bool expandUserProfile, out FileInfo? value, out string? error)
	{
		value = null;
		if (!TryPath(argName, raw, expandUserProfile, static path => new FileInfo(path), out var info, out error))
			return false;
		value = (FileInfo)info!;
		return true;
	}

	/// <summary>A <see cref="DirectoryInfo"/> for <paramref name="raw"/>; see <see cref="TryFile"/>.</summary>
	public static bool TryDirectory(string argName, string raw, bool expandUserProfile, out DirectoryInfo? value, out string? error)
	{
		value = null;
		if (!TryPath(argName, raw, expandUserProfile, static path => new DirectoryInfo(path), out var info, out error))
			return false;
		value = (DirectoryInfo)info!;
		return true;
	}

	/// <summary>
	/// A <see cref="Uri"/> of <paramref name="kind"/> for <paramref name="raw"/>. An empty value is an error even for
	/// <see cref="UriKind.Relative"/>, which <see cref="Uri"/> itself accepts.
	/// </summary>
	public static bool TryUri(string argName, string raw, UriKind kind, out Uri? value, out string? error)
	{
		error = null;
		value = null;
		if (raw.Length == 0)
		{
			error = InvalidValueError("URI", argName, raw, "The URI is empty.");
			return false;
		}

		if (Uri.TryCreate(raw, kind, out value))
			return true;

		string reason;
		if (kind != UriKind.RelativeOrAbsolute && Uri.TryCreate(raw, UriKind.RelativeOrAbsolute, out var other))
			reason = other.IsAbsoluteUri ? "Expected a relative URI." : "Expected an absolute URI.";
		else
		{
			try
			{
				_ = new Uri(raw, kind);
				reason = "The format of the URI could not be determined.";
			}
			catch (UriFormatException e)
			{
				reason = e.Message;
			}
		}

		error = InvalidValueError("URI", argName, raw, reason);
		return false;
	}

	private static bool TryPath(string argName, string raw, bool expandUserProfile, Func<string, FileSystemInfo> create,
		out FileSystemInfo? value, out string? error)
	{
		value = null;
		error = null;
		// Only empty is ours to reject: whitespace can name a file, so the constructor decides.
		if (raw.Length == 0)
		{
			error = InvalidValueError("path", argName, raw, "The path is empty.");
			return false;
		}

		try
		{
			value = create(expandUserProfile ? ArghPath.ExpandUserProfilePath(raw) : raw);
			return true;
		}
		// Which paths throw differs by runtime: .NET Framework rejects characters such as '|' that .NET Core allows.
		catch (Exception e)
		{
			error = InvalidValueError("path", argName, raw, ExceptionReason(e));
			return false;
		}
	}

	private static string InvalidValueError(string what, string argName, string raw, string reason) =>
		$"Error: invalid {what} for {argName}: '{raw}'. {reason}";

	/// <summary>
	/// Whether <paramref name="value"/> is a URL as <c>[Url]</c>, the DataAnnotations <c>UrlAttribute</c>, means it: an
	/// absolute URI whose scheme is <c>http</c>, <c>https</c> or <c>ftp</c>.
	/// </summary>
	public static bool IsUrl(Uri value) => value.IsAbsoluteUri && value.Scheme is "http" or "https" or "ftp";

	/// <summary>Whether <paramref name="value"/> is an absolute URI that <see cref="IsUrl(Uri)"/> accepts.</summary>
	public static bool IsUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsUrl(uri);

	/// <summary>The exception's message without the parameter name an <see cref="ArgumentException"/> appends to it.</summary>
	private static string ExceptionReason(Exception e)
	{
		var message = e.Message;
		if (e is ArgumentException { ParamName: { Length: > 0 } name })
		{
			foreach (var suffix in new[] { $" (Parameter '{name}')", Environment.NewLine + "Parameter name: " + name })
			{
				if (message.EndsWith(suffix, StringComparison.Ordinal))
					return message.Substring(0, message.Length - suffix.Length);
			}
		}

		return message;
	}
}
