using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Nullean.Argh;

public sealed partial class CliParserGenerator
{
	private static string GetCommandRoutePath(CommandModel cmd)
	{
		if (cmd.IsRootDefault)
		{
			if (cmd.RoutePrefix.IsDefaultOrEmpty)
				return "<root>";
			return string.Join("/", cmd.RoutePrefix) + "/<root>";
		}

		if (cmd.RoutePrefix.IsDefaultOrEmpty)
			return cmd.CommandName;
		return string.Join("/", cmd.RoutePrefix) + "/" + cmd.CommandName;
	}

	/// <summary>Help printer invoked from generated command runners for default/root handlers (overview lives in root or namespace overview).</summary>
	private static string HelpPrinterMethodForCommand(CommandModel cmd)
	{
		if (cmd.IsRootDefault)
		{
			if (cmd.RoutePrefix.IsDefaultOrEmpty)
				return "PrintRootHelp";
			return "PrintHelp_CommandNamespace_" + CommandNamespacePathKey(cmd.RoutePrefix);
		}

		return "PrintHelp_" + cmd.RunMethodName;
	}

	private const string HelpBlankLine = "Console.Out.WriteLine();";

	private static string HelpSection(string title) => $"Console.Out.WriteLine(CliHelpFormatting.Section({Quote(title)}));";

	private static string HelpUsageLine(string entryAssemblyName, string tail) =>
		$"Console.Out.WriteLine(CliHelpFormatting.Section(\"Usage: \") + CliHelpFormatting.Accent({Quote(entryAssemblyName)}) + {Quote(tail)});";

	/// <summary>A built-in option row such as <c>-h, --help</c>: the name padded to the option column, then its description.</summary>
	private static string HelpBuiltInRow(string name, int maxOptWidth, string description) =>
		$"Console.Out.WriteLine(\"  \" + CliHelpFormatting.Placeholder({Quote(name.PadRight(maxOptWidth))}) + \"  {description}\");";

	/// <summary>The rows of a <c>Namespaces:</c> or <c>Commands:</c> listing, names padded to the longest.</summary>
	private static void EmitHelpListing(CodeWriter w, bool namespaces, IReadOnlyList<(string Name, string Summary)> rows)
	{
		var width = rows.Max(r => r.Name.Length);
		foreach ((var name, var summary) in rows)
			w.Line($"CliHelpFormatting.WriteHelpListNameAndDescription({BoolLiteral(namespaces)}, {Quote(name)}, {QuoteOrNull(summary)}, {width});");
	}

	/// <summary>A call rendering XML documentation at runtime, indented by <paramref name="indent"/>.</summary>
	private static string WriteIndentedDocCall(string indent, string innerXml, bool remarks) =>
		// Concatenate (do not use $"..." interpolation): inner XML can contain `{`/`}` from generic cref text.
		"global::Nullean.Argh.Help.XmlDocumentationRenderer.WriteIndentedDoc(Console.Out, \"" + indent + "\", \"" + EscapeDocXml(innerXml) + "\", " + BoolLiteral(remarks) + ");";

	/// <summary>Plain documentation text split into lines, each trimmed; a blank line comes back empty.</summary>
	private static IEnumerable<string> PlainDocLines(string text) =>
		text.Replace("\r\n", "\n").Split('\n').Select(static part =>
		{
			var line = part.TrimEnd('\r');
			return string.IsNullOrWhiteSpace(line) ? "" : line.Trim();
		});

	/// <summary>
	/// Summary or remarks under a usage line, three spaces in: the XML documentation when there is some, the plain text
	/// otherwise. Plain lines are styled (white summary, gray remarks) unless <paramref name="styled"/> is false, as for a
	/// default handler's overview.
	/// </summary>
	private static void EmitHelpDocLines(CodeWriter w, string? innerXml, string? plainFallback, bool remarks, bool styled = true)
	{
		if (!string.IsNullOrWhiteSpace(innerXml))
		{
			w.Line(WriteIndentedDocCall("   ", innerXml!, remarks));
			return;
		}

		if (string.IsNullOrWhiteSpace(plainFallback))
			return;
		var styler = remarks ? "CliHelpFormatting.DocRemarksLine" : "CliHelpFormatting.DocSummaryLine";
		foreach (var line in PlainDocLines(plainFallback!))
		{
			if (line.Length == 0)
				w.Line(HelpBlankLine);
			else if (styled)
				w.Line($"Console.Out.WriteLine(\"   \" + {styler}({Quote(line)}));");
			else
				w.Line($"Console.Out.WriteLine(\"   \" + {Quote(line)});");
		}
	}

	/// <summary>Flatten remarks inner XML to plain text at generation time (used for single-line detection).</summary>
	private static string FlattenRemarksXml(string? innerXml)
	{
		if (string.IsNullOrWhiteSpace(innerXml))
			return "";
		try
		{
			var el = XElement.Parse("<x>" + innerXml + "</x>", LoadOptions.PreserveWhitespace);
			return Documentation.FlattenBlockPublic(el).Replace("\r\n", "\n").Trim();
		}
		catch { return ""; }
	}

