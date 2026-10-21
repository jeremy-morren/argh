# Tests the app's PowerShell completion in the PowerShell that runs this script (Windows PowerShell 5.1 or pwsh).
# It parses the script, loads it the way the docs say (... | Out-String | Invoke-Expression), then asks
# PowerShell's own completion engine with TabExpansion2, which is what Tab calls.
# Usage: powershell -File test-powershell.ps1 -Target windows-powershell    (or pwsh ... -Target windows-pwsh)
# Keep this file Windows PowerShell 5.1 compatible: no ternary, no null-coalescing, no pipeline chains.
param(
	[Parameter(Mandatory = $true)][string]$Target,
	[string]$App = 'Basic'
)
$ErrorActionPreference = 'Stop'
$failures = 0

"PowerShell $($PSVersionTable.PSVersion) ($($PSVersionTable.PSEdition)), target $Target"

$script = & $App __completion powershell | Out-String
if ($LASTEXITCODE -ne 0) { "FAIL: '$App __completion powershell' exited with $LASTEXITCODE"; exit 1 }

$tokens = $null
$errors = $null
[void][System.Management.Automation.Language.Parser]::ParseInput($script, [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) {
	"FAIL: PowerShell can't parse the completion script:"
	$errors | ForEach-Object { "  $($_.Extent.StartLineNumber): $($_.Message)" }
	exit 1
}
'ok   script parses'

$script | Invoke-Expression

$known = @(Get-Content (Join-Path $PSScriptRoot 'known-failures.txt') | Where-Object { $_ -and -not $_.StartsWith('#') })

foreach ($entry in Get-Content (Join-Path $PSScriptRoot 'cases.txt')) {
	if (-not $entry -or $entry.StartsWith('#')) { continue }
	$name, $line, $expected = $entry.Split('|')

	$result = TabExpansion2 -inputScript $line -cursorColumn $line.Length
	$got = @($result.CompletionMatches | ForEach-Object { $_.CompletionText.TrimEnd() })
	$missing = @($expected.Split(' ', [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { $got -notcontains $_ })

	$knownEntry = $known | Where-Object { ($_ -split '\s+', 3)[0] -eq $Target -and ($_ -split '\s+', 3)[1] -eq $name } | Select-Object -First 1
	$shown = ($got | ForEach-Object { "'$_'" }) -join ' '
	if ($missing.Count -eq 0 -and -not $knownEntry) {
		"ok   ${name}: [$line] -> $shown"
	}
	elseif ($missing.Count -eq 0) {
		"FAIL ${name}: passes now; remove it from known-failures.txt ($knownEntry)"
		$failures++
	}
	elseif ($knownEntry) {
		"known ${name}: [$line] -> $shown (missing: $($missing -join ' ')) -- $knownEntry"
	}
	else {
		"FAIL ${name}: [$line] -> $shown (missing: $($missing -join ' '))"
		$failures++
	}
}

if ($failures -gt 0) { exit 1 }
exit 0
