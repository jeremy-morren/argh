using System.Globalization;
using System.Runtime.CompilerServices;
using Nullean.Argh.Parsing;
using Nullean.Argh.Runtime;

/// <summary>
/// Parser option tests set the culture and styles through ARGH_TEST_CULTURE, ARGH_TEST_NUMBER_STYLES and
/// ARGH_TEST_DATE_STYLES, what a repeated flag does through ARGH_TEST_REPEATED, and how names match through
/// ARGH_TEST_COMMAND_COMPARER and ARGH_TEST_ARGUMENT_COMPARER (a <see cref="StringComparison"/> name). A module initializer applies them
/// before <c>Main</c>, so <c>Program.cs</c> stays unchanged.
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
		var repeated = Environment.GetEnvironmentVariable("ARGH_TEST_REPEATED");
		var commandComparer = Environment.GetEnvironmentVariable("ARGH_TEST_COMMAND_COMPARER");
		var argumentComparer = Environment.GetEnvironmentVariable("ARGH_TEST_ARGUMENT_COMPARER");
		if (string.IsNullOrEmpty(culture) && string.IsNullOrEmpty(numberStyles) && string.IsNullOrEmpty(dateStyles)
		    && string.IsNullOrEmpty(repeated) && string.IsNullOrEmpty(commandComparer) && string.IsNullOrEmpty(argumentComparer))
			return;

		var options = new ParserOptions
		{
			Culture = string.IsNullOrEmpty(culture) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture),
			NumberStyles = string.IsNullOrEmpty(numberStyles) ? NumberStyles.Float : Enum.Parse<NumberStyles>(numberStyles),
		};
		if (!string.IsNullOrEmpty(dateStyles))
			options.DateTimeStyles = Enum.Parse<DateTimeStyles>(dateStyles);
		if (!string.IsNullOrEmpty(repeated))
			options.RepeatedScalarBehaviour = Enum.Parse<Nullean.Argh.RepeatedScalarBehaviour>(repeated);
		if (!string.IsNullOrEmpty(commandComparer))
			options.CommandStringComparer = StringComparer.FromComparison(Enum.Parse<StringComparison>(commandComparer));
		if (!string.IsNullOrEmpty(argumentComparer))
			options.ArgumentStringComparer = StringComparer.FromComparison(Enum.Parse<StringComparison>(argumentComparer));
		ArghRuntime.ParserOptions = options;
	}
}
