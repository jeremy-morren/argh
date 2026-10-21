using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Argh;

public sealed partial class CliParserGenerator
{
	private static string? ComputeEffectiveEnvVarName(ParameterModel p, string? environmentPrefix)
	{
		if (!string.IsNullOrEmpty(p.EnvVarName))
			return p.EnvVarName;
		if (!string.IsNullOrEmpty(environmentPrefix) && p.Kind == ParameterKind.Flag)
		{
			// "--my-flag" → strip leading "--", replace "-" with "_", uppercase, prepend prefix
			var flagName = p.CliLongName.TrimStart('-').Replace('-', '_').ToUpperInvariant();
			return environmentPrefix + flagName;
		}
		return null;
	}

	private static void EmitCommandRunner(
		CodeWriter w,
		CommandModel cmd,
		ImmutableArray<GlobalMiddlewareRegistration> globalMiddleware,
		bool emitDtoTryParse = false,
		bool dtoLenient = false,
		string? dtoMethodName = null,
		string? dtoResultTypeFq = null,
		string? dtoOptionsTypeFq = null,
		ImmutableArray<string>? dtoOptionsBestCtorParamOrder = null,
		ImmutableArray<(string TypeFq, string TypeMetadataName, ImmutableArray<string> AllBaseTypeMetadataNames, string StaticFieldName, string LocalVarName, ImmutableArray<ParameterModel> FlatMembers, ImmutableArray<string>? BestCtorParamOrder)> injectedOptions = default,
		string? entryAssemblyName = null,
		string? environmentPrefix = null)
	{
		var anyRepeatedCollection = cmd.Parameters.Any(static p =>
			p is { IsCollection: true, Kind: ParameterKind.Flag } && p.CollectionSeparator is null);

		var failureExit = emitDtoTryParse ? "return false" : "return 2";
		var helpMethodName = emitDtoTryParse ? null : HelpPrinterMethodForCommand(cmd);
		var flagHelpStdErrMethodName = emitDtoTryParse || cmd.IsRootDefault ? null : $"PrintHelp_{cmd.RunMethodName}_Flag_ToStdErr";
		string? parseFailureRunHint = null;
		if (!emitDtoTryParse && entryAssemblyName is not null && !string.IsNullOrEmpty(cmd.CommandName))
		{
			var routeForHint = cmd.RoutePrefix.IsDefaultOrEmpty ? "" : string.Join(" ", cmd.RoutePrefix) + " ";
			parseFailureRunHint = $"Run '{Escape(entryAssemblyName)} {Escape(routeForHint)}{Escape(cmd.CommandName)} --help' for usage.";
		}

		var failure = new ParseFailure(failureExit, helpMethodName, flagHelpStdErrMethodName, parseFailureRunHint);
		var exit = failureExit + ";";
		string SetFlag(string value) => anyRepeatedCollection ? $"SetFlag(flagName, {value});" : $"flags[flagName] = {value};";
		const string unknownLongOption = "Console.Error.WriteLine($\"Error: unknown option '--{flagName}'.\");";

		string header;
		if (emitDtoTryParse)
		{
			if (dtoMethodName is null || dtoResultTypeFq is null)
				throw new InvalidOperationException("DTO try-parse requires method name and result type.");
			header = $"internal static bool {dtoMethodName}(string[] args, out {dtoResultTypeFq}? value)";
		}
		else
			header = $"private static async Task<int> {cmd.RunMethodName}(string[] args, CancellationToken ct)";

		using (w.Block(header))
		{
			if (emitDtoTryParse)
				w.Line("value = null;");

			EmitCliValueDeclarations(w, cmd, dtoOptionsTypeFq);

			w.Line("var flags = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);");
			if (anyRepeatedCollection)
			{
				w.Line("var multiFlags = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);");
				EmitIsMultiFlagPredicate(w, cmd);
				using (w.Block("void SetFlag(string name, string? value)"))
				{
					using (w.If("IsMultiFlag(name)"))
					{
						w.Line("if (value is null) return;");
						w.Line("if (!multiFlags.TryGetValue(name, out var list)) { list = new List<string>(); multiFlags[name] = list; }");
						w.Line("list.Add(value);");
						w.Line("return;");
					}

					w.Line("flags[name] = value;");
				}
			}

			w.Line("var positionals = new List<string>();");
			EmitBoolSwitchNames(w, cmd);
			EmitCanonFlagNameMethod(w, cmd);
			EmitShortFlagMethods(w, cmd, multiFlagsAvailable: anyRepeatedCollection,
				parseFailureRunHint: emitDtoTryParse ? null : parseFailureRunHint);
			EmitKnownNonBoolFlagNames(w, cmd);
			if (!emitDtoTryParse)
				EmitCommandRunnerFuzzyFailHelper(w, cmd, flagHelpStdErrMethodName, parseFailureRunHint);
			using (w.For("var i = 0; i < args.Length;"))
			{
				w.Line("var a = args[i];");
				// Help is a flag like any other: read in order, so it can be the value of a flag before it.
				if (helpMethodName is not null)
				{
					using (w.If("a == \"--help\" || a == \"-h\""))
					{
						w.Line($"{helpMethodName}();");
						w.Return("0");
					}
				}

				using (w.If("a.StartsWith(\"--\", StringComparison.Ordinal)"))
				{
					w.Line("var eq = a.IndexOf('=');");
					using (w.If("eq >= 0"))
					{
						w.Line("var flagName = CanonFlagName(a.Substring(2, eq - 2));");
						w.Line("var flagValue = a.Substring(eq + 1);");
						const string unknownFlag = "!IsBoolSwitchName(flagName) && !IsKnownNonBoolFlagName(flagName)";
						if (emitDtoTryParse && !dtoLenient)
						{
							using (w.If(unknownFlag))
							{
								w.Line(unknownLongOption);
								w.Line(exit);
							}
						}
						else if (!emitDtoTryParse)
							w.If(unknownFlag, "return FailUnknownLongOption(flagName);");

						w.Line(SetFlag("flagValue"));
						w.Line("i++;");
					}

					using (w.Else())
					{
						w.Line("var flagName = CanonFlagName(a.Substring(2));");
						using (w.If("IsBoolSwitchName(flagName)"))
						{
							// A bare switch has no value, unlike --flag=true: --no-flag=true is an error.
							w.Line("flags[flagName] = null;");
							w.Line("i++;");
						}

						using (w.Else())
						{
							if (emitDtoTryParse && dtoLenient)
							{
								using (w.If("!IsKnownNonBoolFlagName(flagName)"))
								{
									w.If("i + 1 < args.Length && !args[i + 1].StartsWith(\"-\", StringComparison.Ordinal)", "i += 2;");
									w.Else("i++;");
									w.Line("continue;");
								}
							}
							else if (emitDtoTryParse)
							{
								using (w.If("!IsKnownNonBoolFlagName(flagName)"))
								{
									w.Line(unknownLongOption);
									w.Line(exit);
								}
							}
							else
								w.If("!IsKnownNonBoolFlagName(flagName)", "return FailUnknownLongOption(flagName);");

							using (w.If("i + 1 >= args.Length"))
							{
								w.Line("Console.Error.WriteLine($\"Error: missing value for flag --{flagName}.\");");
								if (flagHelpStdErrMethodName is not null)
								{
									w.Line("Console.Error.WriteLine();");
									w.Line($"{flagHelpStdErrMethodName}(flagName);");
									w.Line("Console.Error.WriteLine();");
									if (parseFailureRunHint is not null)
										w.Line(ErrorLine(parseFailureRunHint));
								}
								else if (helpMethodName is not null)
									w.Line($"{helpMethodName}();");
								w.Line(exit);
							}

							w.Line(SetFlag("args[i + 1]"));
							w.Line("i += 2;");
						}
					}

					w.Line("continue;");
				}

				using (w.If("a.Length >= 2 && a[0] == '-' && a[1] != '-'"))
				{
					w.Line("var eqs = a.IndexOf('=');");
					using (w.If("eqs >= 0"))
					{
						w.Line("var shortKey = a.Substring(1, eqs - 1);");
						using (w.If("shortKey.Length != 1"))
						{
							w.Line("Console.Error.WriteLine(\"Error: short options must be a single letter (e.g. -e=value).\");");
							if (helpMethodName is not null)
								w.Line($"{helpMethodName}();");
							w.Line(exit);
						}

						if (emitDtoTryParse && dtoLenient)
							w.Line("TryApplyShortFlag(shortKey[0], a.Substring(eqs + 1));");
						else
							w.If("!TryApplyShortFlag(shortKey[0], a.Substring(eqs + 1))", exit);
						w.Line("i++;");
						w.Line("continue;");
					}

					using (w.If("a.Length == 2"))
					{
						w.Line("var sc = a[1];");
						using (w.If("IsShortBoolChar(sc)"))
						{
							w.If("!TryApplyShortFlag(sc, \"true\")", exit);
							w.Line("i++;");
							w.Line("continue;");
						}

						if (emitDtoTryParse && dtoLenient)
						{
							// lenient: skip unknown value-taking short flags using same heuristic as long flags
							using (w.If("i + 1 < args.Length && !args[i + 1].StartsWith(\"-\", StringComparison.Ordinal)"))
							{
								w.Line("TryApplyShortFlag(sc, args[i + 1]);");
								w.Line("i += 2;");
							}

							using (w.Else())
							{
								w.Line("TryApplyShortFlag(sc, \"true\");");
								w.Line("i++;");
							}

							w.Line("continue;");
						}
						else
						{
							using (w.If("i + 1 >= args.Length"))
							{
								w.Line("Console.Error.WriteLine($\"Error: missing value for short flag '-{sc}'.\");");
								if (helpMethodName is not null)
									w.Line($"{helpMethodName}();");
								w.Line(exit);
							}

							w.If("!TryApplyShortFlag(sc, args[i + 1])", exit);
							w.Line("i += 2;");
							w.Line("continue;");
						}
					}

					w.Line("Console.Error.WriteLine(\"Error: combined short flags (e.g. -abc) are not supported.\");");
					if (helpMethodName is not null)
						w.Line($"{helpMethodName}();");
					w.Line(exit);
				}

				w.Line("positionals.Add(a);");
				w.Line("i++;");
			}

			w.Line();

			foreach (var p in cmd.Parameters)
			{
				if (p.Kind is ParameterKind.Injected or ParameterKind.OptionsInjected or ParameterKind.Positional)
					continue;

				// The command line wins, then the environment variable, then the default the local starts with.
				if (p.Special != BoolSpecialKind.None)
				{
					var boolEnvVar = ComputeEffectiveEnvVarName(p, environmentPrefix);
					EmitBoolFlagBinding(w, p, p.LocalVarName, failure, boolEnvVar is null ? null : BoolFromEnvironmentCall(p, boolEnvVar));
					continue;
				}

				if (p.IsCollection && p.Kind == ParameterKind.Flag)
				{
					EmitBindCollectionParameter(w, p, anyRepeatedCollection, failure);
					continue;
				}

				var flagKey = Escape(p.CliLongName);
				var text = p.LocalVarName + "Text";
				var effectiveEnvVar = ComputeEffectiveEnvVarName(p, environmentPrefix);
				var missingRequired = $"Console.Error.WriteLine($\"Error: missing required flag --{flagKey}.\");";
				using (w.If($"!flags.TryGetValue(\"{flagKey}\", out var {text})"))
				{
					if (effectiveEnvVar != null)
						EmitEnvironmentFallback(w, p, effectiveEnvVar, text, p.LocalVarName + "EnvVal");
					if (p.IsRequired)
					{
						if (effectiveEnvVar != null)
							EmitParseError(w, $"{text} == null", missingRequired, p, failure);
						else
						{
							w.Line(missingRequired);
							EmitAfterCliParseErrorHelp(w, p, failure);
							w.Line(exit);
						}
					}
					else if (effectiveEnvVar == null)
						w.Line($"{text} = null;");
				}

				EmitParseAndAssign(w, p, text, p.LocalVarName, failure);
			}

			var posIndex = 0;
			foreach (var p in cmd.Parameters)
			{
				if (p.Kind != ParameterKind.Positional)
					continue;

				if (p.IsVariadic)
				{
					EmitVariadicPositionalParse(w, p, posIndex, failure);
					posIndex++;
					continue;
				}

				// Without the argument, the value comes from its [Env] variable when it has one.
				var raw = $"positionals[{posIndex}]";
				var missing = $"positionals.Count <= {posIndex}";
				if (!string.IsNullOrEmpty(p.EnvVarName))
				{
					raw = p.LocalVarName + "Text";
					missing = $"{raw} == null";
					w.Line($"var {raw} = positionals.Count > {posIndex} ? positionals[{posIndex}] : null;");
					using (w.If(missing))
						EmitEnvironmentFallback(w, p, p.EnvVarName!, raw, p.LocalVarName + "EnvVal");
				}

				if (p.IsRequired)
				{
					using (w.If(missing))
					{
						w.Line($"Console.Error.WriteLine(\"Error: missing required argument <{Escape(p.CliLongName)}>.\");");
						if (helpMethodName is not null)
							w.Line($"{helpMethodName}();");
						w.Line(exit);
					}
				}
				else
					w.If(missing, $"{p.LocalVarName} = {p.DefaultValueLiteral ?? "default!"};");

				using (w.Else())
					EmitParseFromString(w, p, raw, p.LocalVarName, outVarKeyword: false, failure);

				posIndex++;
			}

			if (emitDtoTryParse)
			{
				EmitValidationChecks(w, cmd, failureExit, entryAssemblyName: null);
				if (dtoOptionsTypeFq is not null)
					EmitOptionsDtoConstructionAndReturn(w, dtoOptionsTypeFq, cmd.Parameters, dtoOptionsBestCtorParamOrder);
				else
					EmitAsParametersConstructionForDto(w, cmd);
			}
			else
				EmitCommandInvocationTail(w, cmd, globalMiddleware, injectedOptions, failure, entryAssemblyName, environmentPrefix);
		}

		w.Line();
	}