	/// <summary>
	/// Emits the Notes: section for command/namespace help.
	/// Single-line remarks are inlined on the same line as "Notes:";
	/// multi-line remarks follow on the next line with 2-space indent to align with Commands/Options above.
	/// </summary>
	private static void EmitNotesSection(CodeWriter w, string? innerXml, string plainRendered)
	{
		var flat = string.IsNullOrWhiteSpace(plainRendered)
			? FlattenRemarksXml(innerXml)
			: plainRendered.Replace("\r\n", "\n").Trim();
		if (string.IsNullOrWhiteSpace(flat))
			return;

		if (!flat.Contains('\n'))
		{
			w.Line($"Console.Out.WriteLine(CliHelpFormatting.Section(\"Notes:\") + \"  \" + CliHelpFormatting.DocRemarksLine({Quote(flat)}));");
			return;
		}

		w.Line(HelpSection("Notes:"));
		if (!string.IsNullOrWhiteSpace(innerXml))
		{
			w.Line(WriteIndentedDocCall("  ", innerXml!, remarks: true));
			return;
		}

		foreach (var line in PlainDocLines(flat))
			w.Line(line.Length == 0 ? HelpBlankLine : $"Console.Out.WriteLine(\"  \" + CliHelpFormatting.DocRemarksLine({Quote(line)}));");
	}

	private static void EmitRootCommandHelpOverview(
		CodeWriter w,
		CommandModel rootCmd,
		AppEmitModel app,
		string entryAssemblyName,
		bool includeScopedDefaultHelpOptions = true)
	{
		// "(default command)" is not an argv token — labels the opt-in default handler; summary/remarks from XML on the handler.
		w.Line("Console.Out.WriteLine(\" \" + CliHelpFormatting.DefaultCommandLabel(\"(default command)\"));");
		EmitHelpDocLines(w, rootCmd.SummaryInnerXml, rootCmd.SummaryOneLiner, remarks: false, styled: false);
		var remarksXml = TransformRemarksInnerXmlForHelp(rootCmd.RemarksInnerXml, rootCmd, app.AllCommands, entryAssemblyName);
		EmitHelpDocLines(w, remarksXml, rootCmd.RemarksRendered, remarks: true, styled: false);
		var rootFlags = rootCmd.Parameters.Where(static p => p.Kind == ParameterKind.Flag).ToList();
		if (includeScopedDefaultHelpOptions && rootFlags.Count > 0)
		{
			var mw = Math.Min(
				Math.Max(rootFlags.Max(p => HelpLayout.FormatOptionLeftCell(p).Length), "-h, --help".Length),
				40);
			w.Line(HelpBlankLine);
			w.Line(HelpSection("Options for this default:"));
			foreach (var p in rootFlags)
			{
				var desc = BuildDescriptionSuffix(p, forPositional: false, app.EnvironmentPrefix);
				w.Lines(HelpRowStatements("Out", HelpNameCell("Accent", HelpLayout.FormatOptionLeftCell(p).PadRight(mw)), desc, null, null));
			}
		}

		var rootPositionals = rootCmd.Parameters.Where(static p => p.Kind == ParameterKind.Positional).ToList();
		if (includeScopedDefaultHelpOptions && rootPositionals.Count > 0)
		{
			w.Line(HelpBlankLine);
			w.Line(HelpSection("Arguments for this default:"));
			EmitPositionalHelpRows(w, rootPositionals);
		}

		w.Line(HelpBlankLine);
	}

	private static void EmitRootAliasHelpOverview(CodeWriter w, CommandModel aliasCmd, ImmutableArray<string> path, string entryAssemblyName)
	{
		var fullCommandPath = path.IsDefaultOrEmpty
			? $"{entryAssemblyName} {aliasCmd.CommandName}"
			: $"{entryAssemblyName} {string.Join(" ", path)} {aliasCmd.CommandName}";
		w.Line($"Console.Out.WriteLine(\" \" + CliHelpFormatting.DefaultCommandLabel({Quote($"(default: {aliasCmd.CommandName})")}));");
		if (!string.IsNullOrWhiteSpace(aliasCmd.SummaryOneLiner))
			w.Line($"Console.Out.WriteLine({Quote("    " + aliasCmd.SummaryOneLiner)});");
		w.Line($"Console.Out.WriteLine({Quote($"    Alias for '{fullCommandPath}'. Run '{fullCommandPath} --help' for details.")});");
		w.Line(HelpBlankLine);
	}

	/// <summary>Space-separated CLI path for help listings (e.g. <c>storage blob upload</c>).</summary>
	private static string FormatQualifiedCliPath(ImmutableArray<string> prefix, string segment)
	{
		if (prefix.IsDefaultOrEmpty)
			return segment;
		return string.Join(" ", prefix) + " " + segment;
	}

	private static void EmitArghGeneratedRouteArgsMethod(CodeWriter w)
	{
		using (w.Block("public static RouteMatch? Route(string[] args)"))
		{
			w.If("args is null", "throw new ArgumentNullException(nameof(args));");
			w.If("!TryParseRoute(args, out var m)", "return null;");
			w.Return("m");
		}

		w.Line();
	}


	private static void EmitIsIntrinsicCommand(CodeWriter w, AppEmitModel app)
	{
		var intrinsicCommands = app.AllCommands.Where(static c => c.IsIntrinsic).ToList();
		using (w.Block("internal static bool IsIntrinsicCommand(string[] args)"))
		{
			w.Line("if (args is null || args.Length == 0) return false;");
			if (intrinsicCommands.Count == 0)
				w.Return("false");
			else
			{
				w.Line("var match = Route(args);");
				w.Line("if (match is null) return false;");
				using (w.Switch("match.Value.CommandPath"))
				{
					foreach (var cmd in intrinsicCommands)
						w.Line($"case {Quote(GetCommandRoutePath(cmd))}: return true;");
					w.Line("default: return false;");
				}
			}
		}

		w.Line();
	}

