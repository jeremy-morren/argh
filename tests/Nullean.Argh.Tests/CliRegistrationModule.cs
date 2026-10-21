using System.Runtime.CompilerServices;
using Nullean.Argh;
using Nullean.Argh.Builder;
using Nullean.Argh.Tests.Fixtures;

namespace Nullean.Argh.Tests;

internal static class CliRegistrationModule
{
	[ModuleInitializer]
	internal static void RegisterCommands()
	{
		IArghRootBuilder app = new ArghBuilder();
		app.UseSchemaVersion("9.9.9-test");
		app.UseMiddleware<TestsGlobalMiddleware>();
		app.UseGlobalOptions<TestGlobalCliOptions>();
		app.MapAndRootAlias<CliRootPrefetchAliasCommands>();
		app.Map("hello", CliTestHandlers.Hello);
		app.Map("nin-hello", CliTestHandlers.NinHello);
		app.Map("enum-cmd", CliTestHandlers.EnumCmd);
		app.Map("enum-value-cmd", CliTestHandlers.EnumValueCmd);
		app.Map("deploy", CliTestHandlers.Deploy);
		app.Map("as-params-with-ct", CliTestHandlers.AsParamsWithCt);
		app.Map("nullable-numeric-as-params", CliTestHandlers.NullableNumericAsParams);
		app.Map("multi-enum-as-params", CliTestHandlers.MultiEnumAsParams);
		app.Map("optional-uri-as-params", CliTestHandlers.OptionalUriAsParams);
		app.Map("prop-doc-as-params", CliTestHandlers.PropDocAsParams);
		app.Map("param-comment-record", CliTestHandlers.ParamCommentRecordCmd);
		app.Map("tags", CliTestHandlers.Tags);
		app.Map("tag-set", CliTestHandlers.TagSet);
		app.Map("color-set", CliTestHandlers.ColorSet);
		app.Map("opt-tag-set", CliTestHandlers.OptTagSet);
		app.Map("as-params-tag-set", CliTestHandlers.AsParamsTagSet);
		app.Map("as-params-collection-syntax", CliTestHandlers.AsParamsCollectionSyntax);
		app.Map("as-params-optional-collection-syntax", CliTestHandlers.AsParamsOptionalCollectionSyntax);
		app.Map("as-params-referenced-dto", CliTestHandlers.AsParamsReferencedDto);
		app.Map("as-params-scalars", CliTestHandlers.AsParamsScalars);
		app.Map("brace-doc", CliTestHandlers.BraceDoc);
		app.Map("severity-cmd", CliTestHandlers.SeverityCmd);
		app.Map("dry-run-cmd", CliTestHandlers.DryRunCmd);
		app.Map("count-cmd", CliTestHandlers.CountCmd);
		app.Map("file-cmd", CliTestHandlers.FileCmd);
		app.Map("dir-cmd", CliTestHandlers.DirCmd);
		app.Map("uri-cmd", CliTestHandlers.UriCmd);
		app.Map("temporal-cmd", CliTestHandlers.TemporalCmd);
		app.Map("time-only-cmd", CliTestHandlers.TimeOnlyCmd);
		app.Map("params-items", CliTestHandlers.ParamsItems);
		app.Map("unicode-docs", CliTestHandlers.UnicodeDocs);
		app.Map("point-cmd", CliTestHandlers.PointCmd);
		app.Map("point-as-params-cmd", CliTestHandlers.PointAsParamsCmd);
		app.Map("optional-point-cmd", CliTestHandlers.OptionalPointCmd);
		app.Map("culture-choices", CliTestHandlers.CultureChoices);
		app.Map("culture-cmd", CliTestHandlers.CultureCmd);
		app.Map("numeric-choices", NumericCliHandlers.NumericChoices);
		app.Map("numeric-defaults", NumericCliHandlers.NumericDefaults);
		app.Map("numeric-ranges", NumericCliHandlers.NumericRanges);
		app.Map("parsables", ParsableHandlers.Parsables);
		app.Map("parsable-dto", ParsableHandlers.ParsableDto);
		app.Map("parsable-with-parser", ParsableHandlers.ParsableWithParser);
		app.Map("numeric-parsables", ParsableHandlers.NumericParsables);
		app.Map("local-names", CliTestHandlers.LocalNames);
		app.Map("local-names-dto", CliTestHandlers.LocalNamesDto);
		app.Map("tag-set-parser", CliTestHandlers.TagSetParser);
		app.Map("tag-set-parser-opt", CliTestHandlers.TagSetParserOpt);
		app.Map("doc-lambda", DocLambdaEcho);
		// Anonymous lambdas have no XML docs; use a named handler (e.g. DocLambdaEcho) for help text.
		app.Map("lambda-cmd", (string msg) => Console.Out.WriteLine($"lambda:{msg}"));
		app.Map("exit-code-cmd", CliTestHandlers.ExitCodeCmd);
		// Regression: a synchronous int-returning lambda must propagate its return value as the exit code too.
		app.Map("exit-code-lambda", (int code) => code);
		app.Map("validate-range", ValidationCliHandlers.ValidateRange);
		app.Map("validate-length", ValidationCliHandlers.ValidateLength);
		app.Map("validate-regex", ValidationCliHandlers.ValidateRegex);
		app.Map("validate-allowed", ValidationCliHandlers.ValidateAllowed);
		app.Map("validate-email", ValidationCliHandlers.ValidateEmail);
		app.Map("validate-email-opt", ValidationCliHandlers.ValidateEmailOptional);
		app.Map("validate-uri-scheme", ValidationCliHandlers.ValidateUriScheme);
		app.Map("validate-uri-scheme-opt", ValidationCliHandlers.ValidateUriSchemeOptional);
		app.Map("validate-non-nullable-range", ValidationCliHandlers.ValidateNonNullableRange);
		app.Map("validate-dto", ValidationCliHandlers.ValidateDto);
		app.Map("validate-timespan-range", ValidationCliHandlers.ValidateTimeSpanRange);
		app.Map("validate-existing-file", ValidationCliHandlers.ValidateExistingFile);
		app.Map("validate-non-existing-file", ValidationCliHandlers.ValidateNonExistingFile);
		app.Map("validate-existing-directory", ValidationCliHandlers.ValidateExistingDirectory);
		app.Map("validate-existing-directory-opt", ValidationCliHandlers.ValidateExistingOptionalDirectory);
		app.Map("validate-non-existing-file-opt", ValidationCliHandlers.ValidateNonExistingOptionalFile);
		app.Map("validate-expand-home-file", ValidationCliHandlers.ValidateExpandHomeFile);
		app.Map("validate-no-symlink-file", ValidationCliHandlers.ValidateNoSymlinkFile);
		app.Map("validate-no-symlink-file-opt", ValidationCliHandlers.ValidateNoSymlinkOptionalFile);
		app.Map("validate-existing-files", ValidationCliHandlers.ValidateExistingFiles);
		app.Map("validate-existing-directories", ValidationCliHandlers.ValidateExistingDirectories);
		app.Map("validate-file-extensions-collection", ValidationCliHandlers.ValidateFileExtensionsCollection);
		app.Map("validate-existing-file-extensions-collection", ValidationCliHandlers.ValidateExistingFileExtensionsCollection);
		app.Map("copy-variadic", ValidationCliHandlers.CopyVariadic);
		app.Map("mixed-variadic", ValidationCliHandlers.MixedVariadic);
		app.Map("compile-variadic", ValidationCliHandlers.CompileVariadic);
		app.Map("archive-variadic", ValidationCliHandlers.ArchiveVariadic);
		app.Map("archive-files-variadic", ValidationCliHandlers.ArchiveFilesVariadic);
		app.Map("long-name-override", ValidationCliHandlers.LongNameOverride);
		app.Map<DiProbeCommands>();
		app.Map<CommandNameOverrideCommands>();
		app.Map("env-cmd", EnvBindingHandlers.EnvCmd);
		app.Map("env-required", EnvBindingHandlers.EnvRequired);
		app.Map("env-bool", EnvBindingHandlers.EnvBool);
		app.Map("env-global", EnvBindingHandlers.EnvGlobal);
		app.Map("env-positional", EnvBindingHandlers.EnvPositional);
		app.MapNamespace("env-ns", "Env binding namespace options tests.", g =>
		{
			g.UseNamespaceOptions<EnvTestNamespaceOptions>();
			g.Map("key", EnvBindingHandlers.EnvNs);
		});
		app.Map("bool-flags", BoolBindingHandlers.BoolFlags);
		app.Map("bool-args", BoolBindingHandlers.BoolArgs);
		app.Map("bool-list", BoolBindingHandlers.BoolList);
		app.Map("bool-dto", BoolBindingHandlers.BoolDto);
		app.Map("bool-global", BoolBindingHandlers.BoolGlobal);
		app.MapNamespace("bool-ns", "Bool namespace options tests.", g =>
		{
			g.UseNamespaceOptions<BoolNamespaceOptions>();
			g.Map("show", BoolBindingHandlers.BoolNs);
		});
		app.MapNamespace("parsable-ns", "IParsable types as namespace options.", g =>
		{
			g.UseNamespaceOptions<ParsableNamespaceOptions>();
			g.Map("show", ParsableHandlers.ParsableNsShow);
		});
		app.MapNamespace("numeric-ns", "Numeric defaults and limits as namespace options.", g =>
		{
			g.UseNamespaceOptions<NumericNamespaceOptions>();
			g.Map("show", NumericCliHandlers.NumericNsShow);
		});
		app.MapNamespace("scalars-ns", "Special scalar types as namespace options.", g =>
		{
			g.UseNamespaceOptions<ScalarNamespaceOptions>();
			g.Map("show", CliTestHandlers.ScalarsNsShow);
		});
		app.Map("schema-default-value", SchemaSpecificHandlers.SchemaDefaultValue);
		app.Map("schema-separator-list", SchemaSpecificHandlers.SchemaSeparatorList);
		app.Map("schema-hidden-param", SchemaSpecificHandlers.SchemaHiddenParam);
		app.Map("schema-quoted-values", SchemaSpecificHandlers.SchemaQuotedValues);
		app.Map<SchemaHiddenCommands>();
		// These commands and the --old-name member are [Obsolete] on purpose: the schema tests read them as deprecated.
		// CS0612/CS0618 are the compiler's warnings for the handlers; AGH0036 is Argh's for the member the generated code binds.
#pragma warning disable CS0612, CS0618, AGH0036
		app.Map("schema-deprecated-simple", SchemaDeprecatedHandlers.SchemaDeprecatedSimple);
		app.Map("schema-deprecated-with-message", SchemaDeprecatedHandlers.SchemaDeprecatedWithMessage);
		app.Map("schema-deprecated-param", SchemaDeprecatedHandlers.SchemaDeprecatedParam);
#pragma warning restore CS0612, CS0618, AGH0036
		// FixtureStage.Preview is [Experimental]; Argh reports ARGHTEST001 here because its generated code uses it.
#pragma warning disable ARGHTEST001
		app.Map("experimental-api", ExperimentalApiHandlers.ExperimentalApi);
#pragma warning restore ARGHTEST001
		app.Map("schema-intent-destructive", SchemaIntentHandlers.SchemaIntentDestructive);
		app.Map("schema-intent-read", SchemaIntentHandlers.SchemaIntentRead);
		app.Map("schema-output-formats", SchemaOutputHandlers.SchemaOutputFormats);
		app.MapNamespace<SchemaHiddenNamespace>("schema-hidden-ns");
		app.MapNamespace<SchemaHiddenSchemaNamespace>("schema-hidden-schema-ns");
		app.MapNamespace<StorageCliCommands>("storage", g =>
		{
			g.UseNamespaceOptions<TestStorageCommandNamespaceOptions>();
			g.MapRoot(StorageNamespaceRoot);
			g.MapNamespace<StorageCliCommands.BlobCommands>("blob");
		});
		// Expression-bodied configure lambdas: nested MapNamespace must stay under each parent (AGH0022); same nested segment under two parents is OK.
		app.Map("cross-assembly-echo", CrossAssemblyHandlers.Echo);
		app.Map("ext-ns-as-params-echo", CrossAssemblyHandlers.ExternalNsEcho);
		app.MapNamespace("billing", "Billing commands", g => g.MapNamespace("tools", "Shared tools segment", h => h.Map("status", BillingToolsHandlers.Status)));
		app.MapNamespace("support", "Support commands", g => g.MapNamespace("tools", "Shared tools segment", h => h.Map("status", SupportToolsHandlers.Status)));
		// ASCII segment and summary keep the root help listing ASCII; the command names inside are not.
		app.MapNamespace("unicode", "Commands with non-ASCII names.", g => g.Map<UnicodeNameCommands>());
		// MapAndRootAlias: multi-command class; Build is [DefaultCommand] and also the root alias for this namespace.
		app.MapNamespace("alias-scope", "Root-alias integration test namespace.", g => g.MapAndRootAlias<RootAliasTestCommands>());
		app.MapNamespace("alias-followed", "Root alias followed by additional mapped command classes.", g =>
		{
			g.MapAndRootAlias<RootAliasSingleMethodCommands>();
			g.Map<RootAliasFollowupDiffCommands>();
			g.Map<RootAliasFollowupServeCommands>();
		});
	}

