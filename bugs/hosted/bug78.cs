// Bug 78 - Hosting suppresses logging for any run that has --help or --version anywhere in argv
// Group C: runtime, dispatch and hosting
//
// Build:    dotnet build bugs/hosted -c Release
// Run:      Bugs.Hosted.exe bug78 --body hi
// Actual:   "info: ... body=hi" log line, then body:hi
// Run:      Bugs.Hosted.exe bug78 --body --version
// Actual:   body:--version only: the command ran, but its log line (and the host's) is suppressed
// Expected: logging behaves the same; "--version" here is a value, not the intrinsic
//
// Cause: IsArghBuiltinIntrinsic uses Array.Exists(args, a => a == "--help" || a == "--version")
//        (src/Nullean.Argh.Core/ArghApp.cs:170-174), consumed by ArghHostingExtensions.cs:48, 104.

using Microsoft.Extensions.Logging;

namespace Bugs.Hosted;

public sealed class Bug78Commands(ILogger<Bug78Commands> log)
{
	/// <summary>Logs its argument, then prints it.</summary>
	public void Bug78(string body)
	{
		log.LogInformation("bug78 body={Body}", body);
		Console.Out.WriteLine($"body:{body}");
	}
}
