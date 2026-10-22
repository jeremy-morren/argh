using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Nullean.Argh;

public sealed partial class CliParserGenerator
{
	/// <summary>An ISO 8601 duration for <paramref name="ticks"/>, such as <c>PT5M</c> or <c>-P1DT2H</c>.</summary>
	private static string IsoDuration(long ticks) => System.Xml.XmlConvert.ToString(System.TimeSpan.FromTicks(ticks));

	private static void EmitRootSchemaBlock(CodeWriter w)
	{
		using (w.If("CompletionProtocol.IsSchemaInvocation(args)"))
		{
			w.Line("Console.Out.Write(ArghRuntime.FormatCliSchemaJson());");
			w.Return("0");
		}

		w.Line();
	}

	// The schema document is one nested constructor expression. Each part below returns its expression with relative
	// indentation, and the writer indents the whole where it lands.

	private static void EmitBuildCliSchemaDocumentHierarchical(CodeWriter w, AppEmitModel app,
		string entryAssemblyName, string entryAssemblyVersion)
	{
		var prefix = app.EnvironmentPrefix;
		var args = new List<string>
		{
			"1",
			Quote(entryAssemblyName),
			Quote(entryAssemblyVersion),
			QuoteOrNull(app.RootSummary),
			"new[] { \"__complete\", \"__completion\", \"__schema\" }",
			SchemaOptionsExpression(app.GlobalOptionsModel, prefix),
			SchemaDefaultHandlerExpression("root", app.Root.RootCommand, entryAssemblyName, prefix),
			SchemaCommandsExpression(app.Root, entryAssemblyName, prefix),
			SchemaNamespacesExpression(app.Root.Children, entryAssemblyName, prefix),
		};
		if (SchemaEnvironmentExpression(app) is { } environment)
			args.Add("Environment: " + environment);
		w.Arrow("internal static ArghCliSchemaDocument BuildCliSchemaDocument()", CodeWriter.ItemList("new ArghCliSchemaDocument(", args, ",", ")", ""));
	}

	/// <summary>The flags of a global or namespace options type.</summary>
	private static string SchemaOptionsExpression(OptionsTypeModel? options, string? environmentPrefix) =>
		SchemaParametersExpression(
			options?.Members.Where(static p => p.Kind == ParameterKind.Flag) ?? Enumerable.Empty<ParameterModel>(), environmentPrefix);

	private static string SchemaParametersExpression(IEnumerable<ParameterModel> parameters, string? environmentPrefix) =>
		CodeWriter.NewArray("CliParameterSchema", parameters.Select(p => CliParameterSchemaExpression(p, environmentPrefix)).ToList());

	private static string SchemaDefaultHandlerExpression(string kind, CommandModel? rootCmd, string entryAssemblyName,
		string? environmentPrefix)
	{
		if (rootCmd is not { IsRootDefault: true } rc)
			return "null";

		return CodeWriter.New("CliDefaultHandlerSchema",
		[
			Quote(kind),
			QuoteOrNull(rc.SummaryOneLiner),
			QuoteOrNull(rc.RemarksRendered),
			QuoteOrNull(BuildCommandUsageSynopsisTail(rc, entryAssemblyName)),
			SchemaExamplesExpression(rc.ExamplesRendered),
			SchemaParametersExpression(rc.Parameters.Where(static p => p.Kind != ParameterKind.Injected), environmentPrefix),
		]);
	}

	private static string SchemaCommandsExpression(RegistryNode node, string entryAssemblyName, string? environmentPrefix) =>
		CodeWriter.NewArray("CliCommandSchema", node.Commands
			.Where(static c => !c.IsRootDefault)
			.OrderBy(c => c.CommandName, StringComparer.Ordinal)
			.Select(c => CliCommandSchemaExpression(c, entryAssemblyName, environmentPrefix))
			.ToList());