	/// <summary><c>match = new RouteMatch(…); return true;</c> for a command that takes the rest of the arguments.</summary>
	private static void EmitRouteMatch(CodeWriter w, CommandModel cmd)
	{
		w.Line($"match = new RouteMatch({Quote(GetCommandRoutePath(cmd))}, TailFrom(args, idx[0]));");
		w.Return("true");
	}

	private static void EmitTryParseRouteHierarchical(CodeWriter w, AppEmitModel app)
	{
		var hasLeadingOptionPrefetch =
			CollectRootPrefetchGlobalMembers(app).Length > 0 || CollectDeferredRootAliasPrefetchFlags(app).Length > 0;
		using (w.Block("public static bool TryParseRoute(string[] args, out RouteMatch match)"))
		{
			w.Line("match = default;");
			w.Line(CheckNameScopes);
			w.Line("if (CompletionProtocol.IsArghMetaCompletionInvocation(args)) return false;");
			w.Line("var idx = new int[1];");
			w.Line($"{OptionFlagsSeenField}.Clear();");
			if (hasLeadingOptionPrefetch)
				w.Line("if (!TryParseGlobalOptions(args, idx)) return false;");
			w.Line("if (args.Length == 0) return false;");
			w.Line("if (idx[0] < args.Length && (ArghNames.IsHelp(args[idx[0]]) || ArghNames.IsVersion(args[idx[0]]))) return false;");
			using (w.If("idx[0] >= args.Length"))
			{
				if ((app.Root.RootCommand ?? app.Root.RootAlias) is { } routeRoot)
					EmitRouteMatch(w, routeRoot);
				else
					w.Return("false");
			}

			w.Return("TryParseRouteRoot(args, idx, out match)");
		}

		w.Line();
		EmitTryParseRouteForNode(w, app, app.Root, ImmutableArray<string>.Empty, "TryParseRouteRoot", isRoot: true);
		EmitArghGeneratedRouteArgsMethod(w);
	}

	private static void EmitTryParseRouteForNode(
		CodeWriter w,
		AppEmitModel app,
		RegistryNode node,
		ImmutableArray<string> path,
		string methodName,
		bool isRoot)
	{
		using (w.Block($"private static bool {methodName}(string[] args, int[] idx, out RouteMatch match)"))
		{
			w.Line("match = default;");
			if (!isRoot && node.CommandNamespaceOptionsModel is { FlattenedMembers.Length: > 0 })
				w.Line($"if (!{CommandNamespaceOptionsParseMethodName(path)}(args, idx)) return false;");
			using (w.If("idx[0] >= args.Length"))
			{
				if ((node.RootCommand ?? node.RootAlias) is { } routeNsRoot)
					EmitRouteMatch(w, routeNsRoot);
				else
					w.Return("false");
			}

			w.Line("if (ArghNames.IsHelp(args[idx[0]])) return false;");
			EmitCommandNameSwitch(w, node, path, "args[idx[0]]", cmd =>
			{
				w.Line("idx[0]++;");
				EmitRouteMatch(w, cmd);
			}, childPath =>
			{
				w.Line("idx[0]++;");
				w.Return($"TryParseRouteCommandNamespace_{CommandNamespacePathKey(childPath)}(args, idx, out match)");
			});

			// The default command takes what the dispatcher gives it.
			if ((node.RootCommand ?? node.RootAlias) is { } defaultCommand)
			{
				if (DefaultCommandTakesTokenCondition(defaultCommand, "args[idx[0]]") is { } takesToken)
				{
					using (w.If(takesToken))
						EmitRouteMatch(w, defaultCommand);
					w.Return("false");
				}
				else
					EmitRouteMatch(w, defaultCommand);
			}
			else
				w.Return("false");
		}

		w.Line();
		foreach (var ch in node.Children)
		{
			var childPath = AppendSegment(path, ch.Segment);
			EmitTryParseRouteForNode(w, app, ch.Node, childPath, "TryParseRouteCommandNamespace_" + CommandNamespacePathKey(childPath), isRoot: false);
		}
	}

	private static ImmutableArray<ParameterModel> CollectRootPrefetchGlobalMembers(AppEmitModel app) =>
		app.GlobalOptionsModel?.FlattenedMembers ?? ImmutableArray<ParameterModel>.Empty;

	/// <summary>
	/// Union of prefetch parse sets (flattened globals + flattened root-alias-command flags).
	/// Used to decide which leading flags can be peeled in <see cref="TryParseGlobalOptions"/> vs deferred to dispatch.
	/// </summary>
	private static ImmutableArray<ParameterModel> MergeRootPrefetchPredicateMembers(AppEmitModel app)
	{
		var globalFlattened = app.GlobalOptionsModel?.FlattenedMembers ?? ImmutableArray<ParameterModel>.Empty;
		if (app.Root.RootAlias is not { } aliasCmd || aliasCmd.Parameters.IsDefaultOrEmpty)
			return globalFlattened;

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var m in globalFlattened)
			if (ParticipatesInOptionPrefetch(m.Kind))
				seen.Add(m.CliLongName);

		var trailing = ImmutableArray.CreateBuilder<ParameterModel>();
		foreach (var p in aliasCmd.Parameters)
		{
			if (!ParticipatesInOptionPrefetch(p.Kind))
				continue;

			if (!seen.Add(p.CliLongName))
				continue;

			trailing.Add(p);
		}

		if (trailing.Count == 0)
			return globalFlattened;

		var merged = ImmutableArray.CreateBuilder<ParameterModel>(globalFlattened.Length + trailing.Count);
		merged.AddRange(globalFlattened);
		merged.AddRange(trailing);