	/// <summary>
	/// Reads the flag's environment variable into <paramref name="text"/> when the command line left it out. A bool's
	/// variable is read as clap's <c>FalseyValueParser</c> reads it and becomes <c>"true"</c> or <c>"false"</c>.
	/// </summary>
	private static void EmitEnvironmentFallback(CodeWriter w, ParameterModel p, string envVar, string text, string envValue)
	{
		if (p.Special != BoolSpecialKind.None)
		{
			w.Line($"{text} = {ArghBoolBindingFq}.EnvironmentText({Quote(envVar)}, {BoolLiteral(p.EnvTreatEmptyAsUnset)});");
			return;
		}

		w.Line($"var {envValue} = global::System.Environment.GetEnvironmentVariable({Quote(envVar)});");
		w.Line(p.EnvTreatEmptyAsUnset
			? $"{text} = ({envValue} != null && {envValue}.Length > 0) ? {envValue} : null;"
			: $"{text} = {envValue};");
	}

	/// <summary>
	/// After binding: validation, the options locals, the handler instance, then the call, straight or through the
	/// middleware pipeline.
	/// </summary>
	private static void EmitCommandInvocationTail(
		CodeWriter w,
		CommandModel cmd,
		ImmutableArray<GlobalMiddlewareRegistration> globalMiddleware,
		ImmutableArray<(string TypeFq, string TypeMetadataName, ImmutableArray<string> AllBaseTypeMetadataNames, string StaticFieldName, string LocalVarName, ImmutableArray<ParameterModel> FlatMembers, ImmutableArray<string>? BestCtorParamOrder)> injectedOptions,
		ParseFailure failure,
		string? entryAssemblyName,
		string? environmentPrefix)
	{
		var failureExit = failure.Exit;
		var flagHelpStdErrMethodName = failure.FlagHelpMethod;
		var parseFailureRunHint = failure.RunHint;
		EmitAsParametersConstruction(w, cmd);

		EmitValidationChecks(w, cmd, failureExit, entryAssemblyName, flagHelpStdErrMethodName);

		// Reconstruct options instances merging command-level flags with pre-parsed statics.
		EmitOptionsReconstructLocals(w, injectedOptions, failure, environmentPrefix);
		EmitOptionsMemberValidation(w, cmd, injectedOptions, failureExit, entryAssemblyName, flagHelpStdErrMethodName);
		EmitRegisteredValidation(w, cmd, injectedOptions, failureExit, flagHelpStdErrMethodName, parseFailureRunHint);

		if (cmd.RequiresInstance)
		{
			// Try to construct from options-injected ctor parameters before falling back to DI or parameterless ctor.
			string? optionsCtorArgs = null;
			if (!injectedOptions.IsDefaultOrEmpty && !cmd.ContainingTypeCtorParams.IsDefaultOrEmpty)
			{
				var ctorParams = cmd.ContainingTypeCtorParams;
				if (ctorParams.Length > 0)
				{
					var ctorArgs = new List<string>();
					var allResolved = true;
					foreach (var (_, cpMetaName) in ctorParams)
					{
						// Exact match first, then most-derived (from end of chain); use LocalVarName (reconstructed)
						string? bestLocal = null;
						foreach (var o in injectedOptions)
							if (o.TypeMetadataName == cpMetaName) { bestLocal = o.LocalVarName; break; }
						if (bestLocal is null)
							for (var _i = injectedOptions.Length - 1; _i >= 0; _i--)
								if (injectedOptions[_i].AllBaseTypeMetadataNames.Contains(cpMetaName)) { bestLocal = injectedOptions[_i].LocalVarName; break; }
						if (bestLocal is null) { allResolved = false; break; }
						ctorArgs.Add(bestLocal);
					}
					if (allResolved)
						optionsCtorArgs = string.Join(", ", ctorArgs);
				}
			}

			var fallback = optionsCtorArgs is not null ? $"new {cmd.ContainingTypeFq}({optionsCtorArgs})"
				: cmd.ContainingTypeHasParameterlessCtor ? $"new {cmd.ContainingTypeFq}()"
				: "throw new global::System.InvalidOperationException(\"Register the command type in DI for hosted execution, or add a public parameterless constructor for standalone CLI.\")";
			w.Line($"var __cmdHandler = (ArghServices.ServiceProvider?.GetService(typeof({cmd.ContainingTypeFq})) as {cmd.ContainingTypeFq}) ?? {fallback};");
			w.Line();
		}

		w.Line();
		var useMiddleware = globalMiddleware.Length > 0 || cmd.CommandMiddlewareData.Length > 0;
		if (!useMiddleware)
		{
			EmitInvocation(w, cmd, injectedOptions: injectedOptions);
			return;
		}

		EmitCommandPathLiteral(w, cmd);
		EmitCommandArguments(w, cmd, injectedOptions);
		w.Line("var ctx = new CommandContext(commandPath, args, ct, __arguments);");
		if (flagHelpStdErrMethodName is not null || parseFailureRunHint is not null)
		{
			using (w.Block("ctx.UsageFooter = __flag =>", ";"))
			{
				if (flagHelpStdErrMethodName is not null)
				{
					using (w.If("__flag is not null"))
					{
						w.Line("Console.Error.WriteLine();");
						w.Line($"{flagHelpStdErrMethodName}(__flag);");
						w.Line("Console.Error.WriteLine();");
					}
				}

				if (parseFailureRunHint is not null)
					w.Line(ErrorLine(parseFailureRunHint));
			}
		}

		using (w.Block("CommandMiddlewareDelegate next = async c =>", ";"))
			EmitInvocation(w, cmd, "c.CancellationToken", "c", injectedOptions: injectedOptions);
		var middleware = new List<(string TypeFq, bool HasParameterlessCtor)>();
		for (var i = cmd.CommandMiddlewareData.Length - 1; i >= 0; i--)
			middleware.Add(cmd.CommandMiddlewareData[i]);
		for (var i = globalMiddleware.Length - 1; i >= 0; i--)
			middleware.Add((globalMiddleware[i].TypeFq, globalMiddleware[i].HasParameterlessCtor));
		for (var cap = 0; cap < middleware.Count; cap++)
		{
			var name = "__cap" + cap;
			w.Line($"var {name} = next;");
			w.Line($"next = async c => await {DiResolveOrNew(middleware[cap].TypeFq, middleware[cap].HasParameterlessCtor)}.InvokeAsync(c, {name});");
		}

		w.Line("await next(ctx).ConfigureAwait(false);");
		w.Return("ctx.ExitCode");
	}

