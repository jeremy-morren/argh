using System.ComponentModel;
using System.Globalization;
using Nullean.Argh.Parsing;
using Nullean.Argh.Schema;

namespace Nullean.Argh.Runtime;

/// <summary>
/// Holds delegates to the application assembly's source-generated CLI entry type in the <c>Nullean.Argh</c> namespace (name is derived per assembly),
/// registered by a module initializer emitted with the generator (AOT-safe, no reflection).
/// </summary>
public static class ArghRuntime
{
	private static Func<string[], Task<int>>? _runAsyncFunc;
	private static Func<string[], RouteMatch?>? _routeFunc;
	private static Func<ArghCliSchemaDocument>? _cliSchemaFactory;
	private static Func<string[], bool>? _intrinsicFunc;
	private static ParserOptions _parserOptions = new();
	private static readonly AsyncLocal<ParserOptions?> s_scopedParserOptions = new();

	/// <summary>
	/// How Argh parses argument and environment-variable values: the culture, the number styles, what a repeated flag does,
	/// and how command and option names match.
	/// Set them with <c>UseParserOptions</c> (or <c>UseCulture</c>) on the app or hosting builder; setting <see langword="null"/>
	/// restores the defaults. Custom <c>IArgumentParser&lt;T&gt;</c> implementations receive them as an argument; middleware can read them here.
	/// </summary>
	public static ParserOptions ParserOptions
	{
		get => s_scopedParserOptions.Value ?? _parserOptions;
		set => _parserOptions = value ?? new ParserOptions();
	}

	/// <summary>
	/// The culture in <see cref="ParserOptions"/>, <see cref="CultureInfo.InvariantCulture"/> by default. Setting it keeps the
	/// other configured options.
	/// </summary>
	public static CultureInfo Culture
	{
		get => ParserOptions.Culture;
		set => _parserOptions = new ParserOptions
		{
			Culture = value, NumberStyles = _parserOptions.NumberStyles, DateTimeStyles = _parserOptions.DateTimeStyles,
			RepeatedScalarBehaviour = _parserOptions.RepeatedScalarBehaviour,
			CommandStringComparer = _parserOptions.CommandStringComparer, ArgumentStringComparer = _parserOptions.ArgumentStringComparer,
		};
	}

	/// <summary>
	/// Makes <see cref="ParserOptions"/> return <paramref name="options"/> in the current async flow until the returned scope is disposed.
	/// Used by the generated <c>TryParseArgh</c> overloads that take parser options; not intended for app code.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static IDisposable PushParserOptions(ParserOptions? options)
	{
		var previous = s_scopedParserOptions.Value;
		s_scopedParserOptions.Value = options;
		return new ParserOptionsScope(previous);
	}

	private sealed class ParserOptionsScope(ParserOptions? previous) : IDisposable
	{
		public void Dispose() => s_scopedParserOptions.Value = previous;
	}

	/// <summary>
	/// Registers the generated CLI runner. Called from emitted module initialization; not intended for app code.
	/// </summary>
	public static void RegisterRunner(Func<string[], Task<int>> runAsync) =>
		_runAsyncFunc = runAsync ?? throw new ArgumentNullException(nameof(runAsync));

	/// <summary>
	/// Registers the generated route helper. Called from emitted module initialization; not intended for app code.
	/// </summary>
	public static void RegisterRoute(Func<string[], RouteMatch?> route) =>
		_routeFunc = route ?? throw new ArgumentNullException(nameof(route));

	/// <summary>
	/// Registers the generated CLI schema document factory. Called from emitted module initialization; not intended for app code.
	/// </summary>
	public static void RegisterCliSchema(Func<ArghCliSchemaDocument> factory) =>
		_cliSchemaFactory = factory ?? throw new ArgumentNullException(nameof(factory));

	/// <summary>
	/// Registers the generated intrinsic command detector. Called from emitted module initialization; not intended for app code.
	/// </summary>
	public static void RegisterIntrinsic(Func<string[], bool> detector) =>
		_intrinsicFunc = detector ?? throw new ArgumentNullException(nameof(detector));

	/// <summary>
	/// Returns <see langword="true"/> if <paramref name="args"/> route to a user-defined <c>[CommandIntrinsic]</c> command.
	/// Does not check built-in intrinsic commands; use <see cref="ArghApp.IsIntrinsicCommand"/> for the full check.
	/// </summary>
	internal static bool IsUserIntrinsicCommand(string[] args) =>
		_intrinsicFunc?.Invoke(args) ?? false;

	/// <summary>
	/// Serializes the registered <see cref="ArghCliSchemaDocument"/> to indented JSON (camelCase), for <c>__schema</c> and programmatic use.
	/// </summary>
	public static string FormatCliSchemaJson()
	{
		if (_cliSchemaFactory is null)
			throw new InvalidOperationException(
				"CLI schema factory is not registered. Reference Nullean.Argh, register commands with ArghApp, and ensure the source generator runs so generated entry types are emitted in this assembly.");

		return CliSchemaJsonWriter.Write(_cliSchemaFactory());
	}

	/// <summary>
	/// Runs the source-generated CLI for the application assembly (same behavior as the generated <c>RunAsync</c> on the per-assembly CLI entry type).
	/// </summary>
	public static Task<int> RunAsync(string[] args)
	{
		if (_runAsyncFunc is null)
			throw new InvalidOperationException(
				"CLI runner is not registered. Reference Nullean.Argh, register commands with ArghApp, and ensure the source generator runs so generated entry types are emitted in this assembly.");

		return _runAsyncFunc(args);
	}

	/// <summary>
	/// Routes argv using the same rules as <see cref="RunAsync"/> without invoking handlers.
	/// </summary>
	public static RouteMatch? Route(string[] args)
	{
		if (_routeFunc is null)
			throw new InvalidOperationException(
				"CLI route delegate is not registered. Reference Nullean.Argh, register commands with ArghApp, and ensure the source generator runs so generated entry types are emitted in this assembly.");

		if (args is null)
			throw new ArgumentNullException(nameof(args));

		return _routeFunc(args);
	}
}
