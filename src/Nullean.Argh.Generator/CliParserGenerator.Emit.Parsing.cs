using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Nullean.Argh;

public sealed partial class CliParserGenerator
{
	private const string UnknownLongOptionError = "Console.Error.WriteLine($\"Error: unknown option '--{flagName}'.\");";

	/// <summary>
	/// The body of a local <c>FailUnknownLongOption(flagName)</c>: the error, with the closest known flags when any are
	/// near (each with its help row when there's a <paramref name="flagHelpMethod"/>), then the run hint; it returns <paramref name="result"/>.
	/// </summary>
	private static void EmitUnknownLongOptionBody(CodeWriter w, string? flagHelpMethod, string? runHintStatement, string result)
	{
		void RunHint()
		{
			if (runHintStatement is null)
				return;
			w.Line("Console.Error.WriteLine();");
			w.Line(runHintStatement);
		}

		w.Line($"var __matches = FuzzyMatch.FindClosest(flagName, __flagFuzzyCands, {FuzzyMaxDistance});");
		using (w.If("__matches.Count == 0"))
		{
			w.Line(UnknownLongOptionError);
			RunHint();
			w.Return(result);
		}

		using (w.If("__matches.Count == 1"))
		{
			w.Line("var __m = __matches[0];");
			w.Line("Console.Error.WriteLine($\"Error: unknown option '--{flagName}'. Did you mean '--{__m}'?\");");
			if (flagHelpMethod is not null)
			{
				w.Line("Console.Error.WriteLine();");
				w.Line($"{flagHelpMethod}(__m);");
			}

			RunHint();
			w.Return(result);
		}

		w.Line("Console.Error.WriteLine($\"Error: unknown option '--{flagName}'. Did you mean one of these?\");");
		w.Line("Console.Error.WriteLine();");
		using (w.Foreach("var __m in __matches"))
		{
			if (flagHelpMethod is not null)
				w.Line($"{flagHelpMethod}(__m);");
		}

		RunHint();
		w.Return(result);
	}

	/// <summary>The fuzzy-match candidates for an unknown long option: each flag's long name once.</summary>
	private static string FlagFuzzyCandidates(IEnumerable<ParameterModel> flags) =>
		$"var __flagFuzzyCands = {InlineStringArray(flags.Select(static p => p.CliLongName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static x => x, StringComparer.OrdinalIgnoreCase))};";

	/// <summary>
	/// The static that records the options flags the leading options parsers saw, by long name with the value given, for
	/// the required checks and for <c>ArghTokenizer</c> to check a flag given again against.
	/// </summary>
	private const string OptionFlagsSeenField = "s_optionFlagsSeen";

	/// <summary>
	/// Whether the leading options parsers saw the member's flag (for a <c>bool?</c>, <c>--no-</c> too), so the static
	/// already holds the command-line value.
	/// </summary>
	private static string OptionFlagSeenCheck(ParameterModel m) => $"{OptionFlagsSeenField}.ContainsKey({Quote(m.CliLongName)})";