		return merged.ToImmutable();
	}

	/// <summary>
	/// Root-alias-command flags minus anything already declared on <see cref="AppEmitModel.GlobalOptionsModel"/>.
	/// When these appear as leading <c>-</c>-prefixed tokens, <see cref="TryParseGlobalOptions"/> must defer them
	/// (break without consuming idx) so <c>DispatchRoot</c> can route them to <see cref="RegistryNode.RootAlias"/>.
	/// </summary>
	private static ImmutableArray<ParameterModel> CollectDeferredRootAliasPrefetchFlags(AppEmitModel app)
	{
		var merged = MergeRootPrefetchPredicateMembers(app);
		var globals = CollectRootPrefetchGlobalMembers(app);
		if (app.Root.RootAlias is null || merged.Length <= globals.Length)
			return ImmutableArray<ParameterModel>.Empty;

		var defer = ImmutableArray.CreateBuilder<ParameterModel>();
		for (var i = globals.Length; i < merged.Length; i++)
		{
			var p = merged[i];
			if (ParticipatesInOptionPrefetch(p.Kind))
				defer.Add(p);
		}

		return defer.Count == 0 ? ImmutableArray<ParameterModel>.Empty : defer.ToImmutable();
	}

	private static bool ParticipatesInOptionPrefetch(ParameterKind kind) =>
		kind == ParameterKind.Flag || kind == ParameterKind.OptionsInjected;

	private static CommandModel SyntheticOptionsCommand(ImmutableArray<ParameterModel> members, string runMethodName) =>
		new(
			ImmutableArray<string>.Empty,
			"__opt__",
			runMethodName,
			"object",
			"__noop",
			false,
			false,
			"global::System.Void",
			false,
			true,
			members,
			false,
			ImmutableArray<HandlerParam>.Empty,
			SourceSpanInfo.None,
			ImmutableArray<(string, string)>.Empty,
			"",   // HandlerDocCommentId
			"",   // SummaryOneLiner
			"",   // RemarksRendered
			"",   // SummaryInnerXml
			"",   // RemarksInnerXml
			"",   // ExamplesRendered
			"",   // UsageHints
			ImmutableArray<(string, bool)>.Empty);


	private static bool HelpUsesEnumChoiceContinuationLayout(ParameterModel p) =>
		p.ScalarKind == CliScalarKind.Enum
		|| (p.IsCollection && p.ElementScalarKind == CliScalarKind.Enum && !p.ElementEnumMemberNames.IsDefaultOrEmpty);

	/// <summary>The start of a help row as interpolated-string text: two spaces, the styled name cell, two spaces.</summary>
	private static string HelpNameCell(string style, string paddedName) => $"  {{CliHelpFormatting.{style}({Quote(paddedName)})}}  ";

	/// <summary>
	/// Statements writing one help row to <c>Console.{stream}</c>: <paramref name="nameCell"/>, the description, then the
	/// validation summary in gray. With a <paramref name="continuationIndent"/> the validation summary goes on a line of its own.
	/// </summary>
	private static IEnumerable<string> HelpRowStatements(string stream, string nameCell, string desc, string? validationLine, string? continuationIndent)
	{
		string WriteLine(string interpolated) => $"Console.{stream}.WriteLine($\"{interpolated}\");";

		var text = EscapeForHelpInterpolation(desc);
		if (validationLine is null)
		{
			yield return WriteLine(nameCell + text);
			yield break;
		}

		var remarks = $"{{CliHelpFormatting.DocRemarksLine({HelpTextExpr(validationLine)})}}";
		if (continuationIndent is not null)
		{
			yield return WriteLine(nameCell + text);
			yield return WriteLine(continuationIndent + remarks);
		}
		else if (desc.Length == 0)
			yield return WriteLine(nameCell + remarks);
		else
			yield return WriteLine(nameCell + text + " " + remarks);
	}

	/// <summary>A flag's help row, as <c>--help</c> lists it; enum choices continue on the next line.</summary>
	private static IEnumerable<string> HelpOptionRowStatements(string stream, ParameterModel p, int maxOptWidth, string? environmentPrefix)
	{
		var validationLine = BuildValidationLine(p);
		var continuationIndent = validationLine != null && HelpUsesEnumChoiceContinuationLayout(p) ? new string(' ', maxOptWidth + 4) : null;
		return HelpRowStatements(
			stream,
			HelpNameCell("Accent", HelpLayout.FormatOptionLeftCell(p).PadRight(maxOptWidth)),
			BuildDescriptionSuffix(p, forPositional: false, environmentPrefix),
			validationLine,
			continuationIndent);
	}

	private static void EmitHelpOptionRows(CodeWriter w, IReadOnlyList<ParameterModel> rows, int maxOptWidth, string? environmentPrefix = null)
	{
		foreach (var p in rows)
			w.Lines(HelpOptionRowStatements("Out", p, maxOptWidth, environmentPrefix));
	}

	/// <summary><c>switch (canonFlagName)</c> printing one flag's help row to stderr after a parse error.</summary>
	private static void EmitFlagHelpSwitch(CodeWriter w, IEnumerable<ParameterModel> flags, int maxOptWidth, string? environmentPrefix)
	{
		using (w.Switch("canonFlagName"))
		{
			foreach (var p in flags.OrderBy(static x => x.CliLongName, StringComparer.OrdinalIgnoreCase))
			{
				using (w.Case(Quote(p.CliLongName)))
				{
					w.Lines(HelpOptionRowStatements("Error", p, maxOptWidth, environmentPrefix));
					w.Line("break;");
				}
			}

			w.Case("default", "break;");
		}
	}

	private static void EmitOptionsTryParseFlagHelpPrinter(
		CodeWriter w,
		string parseMethodName,
		List<ParameterModel> flagMembers,
		int maxOptWidth)
	{
		if (flagMembers.Count == 0)
			return;

		using (w.Block($"private static void {parseMethodName}_FlagHelp_ToStdErr(string canonFlagName)"))
			EmitFlagHelpSwitch(w, flagMembers, maxOptWidth, null);
		w.Line();
	}

	/// <summary><c>Console.Error.WriteLine("…");</c> for text already escaped for a C# string literal, such as a run hint.</summary>
	private static string ErrorLine(string escapedText) => $"Console.Error.WriteLine(\"{escapedText}\");";

	/// <summary>
	/// How generated binding code reports a value it can't use: the statement that leaves (<c>return 2</c>, or
	/// <c>return false</c> for a DTO parser), then the command's help printers and run hint, where it has them.
	/// <paramref name="SourceNote"/> is a statement that follows the error, such as naming the environment variable a value came from.
	/// </summary>
	private sealed record ParseFailure(string Exit, string? HelpMethod = null, string? FlagHelpMethod = null, string? RunHint = null, string? SourceNote = null);

	/// <summary>
	/// <paramref name="failure"/> naming <paramref name="envVar"/> after an invalid value when <paramref name="fromEnvironment"/>
	/// (a C# condition) says the value came from it; <paramref name="failure"/> itself without an environment variable.
	/// </summary>
	private static ParseFailure WithEnvironmentSource(ParseFailure failure, string? envVar, string fromEnvironment) =>
		envVar is null
			? failure
			: failure with { SourceNote = $"if ({fromEnvironment}) Console.Error.WriteLine({Quote($"The value comes from the environment variable {envVar}.")});" };

	/// <summary>After a CLI parse/validation error on stderr: optional flag rows (matching --help), then optional run hint.</summary>
	private static void EmitAfterCliParseErrorHelp(CodeWriter w, ParameterModel p, ParseFailure failure)
	{
		if (p.Kind == ParameterKind.Flag && failure.FlagHelpMethod is not null)
		{
			w.Line("Console.Error.WriteLine();");
			w.Line($"{failure.FlagHelpMethod}({Quote(p.CliLongName)});");
			w.Line("Console.Error.WriteLine();");
			if (failure.RunHint is not null)
				w.Line(ErrorLine(failure.RunHint));
		}
		else if (failure.HelpMethod is not null)
			w.Line($"{failure.HelpMethod}();");
	}

	/// <summary><c>if (condition) { error; help; exit; }</c>: rejects a value the parameter can't take.</summary>
	private static void EmitParseError(CodeWriter w, string condition, string errorStatement, ParameterModel p, ParseFailure failure)
	{
		using (w.If(condition))
		{
			w.Line(errorStatement);
			if (failure.SourceNote is not null)
				w.Line(failure.SourceNote);
			EmitAfterCliParseErrorHelp(w, p, failure);
			w.Line(failure.Exit + ";");
		}
	}

	/// <summary>A value validation checks run on: the parameter, the local holding it, and how a failure exits and what it prints.</summary>
	private sealed record ValidationTarget(ParameterModel P, string VarName, string FailureExit, string? FlagHelpMethod, string? RunHint)
	{
		public string CliName => P.CliLongName;
	}

	/// <summary>After a validation-check error: optional flag rows on stderr, then optional run hint.</summary>
	private static void EmitValidationErrorFooter(CodeWriter w, ValidationTarget t)
	{
		if (t.P.Kind == ParameterKind.Flag && t.FlagHelpMethod is not null)
		{
			w.Line("Console.Error.WriteLine();");
			w.Line($"{t.FlagHelpMethod}({Quote(t.CliName)});");
			w.Line("Console.Error.WriteLine();");
		}

		if (t.RunHint is not null)
			w.Line(ErrorLine(t.RunHint));
	}

	/// <summary><c>if (condition) { error; help; exit; }</c>: rejects a value that fails a validation check.</summary>
	private static void EmitValidationFailure(CodeWriter w, ValidationTarget t, string condition, string errorStatement)
	{
		using (w.If(condition))
		{
			w.Line(errorStatement);
			EmitValidationErrorFooter(w, t);
			w.Line(t.FailureExit + ";");
		}
	}

	/// <summary>Flags in each namespace's options on <paramref name="path"/> that neither the globals nor an outer namespace already show.</summary>
	private static List<(string Segment, List<ParameterModel> Rows)> BuildNamespaceOptionSections(
		AppEmitModel app, ImmutableArray<string> path, List<ParameterModel> globalFlagMembers)
	{
		var sections = new List<(string Segment, List<ParameterModel> Rows)>();
		var suppressedForNamespaceDisplay = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		AddCliKeys(globalFlagMembers, suppressedForNamespaceDisplay);
		foreach ((var seg, var gom) in GetCommandNamespaceOptionChain(app, path))
		{
			var allInNamespace = EnumerateFlagMembers(gom).ToList();
			var rows = allInNamespace.Where(p => !suppressedForNamespaceDisplay.Contains(p.CliLongName)).ToList();
			AddCliKeys(allInNamespace, suppressedForNamespaceDisplay);
			if (rows.Count > 0)
				sections.Add((seg, rows));
		}

		return sections;
	}

	/// <summary>What a command's help lists: global flags, each namespace's own flags, the command's own flags, and the option column width.</summary>
	private sealed record CommandHelpLayout(
		List<ParameterModel> GlobalFlags,
		List<(string Segment, List<ParameterModel> Rows)> NamespaceSections,
		List<ParameterModel> CommandFlags,
		int MaxOptWidth);

	private static CommandHelpLayout BuildCommandHelpLayout(CommandModel cmd, AppEmitModel app)
	{
		var globalFlagMembers = EnumerateFlagMembers(app.GlobalOptionsModel).ToList();
		var namespaceOptionSections = BuildNamespaceOptionSections(app, cmd.RoutePrefix, globalFlagMembers);

		var scopedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		AddCliKeys(globalFlagMembers, scopedKeys);
		foreach ((_, var gom) in GetCommandNamespaceOptionChain(app, cmd.RoutePrefix))
			AddCliKeys(EnumerateFlagMembers(gom), scopedKeys);

		var commandOnlyFlags = cmd.Parameters
			.Where(p => p.Kind == ParameterKind.Flag && !CommandFlagMatchesScopedKeys(p, scopedKeys))
			.ToList();

		var widthCandidates = new List<int> { "-h, --help".Length };
		widthCandidates.AddRange(globalFlagMembers.Select(p => HelpLayout.FormatOptionLeftCell(p).Length));
		foreach ((_, var rows) in namespaceOptionSections)
			widthCandidates.AddRange(rows.Select(p => HelpLayout.FormatOptionLeftCell(p).Length));

		widthCandidates.AddRange(commandOnlyFlags.Select(p => HelpLayout.FormatOptionLeftCell(p).Length));
		var maxOptWidth = Math.Min(widthCandidates.Max(), 40);
		maxOptWidth = Math.Max(maxOptWidth, "-h, --help".Length);
		return new CommandHelpLayout(globalFlagMembers, namespaceOptionSections, commandOnlyFlags, maxOptWidth);
	}

	private static string PositionalNameCell(ParameterModel p) =>
		p.IsVariadic
			? (p.IsRequired ? $"<{p.CliLongName}...>" : $"[<{p.CliLongName}...>]")
			: (p.IsRequired ? $"<{p.CliLongName}>" : $"[<{p.CliLongName}>]");

	/// <summary>The rows of an arguments section, names padded to the longest.</summary>
	private static void EmitPositionalHelpRows(CodeWriter w, IReadOnlyList<ParameterModel> positionals)
	{
		var maxArgWidth = Math.Min(positionals.Max(p => PositionalNameCell(p).Length), 40);
		foreach (var p in positionals)
		{
			var nameCell = HelpNameCell("Placeholder", PositionalNameCell(p).PadRight(maxArgWidth));
			w.Lines(HelpRowStatements("Out", nameCell, BuildDescriptionSuffix(p, forPositional: true), BuildValidationLine(p), null));
		}
	}

	/// <summary>
	/// The usage line of the app or a namespace (<paramref name="path"/>): <c>&lt;command&gt;</c>, and a second line for a
	/// default command that takes positionals, which it gets when no command matches.
	/// </summary>
	private static void EmitScopeUsageLines(CodeWriter w, string entryAssemblyName, ImmutableArray<string> path, string commandTail, CommandModel? defaultCommand)
	{
		var prefix = path.IsDefaultOrEmpty ? "" : " " + string.Join(" ", path);
		w.Line(HelpUsageLine(entryAssemblyName, prefix + commandTail));
		if (defaultCommand is null || !defaultCommand.Parameters.Any(static p => p.Kind == ParameterKind.Positional))
			return;
		var indent = new string(' ', "Usage: ".Length);
		w.Line($"Console.Out.WriteLine({Quote(indent)} + CliHelpFormatting.Accent({Quote(entryAssemblyName)}) + {Quote((prefix + " " + defaultCommand.UsageHints).TrimEnd())});");
	}

	private static void EmitCommandHelpPrinter(CodeWriter w, CommandModel cmd, AppEmitModel app, string entryAssemblyName)
	{
		if (cmd.IsRootDefault)
			return;

		var routeUsage = cmd.RoutePrefix.IsDefaultOrEmpty
			? ""
			: string.Join(" ", cmd.RoutePrefix) + " ";
		var layout = BuildCommandHelpLayout(cmd, app);
		var maxOptWidth = layout.MaxOptWidth;
		var positionals = cmd.Parameters.Where(static p => p.Kind == ParameterKind.Positional).ToList();

		using (w.Block($"private static void PrintHelp_{cmd.RunMethodName}()"))
		{
			w.Line(HelpUsageLine(entryAssemblyName, $" {routeUsage}{cmd.CommandName} {cmd.UsageHints}"));
			w.Line(HelpBlankLine);

			EmitHelpDocLines(w, cmd.SummaryInnerXml, cmd.SummaryOneLiner, remarks: false);
			if (!string.IsNullOrWhiteSpace(cmd.SummaryOneLiner) || !string.IsNullOrWhiteSpace(cmd.SummaryInnerXml))
				w.Line(HelpBlankLine);

			if (positionals.Count > 0)
			{
				w.Line(HelpSection("Arguments:"));
				EmitPositionalHelpRows(w, positionals);
				w.Line(HelpBlankLine);
			}

			w.Line(HelpSection("Global options:"));
			w.Line(HelpBuiltInRow("-h, --help", maxOptWidth, "Show help."));
			if (layout.GlobalFlags.Count > 0)
				EmitHelpOptionRows(w, layout.GlobalFlags, maxOptWidth, app.EnvironmentPrefix);

			w.Line(HelpBlankLine);

			foreach ((var segment, var gRows) in layout.NamespaceSections)
			{
				w.Line(HelpSection($"'{segment}' options:"));
				EmitHelpOptionRows(w, gRows, maxOptWidth, app.EnvironmentPrefix);
				w.Line(HelpBlankLine);
			}

			if (layout.CommandFlags.Count > 0)
			{
				w.Line(HelpSection("Options:"));
				EmitHelpOptionRows(w, layout.CommandFlags, maxOptWidth, app.EnvironmentPrefix);
			}

			var remarksXml = TransformRemarksInnerXmlForHelp(cmd.RemarksInnerXml, cmd, app.AllCommands, entryAssemblyName);
			if (!string.IsNullOrWhiteSpace(cmd.RemarksRendered) || !string.IsNullOrWhiteSpace(remarksXml))
			{
				w.Line(HelpBlankLine);
				EmitNotesSection(w, remarksXml, cmd.RemarksRendered);
			}

			if (!string.IsNullOrWhiteSpace(cmd.ExamplesRendered))
			{
				w.Line(HelpBlankLine);
				w.Line(HelpSection("Examples:"));
				foreach (var line in cmd.ExamplesRendered.Split('\n'))
				{
					var trimmed = line.TrimEnd('\r');
					w.Line(trimmed.Length == 0 ? HelpBlankLine : $"Console.Out.WriteLine({Quote("  " + trimmed)});");
				}
			}
		}

		w.Line();
	}

	private static void EmitCommandFlagHelpToStdErrMethod(CodeWriter w, CommandModel cmd, AppEmitModel app)
	{
		if (cmd.IsRootDefault)
			return;

		var layout = BuildCommandHelpLayout(cmd, app);
		var byCanon = new Dictionary<string, ParameterModel>(StringComparer.OrdinalIgnoreCase);
		foreach (var p in layout.GlobalFlags)
			byCanon[p.CliLongName] = p;
		foreach ((_, var rows) in layout.NamespaceSections)
			foreach (var p in rows)
				byCanon[p.CliLongName] = p;
		foreach (var p in layout.CommandFlags)
			byCanon[p.CliLongName] = p;

		using (w.Block($"private static void PrintHelp_{cmd.RunMethodName}_Flag_ToStdErr(string canonFlagName)"))
		{
			if (byCanon.Count > 0)
				EmitFlagHelpSwitch(w, byCanon.Values, layout.MaxOptWidth, app.EnvironmentPrefix);
		}

		w.Line();
	}

	private static string BuildDescriptionSuffix(ParameterModel p, bool forPositional, string? environmentPrefix = null)
	{
		var parts = new List<string>();

		if (!forPositional && p.Kind == ParameterKind.Flag && p.Special == BoolSpecialKind.None && p.IsRequired)
			parts.Add("[required]");

		if (!forPositional && p is { IsCollection: true, Kind: ParameterKind.Flag })
			parts.Add(p.CollectionSeparator is null ? "[repeatable]" : "[separated]");

		if (forPositional && p.IsVariadic)
			parts.Add("[variadic]");

		if (!string.IsNullOrWhiteSpace(p.Description))
			parts.Add(p.Description.Trim());

		if (p.Special == BoolSpecialKind.None)
		{
			if (HasShownDefault(p))
				parts.Add($"[default: {FormatDefaultForHelpText(p)}]");
		}
		// A switch is off unless given, so only a bool that starts on (or a bool? that starts set) shows its default.
		else if (HasShownDefault(p) && (p.Special == BoolSpecialKind.NullableBool || p.DefaultValueLiteral!.Trim() == "true"))
			parts.Add($"[default: {p.DefaultValueLiteral!.Trim()}]");

		var envVarName = ComputeEffectiveEnvVarName(p, environmentPrefix);
		if (!string.IsNullOrEmpty(envVarName))
			parts.Add($"[env: {envVarName}]");

		return string.Join(" ", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
	}


	/// <summary>
	/// Whether help and <c>__schema</c> show a default: a <c>null</c> or <c>default</c> default (such as
	/// <c>int? n = null</c> or <c>TimeSpan t = default</c>) means "not given", so neither shows it.
	/// </summary>
	private static bool HasShownDefault(ParameterModel p) =>
		p.DefaultValueLiteral?.Trim() is { } literal && literal is not ("default" or "null");

	private const string RuntimeDefaultMarkerStart = "<<argh-default:";
	private const string RuntimeDefaultMarkerEnd = ">>";

	/// <summary>
	/// The default value as help shows it. A numeric default is formatted at runtime with the parse culture, so help shows
	/// it the way users type it (see <see cref="RuntimeCultureText"/>). Other defaults, and <c>__schema</c>, use the
	/// invariant text from <see cref="FormatDefaultForHelp"/>.
	/// </summary>
	private static string FormatDefaultForHelpText(ParameterModel p)
	{
		var type = p.TypeName.TrimEnd('?');
		var literal = p.DefaultValueLiteral?.Trim();
		if (literal is not null and not "default" and not "null"
		    && type is "int" or "long" or "float" or "double" or "decimal" or "short" or "ushort" or "uint" or "ulong" or "byte" or "sbyte")
			return RuntimeCultureText(type, literal);
		return FormatDefaultForHelp(p);
	}

	/// <summary>
	/// A numeric constant (<paramref name="type"/> keyword and C# <paramref name="literal"/>) in help or error text, formatted
	/// at runtime with the parse culture so users see it the way they type it. Returns a marker that
	/// <see cref="ExpandRuntimeDefaults"/> turns into an interpolation hole, so the text must go through
	/// <see cref="EscapeForHelpInterpolation"/> into an interpolated string.
	/// </summary>
	private static string RuntimeCultureText(string type, string literal) =>
		RuntimeDefaultMarkerStart + type + ":" + literal + RuntimeDefaultMarkerEnd;

	/// <summary>
	/// A C# string expression for help text: an interpolated string when the text holds <see cref="RuntimeCultureText"/>
	/// markers, a plain literal otherwise.
	/// </summary>
	private static string HelpTextExpr(string text) =>
		text.IndexOf(RuntimeDefaultMarkerStart, StringComparison.Ordinal) < 0
			? "\"" + Escape(text) + "\""
			: "$\"" + EscapeForHelpInterpolation(text) + "\"";

	/// <summary>Replaces each <see cref="RuntimeCultureText"/> marker in escaped help text with a hole that formats the value at runtime.</summary>
	private static string ExpandRuntimeDefaults(string escaped) =>
		escaped.IndexOf(RuntimeDefaultMarkerStart, StringComparison.Ordinal) < 0
			? escaped
			: System.Text.RegularExpressions.Regex.Replace(
				escaped,
				"<<argh-default:([a-z]+):([^>]*)>>",
				m => "{((" + m.Groups[1].Value + ")(" + m.Groups[2].Value + ")).ToString(" + ParseCulture + ")}");

	/// <summary>The character a C# char literal (<c>'x'</c>, <c>'\''</c>, <c>'\u0001'</c>) stands for, or <see langword="null"/>.</summary>
	private static string? CharLiteralText(string literal)
	{
		if (literal.Length < 3 || literal[0] != '\'' || literal[literal.Length - 1] != '\'')
			return null;
		var inner = literal.Substring(1, literal.Length - 2);
		if (inner.Length == 1)
			return inner;
		if (inner.Length == 6 && inner.StartsWith("\\u", StringComparison.Ordinal)
		    && int.TryParse(inner.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
			return ((char)code).ToString();
		return inner switch
		{
			"\\'" => "'",
			"\\\"" => "\"",
			"\\\\" => "\\",
			"\\0" => "\0",
			"\\a" => "\a",
			"\\b" => "\b",
			"\\f" => "\f",
			"\\n" => "\n",
			"\\r" => "\r",
			"\\t" => "\t",
			"\\v" => "\v",
			_ => null,
		};
	}

	private static string FormatDefaultForHelp(ParameterModel p)
	{
		if (p.DefaultValueLiteral is null)
			return "";

		if (p.ScalarKind == CliScalarKind.Enum && !p.EnumMemberNames.IsDefaultOrEmpty)
		{
			var lit = p.DefaultValueLiteral.Trim();
			for (var i = 0; i < p.EnumMemberNames.Length; i++)
			{
				var member = p.EnumMemberNames[i];
				if (string.Equals(lit, member, StringComparison.Ordinal) || lit.EndsWith("." + member, StringComparison.Ordinal))
					return ResolveEnumMemberCliName(p.EnumMemberCliNames, i, member);
			}

			// A value with no member of its own (a [Flags] combination) is a cast, (global::Level)(7): show the number.
			var castPrefix = "(" + p.EnumTypeFq + ")(";
			if (p.EnumTypeFq is not null && lit.StartsWith(castPrefix, StringComparison.Ordinal) && lit.EndsWith(")", StringComparison.Ordinal))
				return NumericLiteralText(lit.Substring(castPrefix.Length, lit.Length - castPrefix.Length - 1));
		}

		var defaultLiteral = p.DefaultValueLiteral.Trim();
		return p.TypeName switch
		{
			"string" => defaultLiteral.Trim('"'),
			"char" or "char?" when CharLiteralText(defaultLiteral) is { } ch => ch,
			// Numeric literals carry a suffix or cast (1.5d, (short)5); help and __schema show the plain invariant number.
			_ => NumericLiteralText(defaultLiteral)
		};
	}


	private static class UsageSynopsis
	{
		/// <summary>Minimal usage tail: required flags and positionals explicitly; optional switches and flags fold into a single <c>[options]</c>.</summary>
		public static string Build(ImmutableArray<ParameterModel> parameters)
		{
			var parts = new List<string>();
			var needsOptions = false;

			foreach (var p in parameters)
			{
				if (p.Kind == ParameterKind.Injected || p.Kind == ParameterKind.OptionsInjected)
					continue;

				if (p.Kind == ParameterKind.Positional)
				{
					string seg;
					if (p.IsVariadic)
						seg = p.IsRequired ? $"<{p.CliLongName}...>" : $"[<{p.CliLongName}...>]";
					else
						seg = p.IsRequired ? $"<{p.CliLongName}>" : $"[<{p.CliLongName}>]";
					parts.Add(seg);
					continue;
				}

				if (p.Kind != ParameterKind.Flag)
					continue;

				if (p.Special == BoolSpecialKind.Bool)
				{
					needsOptions = true;
					continue;
				}

				if (p.Special == BoolSpecialKind.NullableBool)
				{
					needsOptions = true;
					continue;
				}

				if (p.IsCollection)
				{
					if (p.IsRequired)
					{
						var typeHint = HelpLayout.TypeHint(p);
						parts.Add($"--{p.CliLongName} {typeHint}");
					}
					else
					{
						needsOptions = true;
					}

					continue;
				}

				var typeHintScalar = HelpLayout.TypeHint(p);
				if (p.IsRequired)
					parts.Add($"--{p.CliLongName} {typeHintScalar}");
				else
					needsOptions = true;
			}

			if (needsOptions)
				parts.Add("[options]");

			return string.Join(" ", parts);
		}
	}
}