	/// <summary>The bound option objects a handler receives: [AsParameters] objects and injected options, each with its local variable and member-to-flag pairs.</summary>
	private static List<(string Name, string VarName, List<(string Member, string Flag)> Flags)> EnumerateBoundObjects(CommandModel cmd, ImmutableArray<(string TypeFq, string TypeMetadataName, ImmutableArray<string> AllBaseTypeMetadataNames, string StaticFieldName, string LocalVarName, ImmutableArray<ParameterModel> FlatMembers, ImmutableArray<string>? BestCtorParamOrder)> injectedOptions)
	{
		var result = new List<(string, string, List<(string, string)>)>();
		if (!cmd.HandlerParamTypes.IsDefaultOrEmpty)
		{
			foreach (var mp in cmd.HandlerParamTypes)
			{
				if (!mp.IsAsParameters)
					continue;
				var group = cmd.Parameters.Where(p => p.AsParametersOwnerParamName == mp.Name && p.AsParametersClrName is not null && p.Kind != ParameterKind.Injected).ToArray();
				if (group.Length == 0)
					continue;
				result.Add((mp.Name, AsParametersConstructedVarName(mp.Name), group.Select(p => (p.AsParametersClrName!, p.CliLongName)).ToList()));
			}
		}

		if (!injectedOptions.IsDefaultOrEmpty)
		{
			foreach (var o in injectedOptions)
				result.Add((o.TypeMetadataName, o.LocalVarName, o.FlatMembers.Where(static m => m.Kind != ParameterKind.Injected).Select(static m => (m.SymbolName, m.CliLongName)).ToList()));
		}

		return result;
	}

	private static string MemberFlagsLiteral(List<(string Member, string Flag)> flags)
	{
		var sb = new StringBuilder("new global::System.Collections.Generic.Dictionary<string, string> { ");
		foreach (var (member, flag) in flags)
			sb.Append($"[\"{Escape(member)}\"] = \"{Escape(flag)}\", ");
		return sb.Append('}').ToString();
	}

	/// <summary>Emits <c>__arguments</c>: the bound objects with their member-to-flag map, for <c>CommandContext.Arguments</c>.</summary>
	private static void EmitCommandArguments(CodeWriter w, CommandModel cmd, ImmutableArray<(string TypeFq, string TypeMetadataName, ImmutableArray<string> AllBaseTypeMetadataNames, string StaticFieldName, string LocalVarName, ImmutableArray<ParameterModel> FlatMembers, ImmutableArray<string>? BestCtorParamOrder)> injectedOptions)
	{
		w.Line("var __arguments = new global::System.Collections.Generic.List<CommandArgument>();");
		foreach (var (name, varName, flags) in EnumerateBoundObjects(cmd, injectedOptions))
			w.Line($"__arguments.Add(new CommandArgument({Quote(name)}, {varName}, {MemberFlagsLiteral(flags)}));");
	}

	/// <summary>Runs registered <c>ArghValidation</c> validators over each bound object; on failure prints the flag's help rows and the run hint, then exits like a built-in check.</summary>
	private static void EmitRegisteredValidation(CodeWriter w, CommandModel cmd, ImmutableArray<(string TypeFq, string TypeMetadataName, ImmutableArray<string> AllBaseTypeMetadataNames, string StaticFieldName, string LocalVarName, ImmutableArray<ParameterModel> FlatMembers, ImmutableArray<string>? BestCtorParamOrder)> injectedOptions, string failureExit, string? flagHelpStdErrMethodName, string? parseFailureRunHint)
	{
		var bound = EnumerateBoundObjects(cmd, injectedOptions);
		if (bound.Count == 0)
			return;
		using (w.If("global::Nullean.Argh.Validation.ArghValidation.HasValidators"))
		{
			w.Line("string? __badFlag = null;");
			w.Line("var __valid = true;");
			foreach (var (_, varName, flags) in bound)
			{
				w.Line($"__valid &= global::Nullean.Argh.Validation.ArghValidation.Validate({varName}, {MemberFlagsLiteral(flags)}, out var __f_{varName});");
				w.Line($"__badFlag ??= __f_{varName};");
			}

			using (w.If("!__valid"))
			{
				if (flagHelpStdErrMethodName is not null)
				{
					using (w.If("__badFlag is not null"))
					{
						w.Line("Console.Error.WriteLine();");
						w.Line($"{flagHelpStdErrMethodName}(__badFlag);");
						w.Line("Console.Error.WriteLine();");
					}
				}

				if (parseFailureRunHint is not null)
					w.Line(ErrorLine(parseFailureRunHint));
				w.Line(failureExit + ";");
			}
		}

		w.Line();
	}

	private static void EmitCommandPathLiteral(CodeWriter w, CommandModel cmd) =>
		w.Line($"var commandPath = {InlineStringArray(cmd.RoutePrefix.Append(cmd.CommandName))};");

	private static void EmitCliValueDeclarations(CodeWriter w, CommandModel cmd, string? rtDefaultTypeFq = null)
	{
		// For cross-assembly options types, seed non-nullable properties from a runtime instance.
		// rtDefaultTypeFq covers UseGlobalOptions / UseNamespaceOptions DTO paths.
		// AsParametersTypeFq covers [AsParameters] init-property paths from cross-assembly types.
		var hasRtDefaults = rtDefaultTypeFq is not null && cmd.Parameters.Any(static p => p.UsesRuntimeDefault);
		if (hasRtDefaults)
			w.Line($"var __rt_default = new {rtDefaultTypeFq}();");

		// Emit one runtime-default instance per unique cross-assembly [AsParameters] type.
		var asParamsRtTypes = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var p in cmd.Parameters)
		{
			if (!p.UsesRuntimeDefault || p.AsParametersTypeFq is null)
				continue;
			if (asParamsRtTypes.ContainsKey(p.AsParametersTypeFq))
				continue;
			var suffix = DtoMethodSuffix(p.AsParametersTypeFq);
			asParamsRtTypes[p.AsParametersTypeFq] = suffix;
			w.Line($"var __rt_default_{suffix} = new {p.AsParametersTypeFq}();");
		}