	/// <summary>
	/// For each member long name, <c>{static}.{member}</c> from the last of <paramref name="options"/> that declares it, so
	/// a later (more specific) options type wins over an earlier one.
	/// </summary>
	private static Dictionary<string, string> OptionsMemberStatics(IEnumerable<(string StaticFieldName, ImmutableArray<ParameterModel> Members)> options)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (staticField, members) in options)
		{
			foreach (var m in members)
				result[m.CliLongName] = staticField + "." + m.SymbolName;
		}

		return result;
	}

	private static void EmitOptionsTryParse(
		CodeWriter w,
		string methodName,
		ImmutableArray<ParameterModel> members,
		string? storeTypeFq = null,
		string? storeFieldName = null,
		ImmutableArray<string>? storeBestCtorParamOrder = null,
		string? entryAssemblyName = null,
		ImmutableArray<ParameterModel>? deferLeadingRootAliasFlags = null,
		IReadOnlyDictionary<string, string>? inheritedValues = null)
	{
		var defer = deferLeadingRootAliasFlags ?? ImmutableArray<ParameterModel>.Empty;

		var flagMembers = members.Where(static p => p.Kind == ParameterKind.Flag).ToList();
		var widthCandidates = new List<int> { "-h, --help".Length };
		widthCandidates.AddRange(flagMembers.Select(static p => HelpLayout.FormatOptionLeftCell(p).Length));
		var maxOptWidth = flagMembers.Count == 0
			? "-h, --help".Length
			: Math.Max(Math.Min(widthCandidates.Max(), 40), "-h, --help".Length);

		if (flagMembers.Count > 0)
			EmitOptionsTryParseFlagHelpPrinter(w, methodName, flagMembers, maxOptWidth);

		var flagHelpMethodName = methodName + "_FlagHelp_ToStdErr";
		var failure = new ParseFailure("return false",
			FlagHelpMethod: flagMembers.Count > 0 ? flagHelpMethodName : null,
			RunHint: string.IsNullOrEmpty(entryAssemblyName) ? null : $"Run '{Escape(entryAssemblyName!)} --help' for usage.");

		var flagSet = FlagSetFieldName(methodName);
		EmitFlagSetField(w, flagSet, flagMembers);
		var deferSet = "null";
		if (!defer.IsDefaultOrEmpty)
		{
			// The default command's own flags stop these options, for it to read them.
			deferSet = "__defer_" + methodName;
			EmitFlagSetField(w, deferSet, defer);
		}

		using (w.Block($"private static bool {methodName}(string[] args, int[] idx)"))
		{
			if (flagMembers.Count > 0)
			{
				w.Line(FlagFuzzyCandidates(flagMembers));
				using (w.Block("bool FailUnknownLongOption(string flagName)"))
					EmitUnknownLongOptionBody(w, flagHelpMethodName, failure.RunHint is null ? null : ErrorLine(failure.RunHint), "false");
				w.Line();
			}

			// Help, the version, and "--" or "-", which are arguments, are for the dispatcher.
			w.Line($"var __tokens = {ArghTokenizerFq}.ReadOptions(args, idx[0], {flagSet}, {deferSet}, {OptionFlagsSeenField});");
			w.Line("idx[0] = __tokens.Next;");
			EmitTokenErrorCheck(w, failure, flagMembers.Count > 0 ? "return FailUnknownLongOption(__error.Name);" : null);
			w.Line("var flags = __tokens.Flags;");
			if (storeTypeFq is not null && storeFieldName is not null && members.Length > 0)
				EmitOptionsConstructAndStore(w, storeTypeFq, members, storeFieldName, storeBestCtorParamOrder, inheritedValues);
			w.Return("true");
		}

		w.Line();
	}

	/// <summary>
	/// After <see cref="EmitOptionsTryParse"/> parses flags into a <c>flags</c> dict, extract member values and
	/// construct the options instance, then store it in <paramref name="storeFieldName"/>. A member whose flag isn't here keeps the value from
	/// <paramref name="inheritedValues"/> (keyed by long name), the enclosing options that already read it, or else its
	/// default. Required members are checked by the command, which also reads the options given after it.
	/// Injected just before <c>return true</c> of the parse method.
	/// </summary>
	private static void EmitOptionsConstructAndStore(
		CodeWriter w,
		string typeFq,
		ImmutableArray<ParameterModel> members,
		string storeFieldName,
		ImmutableArray<string>? bestCtorParamOrder,
		IReadOnlyDictionary<string, string>? inheritedValues)
	{
		var byName = members.ToDictionary(static m => m.SymbolName, StringComparer.OrdinalIgnoreCase);
		var failure = new ParseFailure("return false");

		// For cross-assembly options types the property initializer syntax is not readable.
		// Instantiate the type once to capture all C# runtime defaults.
		var hasRtDefaults = members.Any(static m => m.UsesRuntimeDefault);
		if (hasRtDefaults)
			w.Line($"var __rt_default = new {typeFq}();");

		// Extract each member's value from the flags dict.
		foreach (var m in members)
		{
			if (m.Kind != ParameterKind.Flag)
				continue;
			string? inherited = null;
			inheritedValues?.TryGetValue(m.CliLongName, out inherited);
			// Declare the local variable first (EmitParseAndAssign only assigns, does not declare).
			// For cross-assembly runtime-default properties, seed from the pre-created instance.
			var initializer = inherited
				?? (m.UsesRuntimeDefault && hasRtDefaults ? $"__rt_default.{m.SymbolName}" : GetCliInitializer(m));
			w.Line($"{GetCSharpCliType(m)} {m.LocalVarName} = {initializer};");
			if (m.Special != BoolSpecialKind.None)
				EmitBoolFlagBinding(w, m, m.LocalVarName, failure);
			else
			{
				var text = m.LocalVarName + "Text";
				using (w.If($"flags.TryGetValue({Quote(m.CliLongName)}, out var {text}) && {text} is not null"))
					EmitParseAndAssign(w, m, text, m.LocalVarName, failure);
			}
		}

		// Construct using the primary constructor if all members align, otherwise property assignment.
		if (bestCtorParamOrder is { } ctorOrder && ctorOrder.Length == members.Length)
			w.Line($"{storeFieldName} = new {typeFq}({string.Join(", ", ctorOrder.Select(n => byName[n].LocalVarName))});");
		else
		{
			w.Line($"{storeFieldName} = new {typeFq}();");
			foreach (var m in members)
				w.Line($"{storeFieldName}.{m.SymbolName} = {m.LocalVarName};");
		}
	}

	/// <summary>
	/// Emits local variable reconstruction for each options type in the injection chain.
	/// For every member: prefer the value from the command's <c>flags</c> dict (post-command flags),
	/// fall back to the pre-parsed static field (pre-command flags). This ensures flags work in
	/// either position: <c>myapp --verbose cmd</c> or <c>myapp cmd --verbose</c>. A required member given
	/// nowhere fails here, once the whole command line has been read.
	/// </summary>
	private static void EmitOptionsReconstructLocals(
		CodeWriter w,
		ImmutableArray<(string TypeFq, string TypeMetadataName, ImmutableArray<string> AllBaseTypeMetadataNames, string StaticFieldName, string LocalVarName, ImmutableArray<ParameterModel> FlatMembers, ImmutableArray<string>? BestCtorParamOrder)> chain,
		ParseFailure failure,
		string? environmentPrefix = null)
	{
		if (chain.IsDefaultOrEmpty) return;

		// Each member falls back to the most specific options in the chain that declare it: each namespace's options
		// start from the values the enclosing options read, so that static holds the flag wherever it was given.
		var fallbackMap = OptionsMemberStatics(chain.Select(static o => (o.StaticFieldName, o.FlatMembers)));

		// Track which member vars have already been emitted (across chain entries, to avoid re-declaration).
		var emittedTmpVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var (typeFq, _, _, _, localVar, flatMembers, bestCtorParamOrder) in chain)
		{
			if (flatMembers.IsEmpty) continue;
			var byName = flatMembers.ToDictionary(static m => m.SymbolName, StringComparer.OrdinalIgnoreCase);

			// Extract each member: command-level flags take precedence over pre-parsed static value.
			foreach (var m in flatMembers)
			{
				var fallback = fallbackMap.TryGetValue(m.CliLongName, out var fb) ? fb : "default";
				var tmpName = ParamTemp(m, "ropt");
				// Skip if already emitted by a parent type in the chain (inherited members appear in multiple flat lists).
				if (!emittedTmpVars.Add(tmpName)) continue;
				if (m.Special != BoolSpecialKind.None)
				{
					// The command line after the command wins, then before it (the static), then the environment variable.
					w.Line($"{GetCSharpCliType(m)} {tmpName} = {fallback};");
					var envVar = ComputeEffectiveEnvVarName(m, environmentPrefix);
					var envFallback = envVar is null ? null : $"({OptionFlagSeenCheck(m)} ? null : {BoolFromEnvironmentCall(m, envVar)})";
					EmitBoolFlagBinding(w, m, tmpName, failure, envFallback);
				}
				else
					EmitReparseOptionsMember(w, m, tmpName, fallback, environmentPrefix, failure);
			}

			// Construct the local options instance using pre-computed constructor order.
			if (bestCtorParamOrder is { } ctorOrder && ctorOrder.Length == flatMembers.Length)
				w.Line($"var {localVar} = new {typeFq}({string.Join(", ", ctorOrder.Select(n => "__ropt_" + byName[n].LocalVarName))});");
			else
			{
				w.Line($"var {localVar} = new {typeFq}();");
				foreach (var m in flatMembers)
					w.Line($"{localVar}.{m.SymbolName} = {ParamTemp(m, "ropt")};");
			}
		}
	}

	/// <summary>
	/// A value-typed options member: its text from the command's flags, else its environment variable when the flag wasn't
	/// given before the command either, parsed over the pre-parsed static value. A value that doesn't parse fails like the
	/// same flag before the command, naming the environment variable when it came from there.
	/// </summary>
	private static void EmitReparseOptionsMember(CodeWriter w, ParameterModel m, string tmpName, string fallback, string? environmentPrefix, ParseFailure failure)
	{
		var text = tmpName + "Txt";
		w.Line($"flags.TryGetValue({Quote(m.CliLongName)}, out var {text});");
		var envVar = ComputeEffectiveEnvVarName(m, environmentPrefix);
		if (envVar is not null)
		{
			using (w.If($"{text} == null && !{OptionFlagSeenCheck(m)}"))
				EmitEnvironmentFallback(w, m, envVar, text, tmpName + "EnvVal");
		}

		if (m.IsRequired)
			EmitParseError(w, $"{text} == null && !{OptionFlagSeenCheck(m)}",
				$"Console.Error.WriteLine($\"Error: missing required flag --{Escape(m.CliLongName)}.\");", m, failure);

		w.Line($"{GetCSharpCliType(m)} {tmpName} = {fallback};");
		using (w.If($"{text} is not null"))
			EmitParseFromString(w, m, text, tmpName, outVarKeyword: false,
				WithEnvironmentSource(failure, envVar, $"!flags.ContainsKey({Quote(m.CliLongName)})"));
	}

	/// <summary>
	/// Binds a collection flag: each value given for it (it can be repeated), or else its environment variable, split on its
	/// separator with empty parts kept (<see cref="ArghCollectionBinding"/>), then each element parsed by the code that
	/// parses a single value of the element type (<see cref="EmitParseFromString"/>), which rejects an element that isn't valid.
	/// </summary>
	private static void EmitBindCollectionParameter(CodeWriter w, ParameterModel p, string? environmentPrefix, ParseFailure failure)
	{
		var flagKey = Quote(p.CliLongName);
		var acc = p.LocalVarName + "_acc";
		var given = ParamTemp(p, "given");
		var values = ParamTemp(p, "values");
		var envText = ParamTemp(p, "envText");
		var raw = ParamTemp(p, "raw");
		var element = ParamTemp(p, "ce");
		var envVar = ComputeEffectiveEnvVarName(p, environmentPrefix);

		w.Line($"var {given} = multiFlags.TryGetValue({flagKey}, out var {values});");
		var missing = "!" + given;
		var environmentValue = "null";
		if (envVar is not null)
		{
			w.Line($"string? {envText} = null;");
			using (w.If(missing))
				EmitEnvironmentFallback(w, p, envVar, envText, ParamTemp(p, "envVal"));
			missing += $" && {envText} == null";
			environmentValue = envText;
		}

		if (p.IsRequired)
			EmitParseError(w, missing,
				$"Console.Error.WriteLine($\"Error: missing required flag --{Escape(p.CliLongName)}.\");", p, failure);

		var elementFailure = WithEnvironmentSource(failure, envVar, "!" + given);
		var separator = p.CollectionSeparator is null ? "null" : Quote(p.CollectionSeparator);
		using (w.Foreach($"var {raw} in {ArghCollectionBindingFq}.Elements({given} ? {values} : null, {environmentValue}, {separator})"))
		{
			EmitParseFromString(w, ForElementParsing(p), raw, element, outVarKeyword: true, elementFailure);
			if (p.CollectionTargetIsReadOnlySet)
				EmitParseError(w, $"!{acc}.Add({element})",
					$"Console.Error.WriteLine($\"Error: duplicate value '{{{element}}}' for --{Escape(p.CliLongName)}.\");", p, elementFailure);
			else
				w.Line($"{acc}.Add({element});");
		}

		var declType = p.FullDeclaredTypeFq ?? "object";
		var value = p.CollectionTargetIsArray ? $"{acc}.ToArray()" : acc;
		var useNullWhenUnset = !p.IsRequired && p.DeclaredNullableAnnotated;
		w.Line($"{declType} {p.LocalVarName} = {(useNullWhenUnset ? $"{acc}.Count == 0 ? null : {value}" : value)};");
	}

	private const string ArghCollectionBindingFq = "global::Nullean.Argh.Runtime.ArghCollectionBinding";

	/// <summary><c>head;</c>, or <c>head { A = a, … };</c> when there are object-initializer <paramref name="assignments"/>.</summary>
	private static void EmitObjectCreation(CodeWriter w, string head, IReadOnlyList<string> assignments)
	{
		if (assignments.Count == 0)
		{
			w.Line(head + ";");
			return;
		}

		using (w.Block(head, ";"))
		{
			foreach (var assignment in assignments)
				w.Line(assignment + ",");
		}
	}

	/// <summary>Builds an <c>[AsParameters]</c> object: constructor arguments, then init-only members.</summary>
	private static void EmitAsParametersCreation(CodeWriter w, string varName, string typeFq, IReadOnlyList<ParameterModel> group, string injectedValue)
	{
		string Value(ParameterModel p) => p.Kind == ParameterKind.Injected ? injectedValue : p.LocalVarName;
		var ctorArgs = string.Join(", ", group.Where(static p => !p.AsParametersUseInit).Select(Value));
		EmitObjectCreation(w, $"var {varName} = new {typeFq}({ctorArgs})",
			group.Where(static p => p.AsParametersUseInit).Select(p => $"{p.AsParametersClrName} = {Value(p)}").ToList());
	}

	private static void EmitAsParametersConstruction(CodeWriter w, CommandModel cmd)
	{
		if (cmd.HandlerParamTypes.IsDefaultOrEmpty)
			return;

		foreach (var mp in cmd.HandlerParamTypes)
		{
			if (!mp.IsAsParameters)
				continue;

			var group = cmd.Parameters
				.Where(p => p.AsParametersOwnerParamName == mp.Name)
				.OrderBy(p => p.AsParametersMemberOrder)
				.ToArray();
			if (group.Length == 0)
				continue;

			var typeFq = group[0].AsParametersTypeFq;
			if (typeFq is null)
				continue;

			EmitAsParametersCreation(w, AsParametersConstructedVarName(mp.Name), typeFq, group, "ct");
		}
	}

	private static void EmitAsParametersConstructionForDto(CodeWriter w, CommandModel cmd)
	{
		var group = cmd.Parameters
			.Where(static p => p.AsParametersOwnerParamName is not null)
			.OrderBy(static p => p.AsParametersMemberOrder)
			.ToArray();
		if (group.Length == 0 || group[0].AsParametersTypeFq is not { } typeFq)
		{
			w.Return("false");
			return;
		}

		EmitAsParametersCreation(w, "__dto", typeFq, group, "default(global::System.Threading.CancellationToken)");
		w.Line("value = __dto;");
		EmitDtoRegisteredValidationAndReturn(w, group.Select(static p => (p.AsParametersClrName ?? p.SymbolName, p.CliLongName)).ToList());
	}

	/// <summary>Ends a DTO try-parse: runs the registered validator over <c>value</c> (errors already printed, like the built-in checks) and returns false when it fails.</summary>
	private static void EmitDtoRegisteredValidationAndReturn(CodeWriter w, List<(string Member, string Flag)> flags)
	{
		using (w.If($"global::Nullean.Argh.Validation.ArghValidation.HasValidators && !global::Nullean.Argh.Validation.ArghValidation.Validate(value, {MemberFlagsLiteral(flags)}, out _)"))
		{
			w.Line("value = null;");
			w.Return("false");
		}

		w.Line();
		w.Return("true");
	}

	private static void EmitOptionsDtoConstructionAndReturn(CodeWriter w, string typeFq, ImmutableArray<ParameterModel> members, ImmutableArray<string>? bestCtorParamOrder)
	{
		var byName = members.ToDictionary(static m => m.SymbolName, StringComparer.OrdinalIgnoreCase);
		if (bestCtorParamOrder is { } ctorOrder && ctorOrder.Length > 0 && ctorOrder.Length == members.Length)
			w.Line($"value = new {typeFq}({string.Join(", ", ctorOrder.Select(n => byName[n].LocalVarName))});");
		else
		{
			w.Line($"var __dto = new {typeFq}();");
			foreach (var m in members)
				w.Line($"__dto.{m.SymbolName} = {m.LocalVarName};");

			w.Line("value = __dto;");
		}

		EmitDtoRegisteredValidationAndReturn(w, members.Select(static m => (m.SymbolName, m.CliLongName)).ToList());
	}

	private static string AsParametersConstructedVarName(string methodParameterName) =>
		"__as_" + Naming.SanitizeIdentifier(methodParameterName);

	private static void EmitValidationChecks(
		CodeWriter w,
		CommandModel cmd,
		string failureExit,
		string? entryAssemblyName,
		string? flagHelpStdErrMethodName = null)
	{
		var runHint = ValidationRunHint(cmd, entryAssemblyName);
		foreach (var p in cmd.Parameters)
		{
			if (p.Kind == ParameterKind.Injected || p.Kind == ParameterKind.OptionsInjected)
				continue;
			EmitParameterValidation(w, new ValidationTarget(p, p.LocalVarName, failureExit, flagHelpStdErrMethodName, runHint));
		}
	}

	/// <summary>The run-hint line baked into validation errors, or <see langword="null"/> without an entry assembly.</summary>
	private static string? ValidationRunHint(CommandModel cmd, string? entryAssemblyName)
	{
		if (entryAssemblyName is null || string.IsNullOrEmpty(cmd.CommandName))
			return null;
		var route = cmd.RoutePrefix.IsDefaultOrEmpty
			? ""
			: string.Join(" ", cmd.RoutePrefix) + " ";
		return $"Run '{Escape(entryAssemblyName)} {Escape(route)}{Escape(cmd.CommandName)} --help' for usage.";
	}

	/// <summary>
	/// Validates the options members a command binds, after <see cref="EmitOptionsReconstructLocals"/> has merged the
	/// options given before the command, after it and from environment variables into the <c>__ropt_</c> locals. The
	/// checks and errors are the ones method parameters get.
	/// </summary>
	private static void EmitOptionsMemberValidation(
		CodeWriter w,
		CommandModel cmd,
		ImmutableArray<(string TypeFq, string TypeMetadataName, ImmutableArray<string> AllBaseTypeMetadataNames, string StaticFieldName, string LocalVarName, ImmutableArray<ParameterModel> FlatMembers, ImmutableArray<string>? BestCtorParamOrder)> chain,
		string failureExit,
		string? entryAssemblyName,
		string? flagHelpStdErrMethodName)
	{
		if (chain.IsDefaultOrEmpty)
			return;
		var runHint = ValidationRunHint(cmd, entryAssemblyName);
		var validated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var options in chain)
		{
			foreach (var m in options.FlatMembers)
			{
				if (m.Kind == ParameterKind.Injected || !validated.Add(m.CliLongName))
					continue;
				EmitParameterValidation(w, new ValidationTarget(m, ParamTemp(m, "ropt"), failureExit, flagHelpStdErrMethodName, runHint));
			}
		}
	}

	private static void EmitParameterValidation(CodeWriter w, ValidationTarget t)
	{
		var p = t.P;
		if (p.Validations.IsDefaultOrEmpty)
			return;

		if (p.IsCollection)
		{
			EmitCollectionValidation(w, t);
			return;
		}

		foreach (var constraint in p.Validations)
			EmitConstraintCheck(w, t, constraint);
	}

	/// <summary>
	/// A collection's constraints: a count checks the whole collection, the filesystem-path family reports every element
	/// that fails (<see cref="EmitCollectionFilesystemValidation"/>), and every other constraint checks each element as it
	/// would a single value of the element type.
	/// </summary>
	private static void EmitCollectionValidation(CodeWriter w, ValidationTarget t)
	{
		var p = t.P;
		EmitCollectionFilesystemValidation(w, t);
		foreach (var count in p.Validations.OfType<CollectionCountConstraint>())
			EmitCollectionCountConstraintCheck(w, t, count);

		var elementConstraints = p.Validations
			.Where(static c => c is not CollectionCountConstraint && !IsCollectionFilesystemConstraint(c))
			.ToList();
		if (elementConstraints.Count == 0)
			return;

		var item = ParamTemp(p, "vitem");
		var elementTarget = t with { P = ForElementParsing(p), VarName = item };
		void CheckEach()
		{
			using (w.Foreach($"var {item} in {t.VarName}"))
			{
				foreach (var constraint in elementConstraints)
					EmitConstraintCheck(w, elementTarget, constraint);
			}
		}

		if (!p.IsRequired && p.DeclaredNullableAnnotated)
		{
			using (w.If($"{t.VarName} != null"))
				CheckEach();
		}
		else
			CheckEach();
	}

	private static void EmitConstraintCheck(CodeWriter w, ValidationTarget t, ValidationConstraint constraint)
	{
		switch (constraint)
		{
			case RangeConstraint r:                         EmitRangeConstraintCheck(w, t, r); break;
			case TimeSpanRangeConstraint tsr:               EmitTimeSpanRangeConstraintCheck(w, t, tsr); break;
			case CollectionCountConstraint cc:              EmitCollectionCountConstraintCheck(w, t, cc); break;
			case StringLengthConstraint s:                  EmitStringLengthConstraintCheck(w, t, s); break;
			case RegexConstraint rx:                        EmitRegexConstraintCheck(w, t, rx); break;
			case AllowedValuesConstraint av:                EmitAllowedValuesConstraintCheck(w, t, av); break;
			case DeniedValuesConstraint dv:                 EmitDeniedValuesConstraintCheck(w, t, dv); break;
			case EmailConstraint:                           EmitEmailConstraintCheck(w, t); break;
			case UrlConstraint:                             EmitUrlConstraintCheck(w, t); break;
			case UriSchemeConstraint us:                    EmitUriSchemeConstraintCheck(w, t, us); break;
			case RejectSymbolicLinksConstraint:             EmitRejectSymbolicLinksConstraintCheck(w, t); break;
			case ExistingPathConstraint:                    EmitExistingPathConstraintCheck(w, t); break;
			case NonExistingPathConstraint:                 EmitNonExistingPathConstraintCheck(w, t); break;
			case FileExtensionsConstraint fe:               EmitFileExtensionsConstraintCheck(w, t, fe); break;
		}
	}

	/// <summary>
	/// How errors name a parameter, escaped for a generated string: <c>&lt;name&gt;</c> for a positional argument, as usage
	/// shows it, and <c>--name</c> for a flag.
	/// </summary>
	private static string ErrorArgName(ParameterModel p) =>
		p.Kind == ParameterKind.Positional ? $"<{Escape(p.CliLongName)}>" : $"--{Escape(p.CliLongName)}";

	/// <summary><c>Console.Error.WriteLine("Error: --name: <paramref name="message"/>");</c>, the message already escaped.</summary>
	private static string FlagErrorLine(ValidationTarget t, string message) => ErrorLine($"Error: {ErrorArgName(t.P)}: {message}");

	/// <summary>An error line as an interpolated string, so numbers in <paramref name="message"/> format with the parse culture.</summary>
	private static string InterpolatedErrorLine(string message) => $"Console.Error.WriteLine($\"{EscapeForHelpInterpolation(message)}\");";

	/// <summary>
	/// The value a check reads, when it may be unset: a nullable value type is read through <c>.Value</c> after a
	/// <c>.HasValue</c> guard, and an unset value passes.
	/// </summary>
	private static bool IsNullableValueTypeCheck(ParameterModel p, bool primitiveOnly = false) =>
		!p.IsRequired && p.Special == BoolSpecialKind.None
		&& (primitiveOnly ? p.ScalarKind == CliScalarKind.Primitive : IsNumericOrPrimitiveScalar(p)) && p.TypeName != "string"
		&& p.TypeName.EndsWith("?", StringComparison.Ordinal);

	/// <summary>
	/// For a reference type: <c>value is { } __check_value &amp;&amp; </c>, so an unset value passes, and the copy the
	/// <paramref name="check"/> reads. Unlike <c>value != null</c>, the pattern isn't a pure null test, so the compiler
	/// keeps a non-nullable value not-null afterwards and doesn't warn where it's passed on (CS8601, CS8604).
	/// </summary>
	private static (string NullGuard, string Access) ReferenceCheckAccess(ValidationTarget t, string check)
	{
		var access = $"__{check}_{t.VarName}";
		return ($"{t.VarName} is {{ }} {access} && ", access);
	}

	private static void EmitRangeConstraintCheck(CodeWriter w, ValidationTarget t, RangeConstraint r)
	{
		var isNullableValueType = IsNullableValueTypeCheck(t.P);
		var guard = isNullableValueType ? $"{t.VarName}.HasValue && (" : "";
		var closeGuard = isNullableValueType ? ")" : "";
		var access = isNullableValueType ? $"{t.VarName}.Value" : t.VarName;
		string Bound(string literal) => r.CompareCast is null ? literal : $"(({r.CompareCast})({literal}))";
		var checks = new List<string>();
		if (r.MinLiteral is { } min)
			checks.Add($"{access} < {Bound(min)}");
		if (r.MaxLiteral is { } max)
			checks.Add($"{access} > {Bound(max)}");
		var limitText = (r.MinLiteral, r.MaxLiteral) switch
		{
			({ } lo, { } hi) => $"between {RangeBoundText(r, lo)} and {RangeBoundText(r, hi)}",
			({ } lo, null) => $"at least {RangeBoundText(r, lo)}",
			_ => $"at most {RangeBoundText(r, r.MaxLiteral!)}",
		};
		EmitValidationFailure(w, t, $"{guard}{string.Join(" || ", checks)}{closeGuard}",
			InterpolatedErrorLine($"Error: --{t.CliName}: value must be {limitText}."));
	}

	/// <summary>
	/// A primitive, or a number bound through <c>INumberBase</c> (<c>short</c>, <c>Half</c>, ...): its nullable form is
	/// <c>Nullable&lt;T&gt;</c>, so a check reads <c>.Value</c> after <c>.HasValue</c> and an unset value passes.
	/// </summary>
	private static bool IsNumericOrPrimitiveScalar(ParameterModel p) =>
		p.ScalarKind == CliScalarKind.Primitive || p is { ScalarKind: CliScalarKind.Parsable, ParsableIsNumber: true };

	private static void EmitTimeSpanRangeConstraintCheck(CodeWriter w, ValidationTarget t, TimeSpanRangeConstraint tsr)
	{
		// The bounds were parsed at build time, so the check compares ticks.
		var varName = t.VarName;
		var isNullableValueType = IsNullableValueTypeCheck(t.P, primitiveOnly: true);
		var guard = isNullableValueType ? $"{varName}.HasValue && (" : "";
		var closeGuard = isNullableValueType ? ")" : "";
		var ticks = isNullableValueType ? $"{varName}.Value.Ticks" : $"{varName}.Ticks";
		EmitValidationFailure(w, t, $"{guard}{ticks} < {tsr.MinTicks}L || {ticks} > {tsr.MaxTicks}L{closeGuard}",
			FlagErrorLine(t, $"value must be between {Escape(tsr.MinText)} and {Escape(tsr.MaxText)}."));
	}

	/// <summary>The condition and message for a minimum and/or maximum on a length or count.</summary>
	private static (string Condition, string Message) LengthLimit(string length, int? min, int? max, string guard, string units)
	{
		if (min.HasValue && max.HasValue)
			return ($"{guard}({length} < {min} || {length} > {max})", $"between {min} and {max} {units}.");
		if (min.HasValue)
			return ($"{guard}{length} < {min}", $"at least {min} {units}.");
		return ($"{guard}{length} > {max}", $"at most {max} {units}.");
	}

	private static void EmitCollectionCountConstraintCheck(CodeWriter w, ValidationTarget t, CollectionCountConstraint cc)
	{
		var p = t.P;
		var lenExpr = p.CollectionTargetIsArray ? $"{t.VarName}.Length" : $"{t.VarName}.Count";
		var nullGuard = !p.IsRequired && p.DeclaredNullableAnnotated ? $"{t.VarName} != null && " : "";
		var (condition, limit) = LengthLimit(lenExpr, cc.Min, cc.Max, nullGuard, "items");
		EmitValidationFailure(w, t, condition, FlagErrorLine(t, Escape("must have " + limit)));
	}

	private static void EmitStringLengthConstraintCheck(CodeWriter w, ValidationTarget t, StringLengthConstraint s)
	{
		var (nullGuard, sv) = ReferenceCheckAccess(t, "length");
		var (condition, limit) = LengthLimit($"{sv}.Length", s.Min, s.Max, nullGuard, "characters");
		EmitValidationFailure(w, t, condition, FlagErrorLine(t, Escape("value must be " + limit)));
	}

	private static void EmitRegexConstraintCheck(CodeWriter w, ValidationTarget t, RegexConstraint rx)
	{
		var (nullGuard, rv) = ReferenceCheckAccess(t, "pattern");
		EmitValidationFailure(w, t,
			$"{nullGuard}!global::System.Text.RegularExpressions.Regex.IsMatch({rv}, @\"{EscapeVerbatimString(rx.Pattern)}\")",
			FlagErrorLine(t, $"value does not match required pattern {Escape(rx.Pattern)}."));
	}

	/// <summary>
	/// The condition that <paramref name="t"/>'s value is (or, with <paramref name="negate"/>, isn't) one of
	/// <paramref name="values"/>: ordinal for strings, otherwise <see cref="MembershipEquals"/>.
	/// </summary>
	private static string MembershipCondition(ValidationTarget t, IEnumerable<string> values, string? equalsCast, bool negate)
	{
		var not = negate ? "!" : "";
		var join = negate ? " && " : " || ";
		if (t.P.TypeName == "string")
		{
			var (nullGuard, access) = ReferenceCheckAccess(t, negate ? "allowed" : "denied");
			return $"{nullGuard}({string.Join(join, values.Select(v => $"{not}string.Equals({access}, {v}, global::System.StringComparison.Ordinal)"))})";
		}

		var isNullableValueType = IsNullableValueTypeCheck(t.P);
		var guard = isNullableValueType ? $"{t.VarName}.HasValue && " : "";
		var valueAccess = isNullableValueType ? $"{t.VarName}.Value" : t.VarName;
		return $"{guard}({string.Join(join, values.Select(v => not + MembershipEquals(valueAccess, v, equalsCast)))})";
	}

	private static void EmitAllowedValuesConstraintCheck(CodeWriter w, ValidationTarget t, AllowedValuesConstraint av) =>
		EmitValidationFailure(w, t, MembershipCondition(t, av.Values, av.EqualsCast, negate: true),
			InterpolatedErrorLine($"Error: --{t.CliName}: value must be one of: {string.Join(", ", CultureDisplayValues(av.Values, av.DisplayValues, av.CultureTypes))}."));

	/// <summary>
	/// Whether <paramref name="access"/> equals <paramref name="literal"/>: <c>.Equals</c> for floating-point values (see
	/// <see cref="AllowedValuesConstraint.EqualsCast"/>), so <c>NaN</c> matches <c>NaN</c>; <c>==</c> otherwise.
	/// </summary>
	private static string MembershipEquals(string access, string literal, string? equalsCast) =>
		equalsCast switch
		{
			null => $"({access} == {literal})",
			"" => $"{access}.Equals({literal})",
			_ => $"{access}.Equals(({equalsCast})({literal}))",
		};

	private static void EmitDeniedValuesConstraintCheck(CodeWriter w, ValidationTarget t, DeniedValuesConstraint dv) =>
		EmitValidationFailure(w, t, MembershipCondition(t, dv.Values, dv.EqualsCast, negate: false),
			InterpolatedErrorLine($"Error: --{t.CliName}: value must not be: {string.Join(", ", CultureDisplayValues(dv.Values, dv.DisplayValues, dv.CultureTypes))}."));

	private static void EmitEmailConstraintCheck(CodeWriter w, ValidationTarget t)
	{
		// Simple email check: at least one char, @, at least one char (DataAnnotations-compatible)
		var (nullGuard, ev) = ReferenceCheckAccess(t, "email");
		EmitValidationFailure(w, t, $"{nullGuard}({ev}.IndexOf('@') < 1 || {ev}.IndexOf('@') == {ev}.Length - 1)",
			FlagErrorLine(t, "value is not a valid email address."));
	}

	private static void EmitUrlConstraintCheck(CodeWriter w, ValidationTarget t)
	{
		// Validates absolute URL with http, https, or ftp scheme
		var (nullGuard, v) = ReferenceCheckAccess(t, "url");
		var check = CodeWriter.IndentLines(
			$"(global::System.Uri.TryCreate({v}, global::System.UriKind.Absolute, out var __urlCheck_{t.VarName}) &&\n" +
			$" (__urlCheck_{t.VarName}.Scheme == \"http\" || __urlCheck_{t.VarName}.Scheme == \"https\" || __urlCheck_{t.VarName}.Scheme == \"ftp\"))");
		EmitValidationFailure(w, t, $"{nullGuard}!\n{check}", FlagErrorLine(t, "value is not a valid URL."));
	}

	private static void EmitUriSchemeConstraintCheck(CodeWriter w, ValidationTarget t, UriSchemeConstraint us)
	{
		var (nullGuard, access) = ReferenceCheckAccess(t, "scheme");
		var schemeChecks = us.Schemes.Select(s => $"{access}.Scheme == {Quote(s)}");
		EmitValidationFailure(w, t, $"{nullGuard}(!{access}.IsAbsoluteUri || !({string.Join(" || ", schemeChecks)}))",
			FlagErrorLine(t, $"URI scheme must be one of: {Escape(string.Join(", ", us.Schemes))}."));
	}

	private static void EmitRejectSymbolicLinksConstraintCheck(CodeWriter w, ValidationTarget t)
	{
		var (nullGuard, access) = ReferenceCheckAccess(t, "link");
		EmitValidationFailure(w, t, $"{nullGuard}global::Nullean.Argh.ArghIO.PathIsSymbolicOrReparsePoint({access}.FullName)",
			FlagErrorLine(t, "path must not be a symbolic link or reparse point."));
	}

	private static void EmitExistingPathConstraintCheck(CodeWriter w, ValidationTarget t)
	{
		var (nullGuard, access) = ReferenceCheckAccess(t, "exists");
		var isFile = t.P.ScalarKind == CliScalarKind.FileInfo;
		EmitValidationFailure(w, t, $"{nullGuard}!global::System.IO.{(isFile ? "File" : "Directory")}.Exists({access}.FullName)",
			FlagErrorLine(t, isFile ? "file does not exist." : "directory does not exist."));
	}

	private static void EmitNonExistingPathConstraintCheck(CodeWriter w, ValidationTarget t)
	{
		var (nullGuard, access) = ReferenceCheckAccess(t, "free");
		EmitValidationFailure(w, t, $"{nullGuard}(global::System.IO.File.Exists({access}.FullName) || global::System.IO.Directory.Exists({access}.FullName))",
			FlagErrorLine(t, t.P.ScalarKind == CliScalarKind.FileInfo ? "path already exists or is occupied by a directory." : "path already exists."));
	}

	/// <summary>The condition that a file's extension is none of <paramref name="extensions"/>, ignoring case.</summary>
	private static string FileExtensionMismatch(string fileExpr, IEnumerable<string> extensions) =>
		string.Join(" && ", extensions.Select(ext =>
			$"!string.Equals(global::System.IO.Path.GetExtension({fileExpr}.Name).TrimStart('.'), {Quote(ext)}, global::System.StringComparison.OrdinalIgnoreCase)"));

	private static void EmitFileExtensionsConstraintCheck(CodeWriter w, ValidationTarget t, FileExtensionsConstraint fe)
	{
		var (nullGuard, access) = ReferenceCheckAccess(t, "extension");
		EmitValidationFailure(w, t, $"{nullGuard}({FileExtensionMismatch(access, fe.Extensions)})",
			FlagErrorLine(t, $"extension must be one of: {Escape(string.Join(", ", fe.Extensions))}."));
	}

	private static bool IsCollectionFilesystemConstraint(ValidationConstraint c) =>
		c is ExistingPathConstraint or NonExistingPathConstraint or RejectSymbolicLinksConstraint or FileExtensionsConstraint;

	/// <summary>
	/// Collection-aware emission for the filesystem-path attribute family (<c>[Existing]</c>, <c>[NonExisting]</c>,
	/// <c>[RejectSymbolicLinks]</c>, <c>[FileExtensions]</c>) applied to a <c>List&lt;FileInfo&gt;</c>,
	/// <c>FileInfo[]</c>, <c>DirectoryInfo[]</c>, etc. — including variadic <c>[Argument]</c> collections.
	/// Unlike the scalar constraint switch above (which exits on the first violation), this loops over every
	/// element and collects every failing item before printing one error block and exiting once, so a user
	/// passing e.g. five files with two missing sees both, not just the first.
	/// </summary>
	private static void EmitCollectionFilesystemValidation(CodeWriter w, ValidationTarget t)
	{
		var p = t.P;
		var fsConstraints = p.Validations.Where(IsCollectionFilesystemConstraint).ToList();
		if (fsConstraints.Count == 0)
			return;

		var isDir = p.ElementScalarKind == CliScalarKind.DirectoryInfo;
		var failuresVar = ParamTemp(p, "fsFailures");
		var itemVar = ParamTemp(p, "fsItem");
		var msgVar = ParamTemp(p, "fsMsg");
		var argToken = ErrorArgName(p);
		string AddFailure(string message) => $"{failuresVar}.Add({itemVar}.FullName + \": {message}\");";

		void CheckItems()
		{
			using (w.Foreach($"var {itemVar} in {t.VarName}"))
			{
				foreach (var c in fsConstraints)
				{
					switch (c)
					{
						case RejectSymbolicLinksConstraint:
							// Runs before existence/extension checks; a rejected symlink skips further checks for that item.
							using (w.If($"global::Nullean.Argh.ArghIO.PathIsSymbolicOrReparsePoint({itemVar}.FullName)"))
							{
								w.Line(AddFailure("path must not be a symbolic link or reparse point."));
								w.Line("continue;");
							}

							break;
						case ExistingPathConstraint:
							w.If($"!global::System.IO.{(isDir ? "Directory" : "File")}.Exists({itemVar}.FullName)",
								AddFailure(isDir ? "directory does not exist." : "file does not exist."));
							break;
						case NonExistingPathConstraint:
							w.If($"global::System.IO.File.Exists({itemVar}.FullName) || global::System.IO.Directory.Exists({itemVar}.FullName)",
								AddFailure("path already exists."));
							break;
						case FileExtensionsConstraint fe:
							w.If(FileExtensionMismatch(itemVar, fe.Extensions),
								AddFailure($"extension must be one of: {Escape(string.Join(", ", fe.Extensions))}."));
							break;
					}
				}
			}
		}

		w.Line($"var {failuresVar} = new List<string>();");
		if (!p.IsRequired && p.DeclaredNullableAnnotated)
		{
			using (w.If($"{t.VarName} != null"))
				CheckItems();
		}
		else
			CheckItems();

		using (w.If($"{failuresVar}.Count > 0"))
		{
			w.Foreach($"var {msgVar} in {failuresVar}", $"Console.Error.WriteLine(\"Error: {argToken}: \" + {msgVar});");
			EmitValidationErrorFooter(w, t);
			w.Line(t.FailureExit + ";");
		}
	}

	private static string EscapeVerbatimString(string s) => s.Replace("\"", "\"\"");

	private static void EmitCommandRunnerFuzzyFailHelper(
		CodeWriter w,
		CommandModel cmd,
		string? flagHelpStdErrMethodName,
		string? parseFailureRunHint)
	{
		var flagParams = cmd.Parameters
			.Where(static p => IsEmittedFlagLike(p.Kind))
			.ToList();
		var runHint = parseFailureRunHint is null ? null : $"Console.Error.WriteLine({Quote(parseFailureRunHint)});";

		if (flagParams.Count > 0)
			w.Line(FlagFuzzyCandidates(flagParams));

		using (w.Block("int FailUnknownLongOption(string flagName)"))
		{
			if (flagParams.Count > 0)
				EmitUnknownLongOptionBody(w, flagHelpStdErrMethodName, runHint, "2");
			else
			{
				w.Line(UnknownLongOptionError);
				if (runHint is not null)
				{
					w.Line("Console.Error.WriteLine();");
					w.Line(runHint);
				}

				w.Return("2");
			}
		}

		w.Line();
	}
}