	/// <summary>Documented handler for lambda-style <c>Map</c> (XML appears in help).</summary>
	/// <param name="g">Injected global CLI options.</param>
	/// <param name="line">-l,--line, Text line to echo.</param>
	/// <example>doc-lambda --line hi</example>
	internal static void DocLambdaEcho(TestGlobalCliOptions g, string line) =>
		Console.Out.WriteLine($"doc-lambda:{line}");

	/// <summary>Provides the app-root integration-test default via <c>MapAndRootAlias</c> alongside a sentinel flag.</summary>
	internal sealed class CliRootPrefetchAliasCommands
	{
		/// <summary>Integration-test default when no sub-command is given at the app root.</summary>
		/// <remarks>Root default remarks for help layout tests.</remarks>
		/// <param name="g">Injected global CLI options.</param>
		/// <param name="prefetchRegression">parses-before-dispatch sentinel for root-alias prefetch.</param>
		[DefaultCommand]
		public static void Build(TestGlobalCliOptions g, bool? prefetchRegression = null) =>
			Console.Out.WriteLine(prefetchRegression == true ? "marker:root-default:prefetch" : "marker:root-default");

		public static void Other(TestGlobalCliOptions g) =>
			Console.Out.WriteLine("marker:root-other");
	}

	/// <summary>Integration-test default when only the <c>storage</c> namespace is selected.</summary>
	/// <remarks>Namespace default remarks for help layout tests.</remarks>
	internal static void StorageNamespaceRoot(TestStorageCommandNamespaceOptions o) =>
		Console.Out.WriteLine("marker:storage-ns-root");
}