	private static string CliCommandSchemaExpression(CommandModel cmd, string entryAssemblyName, string? environmentPrefix)
	{
		var args = new List<string>
		{
			StringArrayExpression(cmd.RoutePrefix.IsDefault ? ImmutableArray<string>.Empty : cmd.RoutePrefix),
			Quote(cmd.CommandName),
			QuoteOrNull(cmd.SummaryOneLiner),
			QuoteOrNull(cmd.RemarksRendered),
			QuoteOrNull(BuildCommandUsageSynopsisTail(cmd, entryAssemblyName)),
			SchemaExamplesExpression(cmd.ExamplesRendered),
			SchemaParametersExpression(cmd.Parameters.Where(static p => p.Kind != ParameterKind.Injected), environmentPrefix),
		};
		if (!cmd.CommandAliases.IsDefaultOrEmpty)
			args.Add("Aliases: " + InlineStringArray(cmd.CommandAliases));
		if (cmd.IsHiddenInSchema)
			args.Add("Hidden: true");
		if (cmd.IsDeprecated)
			args.Add("Deprecated: " + SchemaDeprecationExpression(cmd.DeprecationMessage));
		if (cmd.Intent is { } intent)
		{
			var intentParts = new List<string>();
			if (intent.Destructive is bool d) intentParts.Add($"Destructive: {BoolLiteral(d)}");
			if (intent.Idempotent is bool i) intentParts.Add($"Idempotent: {BoolLiteral(i)}");
			if (intent.Scope is string sc) intentParts.Add($"Scope: {Quote(sc)}");
			if (intent.RequiresConfirmation is bool rc) intentParts.Add($"RequiresConfirmation: {BoolLiteral(rc)}");
			if (intent.RequiresAuth is bool ra) intentParts.Add($"RequiresAuth: {BoolLiteral(ra)}");
			args.Add($"Intent: new CliIntentSchema({string.Join(", ", intentParts)})");
		}

		if (cmd.Output is { } output && !output.Formats.IsDefaultOrEmpty)
		{
			var formatFlag = output.FormatFlag is not null ? $", FormatFlag: {Quote(output.FormatFlag)}" : "";
			args.Add($"Output: new CliOutputSchema(Formats: {InlineStringArray(output.Formats)}{formatFlag})");
		}

		return CodeWriter.New("CliCommandSchema", args);
	}

	private static string SchemaDeprecationExpression(string? message) =>
		message is not null ? $"new CliDeprecationSchema(Message: {Quote(message)})" : "new CliDeprecationSchema()";

	private static string StringArrayExpression(IEnumerable<string> values) =>
		CodeWriter.NewArray("string", values.Select(Quote).ToList());

	private static string SchemaNamespacesExpression(List<RegistryNode.NamedCommandNamespaceChild> children,
		string entryAssemblyName, string? environmentPrefix) =>
		CodeWriter.NewArray("CliNamespaceSchema", children
			.OrderBy(c => c.Segment, StringComparer.OrdinalIgnoreCase)
			.Select(ch => CliNamespaceSchemaExpression(ch, entryAssemblyName, environmentPrefix))
			.ToList());

	private static string CliNamespaceSchemaExpression(RegistryNode.NamedCommandNamespaceChild ch, string entryAssemblyName,
		string? environmentPrefix)
	{
		var node = ch.Node;
		return CodeWriter.New("CliNamespaceSchema",
		[
			Quote(ch.Segment),
			QuoteOrNull(ch.SummaryOneLiner),
			QuoteOrNull(FlattenTypeRemarksInnerXml(node.RemarksInnerXml)),
			SchemaOptionsExpression(node.CommandNamespaceOptionsModel, environmentPrefix),
			SchemaDefaultHandlerExpression("namespace", node.RootCommand, entryAssemblyName, environmentPrefix),
			SchemaCommandsExpression(node, entryAssemblyName, environmentPrefix),
			SchemaNamespacesExpression(node.Children, entryAssemblyName, environmentPrefix),
		]);
	}

	private static string FlattenTypeRemarksInnerXml(string? innerXml)
	{
		if (string.IsNullOrWhiteSpace(innerXml))
			return "";
		var wrapped = "<remarks>" + innerXml + "</remarks>";
		return Documentation.ParseMethod(wrapped, CSharpParseOptions.Default).RemarksRendered;
	}

