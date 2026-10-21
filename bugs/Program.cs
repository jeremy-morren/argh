using System.Globalization;
using Nullean.Argh.Runtime;

// BUGS_CULTURE=sv-SE   sets the current culture before parsing (bug72).
var culture = Environment.GetEnvironmentVariable("BUGS_CULTURE");
if (!string.IsNullOrEmpty(culture))
	CultureInfo.CurrentCulture = CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(culture);

var exit = await ArghRuntime.RunAsync(args);

// BUGS_RUN_TWICE=1   runs the same command line again in-process (bug77).
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUGS_RUN_TWICE")))
{
	Console.Out.WriteLine("--- second run");
	exit = await ArghRuntime.RunAsync(args);
}

return exit;
