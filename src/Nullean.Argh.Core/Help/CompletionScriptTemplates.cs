using System.ComponentModel;

namespace Nullean.Argh.Help;

/// <summary>
/// Shells for which static completion script templates are provided (<see cref="CompletionScriptTemplates"/>).
/// </summary>
public enum CompletionShell
{
	/// <summary>Bourne-again shell (<c>bash</c>).</summary>
	Bash = 0,

	/// <summary>Z shell (<c>zsh</c>).</summary>
	Zsh = 1,

	/// <summary>Friendly interactive shell (<c>fish</c>).</summary>
	Fish = 2,

	/// <summary>PowerShell (<c>pwsh</c> 7+ or Windows PowerShell 5.1); accepted as <c>powershell</c> or <c>pwsh</c>.</summary>
	PowerShell = 3,
}

/// <summary>
/// Shell completion script templates. Each template is a single string containing <c>{0}</c> placeholders for the application executable name. Prefer <c>template.Replace("{0}", appName)</c> (or escape literal braces as <c>{{</c>/<c>}}</c> if you use <see cref="string.Format(string, object?)"/>), because shell syntax includes braces that would confuse <c>string.Format</c>. The app prints these when invoked as <c>__completion bash|zsh|fish|powershell</c>; installed scripts call <c>__complete</c> for tab candidates (see <see cref="CompletionProtocol"/>).
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class CompletionScriptTemplates
{
	/// <summary>
	/// Bash completion script template. Replace <c>{0}</c> with the program name (e.g. <c>myapp</c>). The template assumes the CLI exposes a completion protocol such as <c>{0} __complete bash ...</c>.
	/// </summary>
	/// <returns>A format string with <c>{0}</c> for the executable name.</returns>
	public static string GetBash() =>
		"# Generated completion — replace {0} with your executable name.\n" +
		"# Passes all words after the executable (COMP_WORDS[1]..) so nested subcommands complete correctly.\n" +
		"_{0}_completion() {\n" +
		"  COMPREPLY=()\n" +
		"  mapfile -t COMPREPLY < <({0} __complete bash -- \"${COMP_WORDS[@]:1}\")\n" +
		"}\n" +
		"complete -o default -F _{0}_completion {0}\n";

	/// <summary>
	/// Zsh completion script template. Replace <c>{0}</c> with the program name. The template assumes <c>{0} __complete zsh ...</c>.
	/// </summary>
	/// <returns>A format string with <c>{0}</c> for the executable name.</returns>
	public static string GetZsh() =>
		"# Generated completion — replace {0} with your executable name.\n" +
		"# Passes argv after the command name (words[2]..) so nested subcommands complete correctly.\n" +
		"#compdef {0}\n" +
		"_{0}_completion() {\n" +
		"  local -a reply\n" +
		"  reply=($({0} __complete zsh -- \"${words[@]:2}\"))\n" +
		"  compadd -a reply\n" +
		"}\n" +
		"compdef _{0}_completion {0}\n";

	/// <summary>
	/// Fish completion script template. Replace <c>{0}</c> with the program name. The template assumes <c>{0} __complete fish ...</c>.
	/// </summary>
	/// <returns>A format string with <c>{0}</c> for the executable name.</returns>
	public static string GetFish() =>
		"# Generated completion — replace {0} with your executable name.\n" +
		"# Uses tokenized command line; drops the first token (program name). Requires fish 3.4+ for commandline -opc.\n" +
		"complete -c {0} -f -a \"({0} __complete fish -- (commandline -opc)[2..-1])\"\n";

	/// <summary>
	/// PowerShell completion script template. Replace <c>{0}</c> with the program name. The template assumes <c>{0} __complete powershell ...</c>.
	/// </summary>
	/// <returns>A format string with <c>{0}</c> for the executable name.</returns>
	public static string GetPowerShell() =>
		// ASCII only: Windows PowerShell 5.1 writes profiles in the ANSI code page.
		"# Generated completion - replace {0} with your executable name.\n" +
		"# Works in PowerShell 7+ and Windows PowerShell 5.1. Also matches {0}.exe and paths such as .\\bin\\{0}.exe.\n" +
		"Register-ArgumentCompleter -Native -CommandName '{0}' -ScriptBlock {\n" +
		"  param($wordToComplete, $commandAst, $cursorPosition)\n" +
		"  # As cobra and clap do, evaluate the line up to the cursor as PowerShell, so variables, $(...) and\n" +
		"  # quoting reach the app exactly as they would when the command runs. The executable runs as typed.\n" +
		"  $line = $commandAst.Extent.Text\n" +
		"  $end = $cursorPosition - $commandAst.Extent.StartOffset\n" +
		"  if ($line.Length -gt $end) { $line = $line.Substring(0, $end) }\n" +
		"  $exe = $commandAst.CommandElements[0].Extent\n" +
		"  $request = \"& $($exe.Text) __complete powershell '--' \" + $line.Substring($exe.EndOffset - $commandAst.Extent.StartOffset)\n" +
		"  if ($wordToComplete -eq '') {\n" +
		"    # The cursor follows a space, so complete a new, empty word. Legacy native argument passing (5.1,\n" +
		"    # 7.2 and earlier) drops '', so pass '\"\"' instead, which arrives as an empty word, as cobra does.\n" +
		"    $legacy = -not (Test-Path variable:PSNativeCommandArgumentPassing) -or $PSNativeCommandArgumentPassing -eq 'Legacy'\n" +
		"    $request += ' ' + $(if ($legacy) { \"'\" + '\"\"' + \"'\" } else { \"''\" })\n" +
		"  }\n" +
		"  # MenuComplete and Complete leave the cursor on the completed word, so add the space the user would type next.\n" +
		"  $space = ''\n" +
		"  if (Get-Module PSReadLine) {\n" +
		"    $tab = (Get-PSReadLineKeyHandler -Bound -ErrorAction Ignore | Where-Object { $_.Key -eq 'Tab' } | Select-Object -First 1).Function\n" +
		"    if ($tab -eq 'MenuComplete' -or $tab -eq 'Complete') { $space = ' ' }\n" +
		"  }\n" +
		"  # An unfinished line (an open quote or $( ) doesn't parse yet, so it gets no completions.\n" +
		"  try { $out = @(Invoke-Expression ($request + ' 2>$null')) } catch { return }\n" +
		"  $out | ForEach-Object {\n" +
		"    $text = if ($_ -match '[\\s''\"`$@#;,(){}|&<>]') { \"'\" + ($_ -replace \"'\", \"''\") + \"'\" } else { $_ }\n" +
		"    $type = if ($_.StartsWith('-')) { 'ParameterName' } else { 'ParameterValue' }\n" +
		"    [System.Management.Automation.CompletionResult]::new($text + $space, $_, $type, $_)\n" +
		"  }\n" +
		"}\n";
}