	private static string SchemaExamplesExpression(string? examplesRendered) =>
		StringArrayExpression(string.IsNullOrWhiteSpace(examplesRendered)
			? Enumerable.Empty<string>()
			: examplesRendered!.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None)
				.Select(s => s.Trim())
				.Where(s => s.Length > 0));

	private static string? SchemaEnvironmentExpression(AppEmitModel app)
	{
		var args = new List<string>();
		if (!app.EnvironmentVars.IsDefaultOrEmpty)
		{
			args.Add("Variables: " + CodeWriter.NewArray("CliEnvVarSchema", app.EnvironmentVars.Select(v =>
			{
				var desc = v.Description is not null ? $", Description: {Quote(v.Description)}" : "";
				var req = v.Required ? ", Required: true" : "";
				var defVal = v.DefaultValue is not null ? $", DefaultValue: {Quote(v.DefaultValue)}" : "";
				return $"new CliEnvVarSchema({Quote(v.Name)}{desc}{req}{defVal})";
			}).ToList()));
		}

		if (!app.ConfigFiles.IsDefaultOrEmpty)
		{
			args.Add("ConfigFiles: " + CodeWriter.NewArray("CliConfigFileSchema", app.ConfigFiles.Select(c =>
			{
				var desc = c.Description is not null ? $", Description: {Quote(c.Description)}" : "";
				var req = c.Required ? ", Required: true" : "";
				return $"new CliConfigFileSchema({Quote(c.Path)}{desc}{req})";
			}).ToList()));
		}

		return args.Count == 0 ? null : CodeWriter.New("CliEnvironmentSchema", args);
	}

	private static string CliParameterSchemaExpression(ParameterModel p, string? environmentPrefix = null)
	{
		var role = p.IsConfirmationSkip ? "confirmationSkip"
			: p.IsDryRun ? "dryRun"
			: p.Kind == ParameterKind.Positional ? "positional"
			: "flag";
		var type = MapToJsonSchemaType(p.ScalarKind, p.TypeName);
		var args = new List<string>
		{
			Quote(role),
			Quote(p.CliLongName),
			p.ShortOpt is char c ? Quote(c.ToString()) : "null",
			Quote(type),
			BoolLiteral(p.IsRequired),
			string.IsNullOrEmpty(p.Description) ? "null" : Quote(p.Description),
		};

		if (GetSchemaDefaultValue(p) is { } defaultVal)
			args.Add($"DefaultValue: {Quote(defaultVal)}");

		if (p.IsCollection)
			args.Add(p.CollectionSeparator is null ? "Repeatable: true" : $"Separator: {Quote(p.CollectionSeparator)}");

		if (!p.Aliases.IsDefaultOrEmpty)
			args.Add("Aliases: " + InlineStringArray(p.Aliases));

		if (type == "enum" && !p.EnumMemberNames.IsDefaultOrEmpty)
			args.Add("EnumValues: " + InlineStringArray(p.EnumMemberNames.Select((m, i) => ResolveEnumMemberCliName(p.EnumMemberCliNames, i, m))));

		if (p.IsCollection)
		{
			var elemType = MapToJsonSchemaType(p.ElementScalarKind, p.ElementTypeName);
			// elementType only accepts string/integer/number/boolean per the meta-schema; enum members serialize as strings
			if (elemType == "enum") elemType = "string";
			args.Add($"ElementType: {Quote(elemType)}");
		}

		if (p.IsHiddenInSchema)
			args.Add("Hidden: true");

		if (p.IsVariadic)
			args.Add("Variadic: true");

		if (p.IsDeprecated)
			args.Add("Deprecated: " + SchemaDeprecationExpression(p.DeprecationMessage));

		var validations = BuildConstraintsExpression(p.Validations, p.ExpandUserProfileBeforeBind);
		if (validations != "null")
			args.Add($"Validations: {validations}");

		return $"new CliParameterSchema({string.Join(", ", args)})";
	}

	private static string? GetSchemaDefaultValue(ParameterModel p)
	{
		if (!HasShownDefault(p))
			return null;
		var formatted = FormatDefaultForHelp(p);
		return string.IsNullOrEmpty(formatted) ? null : formatted;
	}

	private static string MapToJsonSchemaType(CliScalarKind sk, string typeName)
	{
		if (sk == CliScalarKind.Enum)
			return "enum";
		if (sk == CliScalarKind.Collection)
			return "array";
		// Parsable types are named like primitives (short, char, Guid), so integer types map to "integer" too.
		if (sk != CliScalarKind.Primitive && sk != CliScalarKind.Parsable)
			return "string";

		// GetSimpleTypeName returns nullable variants with a trailing '?' (e.g. "int?", "bool?")
		var t = typeName.TrimEnd('?');
		return t switch
		{
			"String" or "string" or "Char" or "char" => "string",
			"Boolean" or "bool" => "boolean",
			"Int16" or "Int32" or "Int64" or "UInt16" or "UInt32" or "UInt64"
				or "Byte" or "SByte" or "short" or "int" or "long"
				or "ushort" or "uint" or "ulong" or "byte" or "sbyte" => "integer",
			"Single" or "Double" or "Decimal" or "float" or "double" or "decimal" => "number",
			_ => "string"
		};
	}

	private static string BuildConstraintsExpression(ImmutableArray<ValidationConstraint> validations, bool expandUserProfile)
	{
		var parts = new System.Collections.Generic.List<string>();

		if (!validations.IsDefaultOrEmpty)
		{
			foreach (var v in validations)
			{
				switch (v)
				{
					case RangeConstraint r:
						// An unbounded side is left out, which cli-schema v1 allows.
						var rangeMin = r.SchemaMin is { } sMin ? Quote(sMin) : "null";
						var rangeMax = r.SchemaMax is { } sMax ? Quote(sMax) : "null";
						parts.Add($"new CliConstraintSchema(\"range\", Min: {rangeMin}, Max: {rangeMax})");
						break;
					case TimeSpanRangeConstraint ts:
						// cli-schema v1 gives a duration range in ISO 8601, such as PT5M.
						parts.Add($"new CliConstraintSchema(\"timeSpanRange\", Min: {Quote(IsoDuration(ts.MinTicks))}, Max: {Quote(IsoDuration(ts.MaxTicks))})");
						break;
					case CollectionCountConstraint cc:
						var ccMin = cc.Min.HasValue ? $"\"{cc.Min.Value}\"" : "null";
						var ccMax = cc.Max.HasValue ? $"\"{cc.Max.Value}\"" : "null";
						parts.Add($"new CliConstraintSchema(\"count\", Min: {ccMin}, Max: {ccMax})");
						break;
					case StringLengthConstraint sl:
						var slMin = sl.Min.HasValue ? $"\"{sl.Min.Value}\"" : "null";
						var slMax = sl.Max.HasValue ? $"\"{sl.Max.Value}\"" : "null";
						parts.Add($"new CliConstraintSchema(\"length\", Min: {slMin}, Max: {slMax})");
						break;
					case RegexConstraint re:
						parts.Add($"new CliConstraintSchema(\"regex\", Pattern: {Quote(re.Pattern)})");
						break;
					case AllowedValuesConstraint av:
						parts.Add($"new CliConstraintSchema(\"allowed\", Values: {InlineStringArray(av.DisplayValues)})");
						break;
					case DeniedValuesConstraint dv:
						parts.Add($"new CliConstraintSchema(\"denied\", Values: {InlineStringArray(dv.DisplayValues)})");
						break;
					case EmailConstraint:
						parts.Add("new CliConstraintSchema(\"email\")");
						break;
					case UrlConstraint:
						parts.Add("new CliConstraintSchema(\"url\")");
						break;
					case UriSchemeConstraint us:
						parts.Add($"new CliConstraintSchema(\"uriScheme\", Values: {InlineStringArray(us.Schemes)})");
						break;
					case FileExtensionsConstraint fe:
						parts.Add($"new CliConstraintSchema(\"fileExtensions\", Values: {InlineStringArray(fe.Extensions)})");
						break;
					case ExistingPathConstraint:
						parts.Add("new CliConstraintSchema(\"existing\")");
						break;
					case NonExistingPathConstraint:
						parts.Add("new CliConstraintSchema(\"nonExisting\")");
						break;
					case RejectSymbolicLinksConstraint:
						parts.Add("new CliConstraintSchema(\"rejectSymbolicLinks\")");
						break;
				}
			}
		}

		// expandUserProfile is an Argh path-expansion behaviour, not a cli-schema constraint kind — omit it

		if (parts.Count == 0)
			return "null";

		return $"new CliConstraintSchema[] {{ {string.Join(", ", parts)} }}";
	}

}
