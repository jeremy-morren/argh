using System.Globalization;
using System.Runtime.CompilerServices;
using Nullean.Argh.Parsing;
using Nullean.Argh.Runtime;

/// <summary>
/// Parser option tests set the culture and styles through ARGH_TEST_CULTURE, ARGH_TEST_NUMBER_STYLES and
/// ARGH_TEST_DATE_STYLES. A module initializer applies them before <c>Main</c>, so <c>Program.cs</c> stays unchanged.
/// </summary>
internal static class TestHostParserOptions
{
#pragma warning disable CA2255 // Test host only: the options must be set before the CLI runs.
	[ModuleInitializer]
#pragma warning restore CA2255
	internal static void Apply()
	{
		var culture = Environment.GetEnvironmentVariable("ARGH_TEST_CULTURE");
		var numberStyles = Environment.GetEnvironmentVariable("ARGH_TEST_NUMBER_STYLES");
		var dateStyles = Environment.GetEnvironmentVariable("ARGH_TEST_DATE_STYLES");
		if (string.IsNullOrEmpty(culture) && string.IsNullOrEmpty(numberStyles) && string.IsNullOrEmpty(dateStyles))
			return;

		var options = new ParserOptions
		{
			Culture = string.IsNullOrEmpty(culture) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture),
			NumberStyles = string.IsNullOrEmpty(numberStyles) ? NumberStyles.Float : Enum.Parse<NumberStyles>(numberStyles),
		};
		if (!string.IsNullOrEmpty(dateStyles))
			options.DateTimeStyles = Enum.Parse<DateTimeStyles>(dateStyles);
		ArghRuntime.ParserOptions = options;
	}
}
