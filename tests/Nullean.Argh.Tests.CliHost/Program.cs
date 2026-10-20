using System.Runtime.InteropServices;
using Nullean.Argh.Runtime;

// CI runs the integration tests under several Windows console code pages. The test runner starts each host
// with its own console, so the host switches that console to ARGH_TEST_CONSOLE_CODEPAGE before writing anything.
if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("ARGH_TEST_CONSOLE_CODEPAGE") is { Length: > 0 } codePage)
{
	if (!uint.TryParse(codePage, out var codePageId) || !ConsoleCodePage.SetConsoleOutputCP(codePageId))
	{
		Console.Error.WriteLine($"Error: cannot switch the console to code page '{codePage}'.");
		return 3;
	}
}

return await ArghRuntime.RunAsync(args);

internal static class ConsoleCodePage
{
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool SetConsoleOutputCP(uint codePageId);
}