		foreach (var p in cmd.Parameters)
		{
			if (p.Kind == ParameterKind.Injected || p.Kind == ParameterKind.OptionsInjected)
				continue;

			if (p.IsCollection && p.Kind == ParameterKind.Flag)
			{
				var accType = p.CollectionTargetIsReadOnlySet
					? "global::System.Collections.Generic.HashSet"
					: "global::System.Collections.Generic.List";
				w.Line($"var {p.LocalVarName}_acc = new {accType}<{GetElementCSharpFq(p)}>();");
				continue;
			}

			string initializer;
			if (p.UsesRuntimeDefault)
			{
				if (hasRtDefaults && rtDefaultTypeFq is not null)
					initializer = $"__rt_default.{p.SymbolName}";
				else if (p.AsParametersTypeFq is not null && asParamsRtTypes.TryGetValue(p.AsParametersTypeFq, out var asParamsSuffix))
					initializer = $"__rt_default_{asParamsSuffix}.{p.SymbolName}";
				else
					initializer = GetCliInitializer(p);
			}
			else
				initializer = GetCliInitializer(p);

			w.Line($"{GetCSharpCliType(p)} {p.LocalVarName} = {initializer};");
		}
	}

	private static string GetElementCSharpFq(ParameterModel p)
	{
		switch (p.ElementScalarKind)
		{
			case CliScalarKind.Enum when p.ElementEnumTypeFq is not null:
				return p.ElementEnumTypeFq;
			case CliScalarKind.FileInfo:
				return "global::System.IO.FileInfo";
			case CliScalarKind.DirectoryInfo:
				return "global::System.IO.DirectoryInfo";
			case CliScalarKind.Uri:
				return "global::System.Uri";
			case CliScalarKind.CustomParser or CliScalarKind.Parsable when p.ElementCustomValueTypeFq is not null:
				return p.ElementCustomValueTypeFq;
			default:
				break;
		}

		return p.ElementTypeName switch
		{
			"string" => "string",
			"int" => "int",
			"long" => "long",
			"float" => "float",
			"double" => "double",
			"decimal" => "decimal",
			"bool" => "bool",
			"DateTime" => "global::System.DateTime",
			"DateTimeOffset" => "global::System.DateTimeOffset",
			"TimeSpan" => "global::System.TimeSpan",
			"DateOnly" => "global::System.DateOnly",
			"TimeOnly" => "global::System.TimeOnly",
			"DateTime?" => "global::System.DateTime?",
			"DateTimeOffset?" => "global::System.DateTimeOffset?",
			"TimeSpan?" => "global::System.TimeSpan?",
			"DateOnly?" => "global::System.DateOnly?",
			"TimeOnly?" => "global::System.TimeOnly?",
			_ => "string"
		};
	}

	private static ParameterModel ForElementParsing(ParameterModel p) =>
		p with
		{
			ScalarKind = p.ElementScalarKind,
			TypeName = p.ElementTypeName,
			EnumTypeFq = p.ElementEnumTypeFq,
			EnumMemberNames = p.ElementEnumMemberNames,
			EnumMemberCliNames = p.ElementEnumMemberCliNames,
			ParserTypeFq = p.ElementParserTypeFq,
			CustomValueTypeFq = p.ElementCustomValueTypeFq,
			ParserValueTypeFq = p.ElementParserValueTypeFq,
			ParsableIsNumber = p.ElementParsableIsNumber,
			Special = BoolSpecialKind.None,
			IsCollection = false,
			IsRequired = true
		};

	private static void EmitVariadicPositionalParse(CodeWriter w, ParameterModel p, int startIndex, ParseFailure failure)
	{
		var countVar = ParamTemp(p, "varCount");
		var arrVar = ParamTemp(p, "arr");
		var vi = ParamTemp(p, "vi");
		var elemModel = ForElementParsing(p);

		// For [Argument] params T[] with no [MinLength], zero items is valid (C# params convention).
		// If the user added [MinLength(n)], CollectionCountConstraint validation below enforces at-least-n.
		// However if IsRequired is true (non-nullable, no default), we require at least 1.
		if (p.IsRequired)
		{
			using (w.If($"positionals.Count <= {startIndex}"))
			{
				w.Line($"Console.Error.WriteLine(\"Error: missing required argument <{Escape(p.CliLongName)}...>.\");");
				if (failure.HelpMethod is not null)
					w.Line($"{failure.HelpMethod}();");
				w.Line(failure.Exit + ";");
			}
		}

		w.Line($"var {countVar} = positionals.Count > {startIndex} ? positionals.Count - {startIndex} : 0;");
		w.Line($"var {arrVar} = new {GetCSharpCliType(elemModel)}[{countVar}];");
		using (w.For($"var {vi} = 0; {vi} < {countVar}; {vi}++"))
		{
			if (p.ElementScalarKind == CliScalarKind.Primitive && p.ElementTypeName == "string")
				w.Line($"{arrVar}[{vi}] = positionals[{startIndex} + {vi}];");
			else
				EmitParseFromString(w, elemModel, $"positionals[{startIndex} + {vi}]", $"{arrVar}[{vi}]", outVarKeyword: false, failure);
		}

		w.Line($"{p.LocalVarName} = {arrVar};");
	}

	private static string GetCSharpCliType(ParameterModel p)
	{
		if (p.ScalarKind == CliScalarKind.Collection && p.FullDeclaredTypeFq is not null)
			return p.FullDeclaredTypeFq;

		switch (p.ScalarKind)
		{
			case CliScalarKind.Enum when p.EnumTypeFq is not null:
				// Optional on the CLI but backed by a non-nullable enum + default (e.g. options properties): keep a non-nullable temp.
				// Also keep non-nullable for cross-assembly runtime-default properties (no null initial value).
				// IsNullableAnnotated guards against NRT nullable enums (e.g. MyEnum?) on cross-assembly types.
				if ((p.IsRequired || p.DefaultValueLiteral is not null || p.UsesRuntimeDefault) && !p.IsNullableAnnotated)
					return p.EnumTypeFq;
				return p.EnumTypeFq + "?";
			case CliScalarKind.FileInfo:
				return (p.IsRequired || p.UsesRuntimeDefault) && !p.IsNullableAnnotated ? "global::System.IO.FileInfo" : "global::System.IO.FileInfo?";
			case CliScalarKind.DirectoryInfo:
				return (p.IsRequired || p.UsesRuntimeDefault) && !p.IsNullableAnnotated ? "global::System.IO.DirectoryInfo" : "global::System.IO.DirectoryInfo?";
			case CliScalarKind.Uri:
				return (p.IsRequired || p.UsesRuntimeDefault) && !p.IsNullableAnnotated ? "global::System.Uri" : "global::System.Uri?";
			case CliScalarKind.CustomParser when p.CustomValueTypeFq is not null:
				return (p.IsRequired || p.UsesRuntimeDefault) && !p.IsNullableAnnotated || p.CustomValueTypeFq.EndsWith("?", StringComparison.Ordinal)
					? p.CustomValueTypeFq
					: p.CustomValueTypeFq + "?";
			// A Nullable<T> parameter keeps T? even when required; a T with a default value is T.
			case CliScalarKind.Parsable when p.CustomValueTypeFq is not null:
				return p.TypeName.EndsWith("?", StringComparison.Ordinal)
				       || (!p.IsRequired && !p.UsesRuntimeDefault && p.DefaultValueLiteral is null)
				       || p.IsNullableAnnotated
					? p.CustomValueTypeFq + "?"
					: p.CustomValueTypeFq;
			default:
				break;
		}

		if (p.TypeName == "string")
			return (p.IsRequired || p.UsesRuntimeDefault) && !p.IsNullableAnnotated ? "string" : "string?";

		return p.TypeName switch
		{
			"int" => "int",
			"int?" => "int?",
			"long" => "long",
			"long?" => "long?",
			"float" => "float",
			"float?" => "float?",
			"double" => "double",
			"double?" => "double?",
			"decimal" => "decimal",
			"decimal?" => "decimal?",
			"bool" => "bool",
			"bool?" => "bool?",
			"DateTime" => "global::System.DateTime",
			"DateTime?" => "global::System.DateTime?",
			"DateTimeOffset" => "global::System.DateTimeOffset",
			"DateTimeOffset?" => "global::System.DateTimeOffset?",
			"TimeSpan" => "global::System.TimeSpan",
			"TimeSpan?" => "global::System.TimeSpan?",
			"DateOnly" => "global::System.DateOnly",
			"DateOnly?" => "global::System.DateOnly?",
			"TimeOnly" => "global::System.TimeOnly",
			"TimeOnly?" => "global::System.TimeOnly?",
			_ => "string?"
		};
	}

	private static string GetCliInitializer(ParameterModel p)
	{
		if (p.DefaultValueLiteral is not null)
			return p.DefaultValueLiteral;

		if (p.ScalarKind == CliScalarKind.Collection)
			return "null!";

		if (!p.IsRequired && p.ScalarKind is CliScalarKind.Enum or CliScalarKind.FileInfo or CliScalarKind.DirectoryInfo
		    or CliScalarKind.Uri or CliScalarKind.CustomParser or CliScalarKind.Parsable)
			return "null";

		if (!p.IsRequired && p.TypeName.EndsWith("?", StringComparison.Ordinal))
			return "null";

		if (p.TypeName == "string")
			return p.IsRequired ? "null!" : "null";

		return "default!";
	}

	/// <summary>
	/// Global/namespace options flattened into a command via <see cref="FixOptionsParamsInCommands"/> use
	/// <see cref="ParameterKind.OptionsInjected"/> but must still participate in long-name aliases, short-option
	/// binding, and bare bool switch recognition the same as <see cref="ParameterKind.Flag"/>.
	/// </summary>
	private static bool IsEmittedFlagLike(ParameterKind kind) =>
		kind is ParameterKind.Flag or ParameterKind.OptionsInjected;

	private static void EmitBoolSwitchNames(CodeWriter w, CommandModel cmd)
	{
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var p in cmd.Parameters)
		{
			if (!IsEmittedFlagLike(p.Kind))
				continue;
			if (p.Special is BoolSpecialKind.Bool or BoolSpecialKind.NullableBool)
				names.Add(p.CliLongName);
			if (p.Special == BoolSpecialKind.NullableBool)
				names.Add("no-" + p.CliLongName);
		}

		EmitNamePredicate(w, "bool IsBoolSwitchName(string name)", names);
	}

	private static void EmitKnownNonBoolFlagNames(CodeWriter w, CommandModel cmd)
	{
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var p in cmd.Parameters)
		{
			if (!IsEmittedFlagLike(p.Kind))
				continue;
			if (p.Special == BoolSpecialKind.Bool || p.Special == BoolSpecialKind.NullableBool)
				continue;
			names.Add(p.CliLongName);
			foreach (var al in p.Aliases)
				names.Add(al);
		}

		EmitNamePredicate(w, "bool IsKnownNonBoolFlagName(string name)", names);
	}

	private static void EmitCanonFlagNameMethod(CodeWriter w, CommandModel cmd)
	{
		var cases = new List<(string from, string to)>();
		foreach (var p in cmd.Parameters)
		{
			if (!IsEmittedFlagLike(p.Kind))
				continue;
			foreach (var al in p.Aliases)
			{
				if (string.Equals(al, p.CliLongName, StringComparison.OrdinalIgnoreCase))
					continue;
				cases.Add((al, p.CliLongName));
			}

			if (p.Special == BoolSpecialKind.NullableBool)
			{
				foreach (var al in p.Aliases)
				{
					if (string.Equals(al, p.CliLongName, StringComparison.OrdinalIgnoreCase))
						continue;
					cases.Add(("no-" + al, "no-" + p.CliLongName));
				}
			}
		}

		const string signature = "string CanonFlagName(string raw)";
		if (cases.Count == 0)
			w.Line(signature + " => raw;");
		else
			EmitSwitchExpression(w, signature, "raw", cases.Select(static c => (Quote(c.from), Quote(c.to))), "raw");
	}

	private static void EmitShortFlagMethods(CodeWriter w, CommandModel cmd, bool multiFlagsAvailable = true, string? parseFailureRunHint = null)
	{
		var shortCases = new List<(char c, string Primary, bool IsBool, bool IsRepeatableCollection)>();
		foreach (var p in cmd.Parameters)
		{
			if (!IsEmittedFlagLike(p.Kind))
				continue;
			if (p.ShortOpt is not char ch)
				continue;
			// IsRepeatableCollection only applies when multiFlags is available in the emitted context.
			var isRepeatableCollection = multiFlagsAvailable && p.IsCollection && p.CollectionSeparator is null;
			shortCases.Add((ch, p.CliLongName, p.Special != BoolSpecialKind.None, isRepeatableCollection));
		}

		void UnknownShortOption()
		{
			w.Line("Console.Error.WriteLine($\"Error: unknown short option '-{c}'.\");");
			if (parseFailureRunHint is not null)
			{
				w.Line("Console.Error.WriteLine();");
				w.Line($"Console.Error.WriteLine({Quote(parseFailureRunHint)});");
			}

			w.Return("false");
		}

		using (w.Block("bool TryApplyShortFlag(char c, string val)"))
		{
			if (shortCases.Count == 0)
				UnknownShortOption();
			else
			{
				using (w.Switch("c"))
				{
					foreach ((var c, var primary, _, var isRepeatableCol) in shortCases)
					{
						var esc = Escape(primary);
						using (w.Case($"'{c}'"))
						{
							if (isRepeatableCol)
							{
								// Repeatable flag: append to multiFlags so short opt and long opt collect into the same list.
								var list = "__scList_" + esc.Replace("-", "_");
								w.Line($"if (!multiFlags.TryGetValue(\"{esc}\", out var {list})) {{ {list} = new List<string>(); multiFlags[\"{esc}\"] = {list}; }}");
								w.Line($"{list}.Add(val);");
							}
							else
								w.Line($"flags[\"{esc}\"] = val;");
							w.Return("true");
						}
					}

					using (w.Case("default"))
						UnknownShortOption();
				}
			}
		}

		var boolChars = shortCases.Where(static x => x.IsBool).Select(static x => $"'{x.c}'").ToList();
		if (boolChars.Count == 0)
			w.Line("bool IsShortBoolChar(char c) => false;");
		else
			EmitSwitchPredicate(w, "bool IsShortBoolChar(char c)", "c", boolChars);
	}

	private static void EmitParseAndAssign(CodeWriter w, ParameterModel p, string rawExpr, string targetVar, ParseFailure failure)
	{
		if (!p.IsRequired && p.DefaultValueLiteral is not null)
		{
			w.If($"{rawExpr} is null", $"{targetVar} = {p.DefaultValueLiteral};");
			using (w.Else())
				EmitParseFromString(w, p, rawExpr, targetVar, outVarKeyword: false, failure);
		}
		else if (!p.IsRequired && p.DefaultValueLiteral is null)
		{
			// Optional parameter with no explicit default: only parse when a value was actually provided.
			// Guards against passing null into type-specific parsers (e.g. Enum.TryParse) when the flag is absent.
			using (w.If($"{rawExpr} is not null"))
				EmitParseFromString(w, p, rawExpr, targetVar, outVarKeyword: false, failure);
		}
		else
			EmitParseFromString(w, p, rawExpr, targetVar, outVarKeyword: false, failure);
	}

	/// <summary>
	/// Parses <paramref name="rawExpr"/> into <paramref name="targetVar"/>, declaring it when <paramref name="outVarKeyword"/>
	/// is set; a value that doesn't parse prints an error and leaves through <paramref name="failure"/>.
	/// </summary>
	private static void EmitParseFromString(CodeWriter w, ParameterModel p, string rawExpr, string targetVar, bool outVarKeyword, ParseFailure failure)
	{
		if (p.ScalarKind == CliScalarKind.Enum && p.EnumTypeFq is not null && !p.EnumMemberNames.IsDefaultOrEmpty)
			EmitEnumParseFromString(w, p, rawExpr, targetVar, outVarKeyword, failure);
		else if (p.ScalarKind == CliScalarKind.FileInfo)
			EmitFileSystemInfoParseFromString(w, p, rawExpr, targetVar, outVarKeyword, "global::System.IO.FileInfo", "nullableFileInfo", "path");
		else if (p.ScalarKind == CliScalarKind.DirectoryInfo)
			EmitFileSystemInfoParseFromString(w, p, rawExpr, targetVar, outVarKeyword, "global::System.IO.DirectoryInfo", "nullableDirectoryInfo", "dir");
		else if (p.ScalarKind == CliScalarKind.Uri)
			EmitUriParseFromString(w, p, rawExpr, targetVar, outVarKeyword, failure);
		else if (p.ScalarKind == CliScalarKind.CustomParser && p.ParserTypeFq is not null && p.CustomValueTypeFq is not null)
			EmitCustomParserFromString(w, p, rawExpr, targetVar, outVarKeyword, failure);
		// A Nullable<T> goes through the same lifting as the built-in value types.
		else if (p.ScalarKind == CliScalarKind.Parsable && p.CustomValueTypeFq is not null && !p.TypeName.EndsWith("?", StringComparison.Ordinal))
			EmitParsableFromString(w, p, rawExpr, targetVar, outVarKeyword, failure);
		else if (p.ScalarKind == CliScalarKind.Parsable && p.CustomValueTypeFq is not null
		         || p.Special == BoolSpecialKind.None && IsLiftableNullable(p.TypeName))
			EmitLiftedNullableParseFromString(w, p, rawExpr, targetVar, outVarKeyword, failure);
		else
			EmitPrimitiveScalarParseFromString(w, p, rawExpr, targetVar, outVarKeyword, failure);
	}

	/// <summary><c>var target = value;</c> when declaring the target, <c>target = value;</c> otherwise.</summary>
	private static void EmitAssign(CodeWriter w, bool declare, string targetVar, string value) =>
		w.Line(declare ? $"var {targetVar} = {value};" : $"{targetVar} = {value};");

	/// <summary>The error for a value that doesn't parse as <paramref name="what"/>, quoting the raw text.</summary>
	private static string InvalidValueError(string what, ParameterModel p, string rawExpr) =>
		$"Console.Error.WriteLine($\"Error: invalid {what} for {ErrorArgName(p)}: '{{{rawExpr}}}'.\");";

	private const string ArghBoolBindingFq = "global::Nullean.Argh.Runtime.ArghBoolBinding";

	/// <summary>A command-line boolean parse (<c>ArghBool.TryParseBoolish</c>), the same for flags, positionals and elements.</summary>
	private static string BoolishParseCall(string rawExpr, string outArg) =>
		$"global::Nullean.Argh.ArghBool.TryParseBoolish({rawExpr}, {outArg})";

	private static string BoolInvalidValueError(ParameterModel p, string rawExpr) =>
		$"Console.Error.WriteLine({ArghBoolBindingFq}.InvalidValueError(\"{ErrorArgName(p)}\", {rawExpr}));";

	/// <summary>A bool's environment variable read as clap's <c>FalseyValueParser</c> reads it: a <c>bool?</c>, null when unset.</summary>
	private static string BoolFromEnvironmentCall(ParameterModel p, string envVar) =>
		$"{ArghBoolBindingFq}.FromEnvironment({Quote(envVar)}, {BoolLiteral(p.EnvTreatEmptyAsUnset)})";

	/// <summary>
	/// Binds a <c>bool</c> or <c>bool?</c> flag into <paramref name="target"/>, which already holds the value to keep when
	/// the flag isn't on the command line: <c>--name[=value]</c> (and <c>--no-name[=value]</c> for <c>bool?</c>) wins, then
	/// <paramref name="fallback"/> when given (such as an environment variable, itself a <c>bool?</c>). A value that isn't
	/// a boolean fails.
	/// </summary>
	private static void EmitBoolFlagBinding(CodeWriter w, ParameterModel p, string target, ParseFailure failure, string? fallback = null)
	{
		var cli = target + "Cli";
		var error = target + "Err";
		var negatable = BoolLiteral(p.Special == BoolSpecialKind.NullableBool);
		EmitParseError(w, $"!{ArghBoolBindingFq}.TryGetFlag(flags, {Quote(p.CliLongName)}, {negatable}, out var {cli}, out var {error})",
			$"Console.Error.WriteLine({error});", p, failure);
		w.Line(fallback is null ? $"{target} = {cli} ?? {target};" : $"{target} = {cli} ?? {fallback} ?? {target};");
	}

	private static void EmitEnumParseFromString(CodeWriter w, ParameterModel p, string rawExpr, string targetVar, bool outVarKeyword, ParseFailure failure)
	{
		var evVar = ParamTemp(p, "ev");
		var evParsed = ParamTemp(p, "evp");
		w.Line($"var {evParsed} = false;");
		w.Line($"{p.EnumTypeFq} {evVar} = default;");
		using (w.Switch($"({rawExpr} ?? \"\").ToLowerInvariant()"))
		{
			for (var i = 0; i < p.EnumMemberNames.Length; i++)
			{
				var memberName = p.EnumMemberNames[i];
				var cliName = ResolveEnumMemberCliName(p.EnumMemberCliNames, i, memberName);
				w.Line($"case {Quote(cliName.ToLowerInvariant())}: {evVar} = {p.EnumTypeFq}.{memberName}; {evParsed} = true; break;");
			}
		}

		EmitParseError(w, $"!{evParsed}", InvalidValueError("value", p, rawExpr), p, failure);
		EmitAssign(w, outVarKeyword, targetVar, evVar);
	}

	/// <summary>A <c>FileInfo</c> or <c>DirectoryInfo</c>, after expanding <c>~</c> when the parameter asks for it.</summary>
	private static void EmitFileSystemInfoParseFromString(CodeWriter w, ParameterModel p, string rawExpr, string targetVar, bool outVarKeyword,
		string infoTypeFq, string nullableTemp, string pathTemp)
	{
		string PathSource()
		{
			if (!p.ExpandUserProfileBeforeBind)
				return $"{rawExpr}!";
			var expanded = ParamTemp(p, pathTemp);
			w.Line($"var {expanded} = global::Nullean.Argh.ArghPath.ExpandUserProfilePath({rawExpr}!);");
			return expanded;
		}

		// An optional one must stay null when the flag was not provided, not pass null into the constructor (ArgumentNullException).
		if (!p.IsRequired)
		{
			var tmp = ParamTemp(p, nullableTemp);
			w.Line($"{GetCSharpCliType(p)} {tmp} = null;");
			using (w.If($"!string.IsNullOrWhiteSpace({rawExpr})"))
			{
				var optionalSource = PathSource();
				w.Line($"{tmp} = new {infoTypeFq}({optionalSource});");
			}

			EmitAssign(w, outVarKeyword, targetVar, tmp);
			return;
		}

		var source = PathSource();
		EmitAssign(w, outVarKeyword, targetVar, $"new {infoTypeFq}({source})");
	}

	private static void EmitUriParseFromString(CodeWriter w, ParameterModel p, string rawExpr, string targetVar, bool outVarKeyword, ParseFailure failure)
	{
		// Per-parameter name: two Uri parameters in one method must not both declare the same local.
		var uriOut = ParamTemp(p, "uri");
		var tryCreateFails = $"!global::System.Uri.TryCreate({rawExpr}, global::System.UriKind.RelativeOrAbsolute, out var {uriOut})";
		// Optional Uri? must treat omitted flags as null (raw text is null), not run Uri.TryCreate on null/whitespace.
		if (!p.IsRequired)
		{
			var tmpUri = ParamTemp(p, "nullableUriParsed");
			w.Line($"{GetCSharpCliType(p)} {tmpUri} = null;");
			using (w.If($"!string.IsNullOrWhiteSpace({rawExpr})"))
			{
				EmitParseError(w, tryCreateFails, InvalidValueError("URI", p, rawExpr), p, failure);
				w.Line($"{tmpUri} = {uriOut};");
			}

			EmitAssign(w, outVarKeyword, targetVar, tmpUri);
			return;
		}

		EmitParseError(w, tryCreateFails, InvalidValueError("URI", p, rawExpr), p, failure);
		EmitAssign(w, outVarKeyword, targetVar, uriOut);
	}

	/// <summary>
	/// The type of the local that holds a custom parser: the <c>IArgumentParser&lt;T&gt;</c> it's called through, which picks
	/// the right <c>TryParse</c> when the parser implements both <c>IArgumentParser&lt;T&gt;</c> and <c>IArgumentParser&lt;T?&gt;</c>,
	/// or <c>var</c> when it implements neither.
	/// </summary>
	private static string CustomParserLocalType(ParameterModel p) =>
		p.ParserValueTypeFq is { } parsed ? $"global::Nullean.Argh.Parsing.IArgumentParser<{parsed}>" : "var";

	/// <summary>True when a <c>T?</c> value is parsed through the parser's <c>IArgumentParser&lt;T&gt;</c>.</summary>
	private static bool IsLiftedCustomParser(ParameterModel p) =>
		p.ParserValueTypeFq is { } parsed && p.CustomValueTypeFq is { } declared && parsed + "?" == declared;

	private static void EmitCustomParserFromString(CodeWriter w, ParameterModel p, string rawExpr, string targetVar, bool outVarKeyword, ParseFailure failure)
	{
		var parser = ParamTemp(p, "parser");
		var parsed = ParamTemp(p, "pv");
		w.Line($"{CustomParserLocalType(p)} {parser} = new {p.ParserTypeFq}();");
		EmitParseError(w, $"!{parser}.TryParse({rawExpr}!, {ParserOptionsExpr}, out var {parsed})",
			$"Console.Error.WriteLine($\"Error: invalid value for {ErrorArgName(p)}.\");", p, failure);
		// A T? value parsed through IArgumentParser<T> is converted, so a var target is T?.
		EmitAssign(w, outVarKeyword, targetVar, IsLiftedCustomParser(p) ? $"({p.CustomValueTypeFq}){parsed}" : parsed);
	}

	/// <summary>
	/// Parses an <c>IParsable&lt;T&gt;</c> value through a constrained generic helper on the generated root type
	/// (<c>TryParseParsable&lt;T&gt;</c>, or <c>TryParseNumberBase&lt;T&gt;</c> for number types), so types that implement the
	/// interface explicitly work as well as ones with a public <c>TryParse</c>. <c>T?</c> reaches this through the nullable lifting.
	/// </summary>
	private static void EmitParsableFromString(CodeWriter w, ParameterModel p, string rawExpr, string targetVar, bool outVarKeyword, ParseFailure failure)
	{
		var parsed = ParamTemp(p, "pv");
		EmitParseError(w, $"!{ParsableTryParseCall(p, rawExpr, "out var " + parsed)}",
			InvalidValueError(Escape(p.TypeName.TrimEnd('?')), p, rawExpr), p, failure);
		EmitAssign(w, outVarKeyword, targetVar, parsed);
	}

	/// <summary>The temp name for a primitive parsed into a local before it's assigned, or <see langword="null"/> when it's parsed straight into the target.</summary>
	private static string? PrimitiveParseTemp(string typeName) => typeName switch
	{
		"DateTime" => "dt",
		"DateTimeOffset" => "dto",
		"TimeSpan" => "ts",
		"DateOnly" => "do",
		"TimeOnly" => "to",
		_ => null,
	};

	private static void EmitPrimitiveScalarParseFromString(CodeWriter w, ParameterModel p, string rawExpr, string targetVar, bool outVarKeyword, ParseFailure failure)
	{
		var type = p.TypeName;
		// A bool positional or collection element: the same spellings as a bool flag's value.
		if (p.Special != BoolSpecialKind.None || type == "bool")
		{
			var tmp = ParamTemp(p, "bool");
			EmitParseError(w, $"!{BoolishParseCall(rawExpr, "out var " + tmp)}", BoolInvalidValueError(p, rawExpr), p, failure);
			EmitAssign(w, outVarKeyword, targetVar, tmp);
			return;
		}

		if (type == "string")
		{
			EmitAssign(w, outVarKeyword, targetVar, outVarKeyword || !p.IsRequired ? rawExpr : rawExpr + "!");
			return;
		}

		if (type is "int" or "long" or "float" or "double" or "decimal")
		{
			var outArg = outVarKeyword ? "out var " + targetVar : "out " + targetVar;
			EmitParseError(w, $"!{ScalarTryParseCall(type, rawExpr, outArg)}", InvalidValueError(type, p, rawExpr), p, failure);
			return;
		}

		if (PrimitiveParseTemp(type) is { } tempSuffix)
		{
			var tmp = ParamTemp(p, tempSuffix);
			EmitParseError(w, $"!{ScalarTryParseCall(type, rawExpr, "out var " + tmp)}", InvalidValueError(type, p, rawExpr), p, failure);
			EmitAssign(w, outVarKeyword, targetVar, tmp);
			return;
		}

		if (outVarKeyword)
			w.Line($"var {targetVar} = {rawExpr};");
		else
			w.Line($"{targetVar} = {rawExpr}; // fallback");
	}

	/// <summary>
	/// Parses <c>T?</c> exactly as <c>T</c>: the value stays <see langword="null"/> when absent, and otherwise goes through
	/// <c>T</c>'s own parse code, with the same styles, culture and error message.
	/// </summary>
	private static void EmitLiftedNullableParseFromString(CodeWriter w, ParameterModel p, string rawExpr, string targetVar, bool outVarKeyword, ParseFailure failure)
	{
		var lifted = ParamTemp(p, "lifted");
		var value = ParamTemp(p, "value");
		w.Line($"{GetCSharpCliType(p)} {lifted} = null;");
		using (w.If($"{rawExpr} is not null"))
		{
			var inner = p with { TypeName = p.TypeName.TrimEnd('?') };
			if (p.ScalarKind == CliScalarKind.Parsable)
				EmitParsableFromString(w, inner, rawExpr, value, outVarKeyword: true, failure);
			else
				EmitPrimitiveScalarParseFromString(w, inner, rawExpr, value, outVarKeyword: true, failure);
			w.Line($"{lifted} = {value};");
		}

		EmitAssign(w, outVarKeyword, targetVar, lifted);
	}

	/// <summary>
	/// Calls the handler and turns its result into the exit code: <c>return</c> it, or with middleware
	/// (<paramref name="commandContextVar"/>) store it on the context. <paramref name="awaited"/> awaits a task, and
	/// <paramref name="returnsExitCode"/> uses the handler's <c>int</c> result rather than 0.
	/// </summary>
	private static void EmitHandlerCall(CodeWriter w, string call, bool awaited, bool returnsExitCode, string? commandContextVar)
	{
		var expr = awaited ? $"await {call}.ConfigureAwait(false)" : call;
		if (!returnsExitCode)
		{
			w.Line(expr + ";");
			expr = "0";
		}

		if (commandContextVar is null)
			w.Return(expr);
		else
		{
			w.Line($"{commandContextVar}.ExitCode = {expr};");
			w.Line("return;");
		}
	}

	private static void EmitInvocation(
		CodeWriter w,
		CommandModel cmd,
		string ctExpr = "ct",
		string? commandContextVar = null,
		ImmutableArray<(string TypeFq, string TypeMetadataName, ImmutableArray<string> AllBaseTypeMetadataNames, string StaticFieldName, string LocalVarName, ImmutableArray<ParameterModel> FlatMembers, ImmutableArray<string>? BestCtorParamOrder)> injectedOptions = default)
	{
		// Lambda commands: invoke through ArghApp.GetRegisteredLambda with a cast
		if (cmd.IsLambda && !string.IsNullOrEmpty(cmd.LambdaStorageKey))
		{
			EmitLambdaInvocation(w, cmd, ctExpr, commandContextVar);
			return;
		}

		var args = new List<string>();
		if (cmd.HandlerParamTypes.IsDefaultOrEmpty)
		{
			foreach (var p in cmd.Parameters)
			{
				if (p.Kind == ParameterKind.Injected)
					args.Add(ctExpr);
				else if (p.Kind != ParameterKind.OptionsInjected)
					// OptionsInjected entries are flag-recognition markers added by FixOptionsParamsInCommands;
					// they are not method arguments — the options instance is passed as a reconstructed local.
					args.Add(p.LocalVarName);
			}
		}
		else
		{
			foreach (var mp in cmd.HandlerParamTypes)
			{
				if (mp.IsInjectedParam)
				{
					args.Add(ctExpr);
					continue;
				}

				if (mp.IsAsParameters)
				{
					args.Add(AsParametersConstructedVarName(mp.Name));
					continue;
				}

				// Options-type parameters are injected as locally-reconstructed instances that merge
				// command-level flags (post-command) with pre-parsed static values (pre-command).
				if (!injectedOptions.IsDefaultOrEmpty)
				{
					string? localVar = null;
					foreach (var o in injectedOptions)
						if (o.TypeMetadataName == mp.TypeMetadataName) { localVar = o.LocalVarName; break; }
					if (localVar is null)
						for (var _i = injectedOptions.Length - 1; _i >= 0; _i--)
							if (injectedOptions[_i].AllBaseTypeMetadataNames.Contains(mp.TypeMetadataName)) { localVar = injectedOptions[_i].LocalVarName; break; }

					if (localVar is not null)
					{
						args.Add(localVar);
						continue;
					}
				}

				foreach (var p in cmd.Parameters)
				{
					if (p.AsParametersOwnerParamName is not null)
						continue;
					if (p.SymbolName != mp.Name)
						continue;
					args.Add(p.LocalVarName);
					break;
				}
			}
		}

		var argList = string.Join(", ", args);
		var call = cmd.RequiresInstance
			? $"__cmdHandler.{cmd.MethodName}({argList})"
			: $"{cmd.ContainingTypeFq}.{cmd.MethodName}({argList})";

		// Note: ReturnTypeFq comes from SymbolDisplayFormat.FullyQualifiedFormat, which renders special
		// types using their C# keyword ("void", "int") rather than "global::System.Void"/"global::System.Int32" —
		// keep these checks in that form (see also the "int" checks inside Task<int>/ValueTask<int> below).
		// An empty string means no return type info (shouldn't happen for method handlers).
		var retFq = cmd.ReturnTypeFq;
		var awaited = retFq is "global::System.Threading.Tasks.Task" or "global::System.Threading.Tasks.ValueTask"
			|| retFq.StartsWith("global::System.Threading.Tasks.Task<", StringComparison.Ordinal)
			|| retFq.StartsWith("global::System.Threading.Tasks.ValueTask<", StringComparison.Ordinal);
		var returnsExitCode = retFq is "int" or "global::System.Threading.Tasks.Task<int>" or "global::System.Threading.Tasks.ValueTask<int>";
		EmitHandlerCall(w, call, awaited, returnsExitCode, commandContextVar);
	}

	private static void EmitLambdaInvocation(
		CodeWriter w,
		CommandModel cmd,
		string ctExpr,
		string? commandContextVar)
	{
		var lambdaArgs = new List<string>();
		foreach (var p in cmd.Parameters)
		{
			if (p.Kind == ParameterKind.Injected)
				lambdaArgs.Add(ctExpr);
			else
				lambdaArgs.Add(p.LocalVarName);
		}
		var lambdaArgList = string.Join(", ", lambdaArgs);
		var castType = string.IsNullOrEmpty(cmd.LambdaDelegateFq) || cmd.LambdaDelegateFq == "global::System.Delegate"
			? "global::System.Delegate"
			: cmd.LambdaDelegateFq;

		if (castType == "global::System.Delegate")
		{
			// Fallback: use DynamicInvoke
			w.Line($"var __lambdaDelegate = ArghApp.GetRegisteredLambda({Quote(cmd.LambdaStorageKey)});");
			EmitHandlerCall(w, $"__lambdaDelegate?.DynamicInvoke({lambdaArgList})", awaited: false, returnsExitCode: false, commandContextVar);
			return;
		}

		var lambdaRetFq = cmd.ReturnTypeFq;
		var lambdaIsTaskOfInt = lambdaRetFq == "global::System.Threading.Tasks.Task<int>"
			|| lambdaRetFq == "global::System.Threading.Tasks.ValueTask<int>";
		var awaited = lambdaIsTaskOfInt
			|| lambdaRetFq == "global::System.Threading.Tasks.Task"
			|| lambdaRetFq.StartsWith("global::System.Threading.Tasks.Task<", StringComparison.Ordinal);
		w.Line($"var __lambdaDelegate = (({castType})ArghApp.GetRegisteredLambda({Quote(cmd.LambdaStorageKey)})!);");
		EmitHandlerCall(w, $"__lambdaDelegate({lambdaArgList})", awaited, returnsExitCode: lambdaIsTaskOfInt || lambdaRetFq == "int", commandContextVar);
	}

	/// <summary>A nullable value type whose non-nullable form has its own parse code in <see cref="EmitPrimitiveScalarParseFromString"/>.</summary>
	private static bool IsLiftableNullable(string typeName) =>
		typeName.EndsWith("?", StringComparison.Ordinal)
		&& typeName.TrimEnd('?') is "int" or "long" or "float" or "double" or "decimal"
			or "DateTime" or "DateTimeOffset" or "TimeSpan" or "DateOnly" or "TimeOnly";

	private static IEnumerable<ParameterModel> EnumerateFlagMembers(OptionsTypeModel? model)
	{
		if (model is null)
			yield break;

		foreach (var p in model.Members)
		{
			if (p.Kind == ParameterKind.Flag && !p.IsHidden)
				yield return p;
		}
	}

	private static void AddCliKeys(IEnumerable<ParameterModel> flags, HashSet<string> keys)
	{
		foreach (var p in flags)
		{
			keys.Add(p.CliLongName);
			foreach (var a in p.Aliases)
			{
				if (!string.IsNullOrEmpty(a))
					keys.Add(a);
			}
		}
	}

	private static List<(string Segment, OptionsTypeModel Model)> GetCommandNamespaceOptionChain(AppEmitModel app, ImmutableArray<string> routePrefix)
	{
		var list = new List<(string, OptionsTypeModel)>();
		var current = app.Root;
		foreach (var seg in routePrefix)
		{
			RegistryNode.NamedCommandNamespaceChild? found = null;
			foreach (var c in current.Children)
			{
				if (string.Equals(c.Segment, seg, StringComparison.OrdinalIgnoreCase))
				{
					found = c;
					break;
				}
			}

			if (found is null)
				break;

			current = found.Node;
			if (current.CommandNamespaceOptionsModel is { Members: { Length: > 0 } } gom)
				list.Add((seg, gom));
		}

		return list;
	}

	private static bool CommandFlagMatchesScopedKeys(ParameterModel p, HashSet<string> scopedKeys)
	{
		if (scopedKeys.Contains(p.CliLongName))
			return true;

		foreach (var a in p.Aliases)
		{
			if (!string.IsNullOrEmpty(a) && scopedKeys.Contains(a))
				return true;
		}

		return false;
	}


	/// <summary>
	/// Diagnoses misuse of the filesystem-path attribute family. <paramref name="filesystemScalarKind"/> lets
	/// collection call sites pass the *element* kind (e.g. <c>FileInfo</c> for <c>List&lt;FileInfo&gt;</c>) so these
	/// attributes are correctly recognized on collections of FileInfo/DirectoryInfo, not just scalars.
	/// </summary>
	private static void ReportFilesystemPathAttributeIssues(
		ISymbol host,
		CliScalarKind scalarKind,
		string declaredName,
		DiagnosticAccumulator? acc,
		Location? fallbackLocation,
		CliScalarKind? filesystemScalarKind = null)
	{
		Location loc = host.Locations.FirstOrDefault() ?? fallbackLocation ?? Location.None;
		var fsKind = filesystemScalarKind ?? scalarKind;

		var hasExisting = false;
		var hasNonExisting = false;
		var hasExpandProfile = false;
		var hasRejectSymlinks = false;
		var hasFileExtensions = false;

		foreach (var attr in host.GetAttributes())
		{
			var fqn = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "";
			switch (fqn)
			{
				case "global::Nullean.Argh.ExistingAttribute":
					hasExisting = true;
					break;
				case "global::Nullean.Argh.NonExistingAttribute":
					hasNonExisting = true;
					break;
				case "global::Nullean.Argh.ExpandUserProfileAttribute":
					hasExpandProfile = true;
					break;
				case "global::Nullean.Argh.RejectSymbolicLinksAttribute":
					hasRejectSymlinks = true;
					break;
				case "global::System.ComponentModel.DataAnnotations.FileExtensionsAttribute":
					hasFileExtensions = true;
					break;
			}
		}

		if (hasExisting && hasNonExisting)
			acc?.Add(PathExistenceAttributesConflict, loc, declaredName);

		var isFileInfo = fsKind == CliScalarKind.FileInfo;
		var isDirInfo = fsKind == CliScalarKind.DirectoryInfo;
		var isFileOrDir = isFileInfo || isDirInfo;

		if (hasExisting && !isFileOrDir)
			acc?.Add(FilesystemPathAttributeTypeMismatch, loc, declaredName,
				"[Existing] only applies to FileInfo, FileInfo?, DirectoryInfo, DirectoryInfo?, or a collection of FileInfo/DirectoryInfo parameters and properties.");

		if (hasNonExisting && !isFileOrDir)
			acc?.Add(FilesystemPathAttributeTypeMismatch, loc, declaredName,
				"[NonExisting] only applies to FileInfo, FileInfo?, DirectoryInfo, DirectoryInfo?, or a collection of FileInfo/DirectoryInfo parameters and properties.");

		if (hasExpandProfile && !isFileOrDir)
			acc?.Add(FilesystemPathAttributeTypeMismatch, loc, declaredName,
				"[ExpandUserProfile] only applies to FileInfo, DirectoryInfo, or a collection of FileInfo/DirectoryInfo parameters and properties.");

		if (hasRejectSymlinks && !isFileOrDir)
			acc?.Add(FilesystemPathAttributeTypeMismatch, loc, declaredName,
				"[RejectSymbolicLinks] only applies to FileInfo, DirectoryInfo, or a collection of FileInfo/DirectoryInfo parameters and properties.");

		if (hasFileExtensions && !isFileInfo)
			acc?.Add(FilesystemPathAttributeTypeMismatch, loc, declaredName,
				"[FileExtensions] only applies to FileInfo, FileInfo?, or a collection of FileInfo parameters and properties.");
	}

	private static bool TryReadExpandUserProfileBeforeBind(ISymbol host, CliScalarKind scalarKind)
	{
		foreach (var attr in host.GetAttributes())
		{
			if (attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
			    "global::Nullean.Argh.ExpandUserProfileAttribute")
			{
				return scalarKind is CliScalarKind.FileInfo or CliScalarKind.DirectoryInfo;
			}
		}

		return false;
	}


	/// <summary>
	/// Reads DataAnnotations/Argh validation attributes off <paramref name="attributeHost"/>.
	/// </summary>
	/// <param name="filesystemScalarKind">
	/// Kind used to gate the filesystem-path family ([Existing], [NonExisting], [RejectSymbolicLinks], [FileExtensions]).
	/// For scalar parameters this equals <paramref name="scalarKind"/> (the default when null). For collection
	/// parameters (<c>List&lt;FileInfo&gt;</c>, <c>DirectoryInfo[]</c>, ...) callers pass the *element* kind here so
	/// these attributes are recognized per-item while <paramref name="scalarKind"/> stays <see cref="CliScalarKind.Collection"/>
	/// for the other (non filesystem-family) constraint decisions such as [Url] vs Uri-scheme.
	/// </param>
}
