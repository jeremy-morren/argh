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
	private sealed record DtoBindingTarget(
		string TypeFq,
		ImmutableArray<ParameterModel> Members,
		bool IsOptionsDto,
		bool IsGeneric,
		bool IsPublic,
		ImmutableArray<string>? BestCtorParamOrder);

	private static ImmutableArray<DtoBindingTarget> CollectDtoBindingTargets(
		AppEmitModel app)
	{
		// Use string TypeFq as dedup key since we no longer have INamedTypeSymbol in the pipeline boundary.
		var map = new Dictionary<string, DtoBindingTarget>(StringComparer.Ordinal);

		if (app.GlobalOptionsModel is { } gom && gom.FlattenedMembers.Length > 0)
		{
			map[gom.TypeFq] = new DtoBindingTarget(
				gom.TypeFq,
				gom.FlattenedMembers,
				IsOptionsDto: true,
				IsGeneric: gom.IsGeneric,
				IsPublic: gom.IsPublic,
				gom.BestCtorParamOrder);
		}

		foreach ((var node, _) in EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty))
		{
			if (node.CommandNamespaceOptionsModel is not { } nsModel)
				continue;
			if (nsModel.FlattenedMembers.Length == 0)
				continue;
			if (map.ContainsKey(nsModel.TypeFq))
				continue;
			map[nsModel.TypeFq] = new DtoBindingTarget(
				nsModel.TypeFq,
				nsModel.FlattenedMembers,
				IsOptionsDto: true,
				IsGeneric: nsModel.IsGeneric,
				IsPublic: nsModel.IsPublic,
				nsModel.BestCtorParamOrder);
		}

		foreach (var cmd in app.AllCommands)
		{
			if (cmd.HandlerParamTypes.IsDefaultOrEmpty)
				continue;

			foreach (var mp in cmd.HandlerParamTypes)
			{
				if (!mp.IsAsParameters || mp.AsParamTypeFq is not { } typeFq)
					continue;
				if (string.IsNullOrEmpty(typeFq) || map.ContainsKey(typeFq))
					continue;

				// Extract the already-flattened DTO members from the command's Parameters array
				// (these were computed by FlattenAsParametersType during analysis and include proper prefix/AsParametersMeta).
				var flat = cmd.Parameters
					.Where(p => p.AsParametersOwnerParamName == mp.Name)
					.ToImmutableArray();

				if (flat.Length > 0)
					map[typeFq] = new DtoBindingTarget(
						typeFq,
						flat,
						IsOptionsDto: false,
						IsGeneric: mp.AsParamIsGeneric,
						IsPublic: mp.AsParamIsPublic,
						mp.AsParamBestCtorParamOrder);
			}
		}

		return map.Values
			.OrderBy(t => t.TypeFq, StringComparer.Ordinal)
			.ToImmutableArray();
	}

	private static string OptionsStaticFieldName(INamedTypeSymbol type) =>
		"s_opts_" + DtoMethodSuffix(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

	private static string OptionsStaticFieldNameFq(string typeFq) =>
		"s_opts_" + DtoMethodSuffix(typeFq);

	/// <summary>Name of the per-command-runner local variable that holds the reconstructed options instance.</summary>
	private static string OptionsLocalVarName(INamedTypeSymbol type) =>
		"__opts_" + DtoMethodSuffix(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

	private static string OptionsLocalVarNameFq(string typeFq) =>
		"__opts_" + DtoMethodSuffix(typeFq);

	private static string DtoMethodSuffix(INamedTypeSymbol type) =>
		DtoMethodSuffix(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

	private static string DtoMethodSuffix(string typeFq)
	{
		var fq = typeFq;
		if (fq.StartsWith("global::", StringComparison.Ordinal))
			fq = fq.Substring(8);

		var sb = new StringBuilder();
		foreach (var c in fq)
		{
			if (char.IsLetterOrDigit(c))
				sb.Append(c);
			else
				sb.Append('_');
		}

		return sb.Length == 0 ? "Dto" : sb.ToString();
	}

	private static void EmitDtoBindingMethods(CodeWriter w, ImmutableArray<DtoBindingTarget> targets)
	{
		foreach (var t in targets)
		{
			var suffix = DtoMethodSuffix(t.TypeFq);
			var lenientName = "TryParseDto_" + suffix;
			var strictName = "TryParseDtoExact_" + suffix;
			var syn = SyntheticOptionsCommand(t.Members, lenientName);

			EmitCommandRunner(
				w,
				syn,
				ImmutableArray<GlobalMiddlewareRegistration>.Empty,
				emitDtoTryParse: true,
				dtoLenient: true,
				dtoMethodName: lenientName,
				dtoResultTypeFq: t.TypeFq,
				dtoOptionsTypeFq: t.IsOptionsDto ? t.TypeFq : null,
				dtoOptionsBestCtorParamOrder: t.IsOptionsDto ? t.BestCtorParamOrder : null);

			EmitCommandRunner(
				w,
				syn,
				ImmutableArray<GlobalMiddlewareRegistration>.Empty,
				emitDtoTryParse: true,
				dtoLenient: false,
				dtoMethodName: strictName,
				dtoResultTypeFq: t.TypeFq,
				dtoOptionsTypeFq: t.IsOptionsDto ? t.TypeFq : null,
				dtoOptionsBestCtorParamOrder: t.IsOptionsDto ? t.BestCtorParamOrder : null);
		}
	}

	private static void EmitDtoTypeExtensions(
		SourceProductionContext context,
		ImmutableArray<DtoBindingTarget> targets,
		string arghGeneratedRootTypeName,
		string suppressionPragma)
	{
		if (targets.IsEmpty)
			return;

		const string pushOptions = "global::Nullean.Argh.Runtime.ArghRuntime.PushParserOptions(options)";
		const string optionsParam = "global::Nullean.Argh.Parsing.ParserOptions options";
		var w = new CodeWriter();
		w.Line("// <auto-generated/>");
		w.Line("#nullable enable");
		w.Line(suppressionPragma);
		w.Line("using System;");
		w.Line("using System.Diagnostics.CodeAnalysis;");
		w.Line();
		using (w.Block("namespace Nullean.Argh"))
		{
			w.Line("/// <summary>Source-generated DTO parsers. Uses C# 14 extension members (static extensions on each DTO type) plus a <see cref=\"Type\"/>-based overload for generic dispatch.</summary>");
			using (w.Block("public static class ArghTypeBindingExtensions"))
			{
				// One Type-based dispatcher per binding mode, then the same pair taking parser options.
				foreach (var (name, prefix) in new[] { ("TryParseArgh", "TryParseDto_"), ("TryParseArghExact", "TryParseDtoExact_") })
				{
					using (w.Block($"public static bool {name}<T>(this Type type, string[] args, [NotNullWhen(true)] out T? value) where T : class"))
					{
						w.Line("value = null;");
						w.If("!ReferenceEquals(type, typeof(T))", "throw new ArgumentException(\"The receiver must be typeof(T).\", nameof(type));");
						foreach (var t in targets)
						{
							using (w.If($"typeof(T) == typeof({t.TypeFq})"))
							{
								w.Line($"var ok = {arghGeneratedRootTypeName}.{prefix}{DtoMethodSuffix(t.TypeFq)}(args, out var v);");
								w.Line("value = (T?)(object?)v;");
								w.Line("return ok;");
							}
						}

						w.Line("throw new InvalidOperationException(\"No pregenerated Argh DTO parser for \" + typeof(T).FullName + \". Register the type as UseGlobalOptions/UseNamespaceOptions or use it with [AsParameters] on a command.\");");
					}

					w.Line();
				}

				// Parser options overloads: ArghRuntime.ParserOptions returns the given options for the duration of the parse.
				foreach (var name in new[] { "TryParseArgh", "TryParseArghExact" })
				{
					w.Line($"/// <summary>Like <c>{name}</c>, but parses values with <paramref name=\"options\"/> instead of <see cref=\"global::Nullean.Argh.Runtime.ArghRuntime.ParserOptions\"/>.</summary>");
					using (w.Block($"public static bool {name}<T>(this Type type, string[] args, {optionsParam}, [NotNullWhen(true)] out T? value) where T : class"))
						w.Using(pushOptions, $"return {name}<T>(type, args, out value);");
					w.Line();
				}

				foreach (var t in targets)
				{
					if (t.IsGeneric)
						continue;

					var fq = t.TypeFq;
					var vis = t.IsPublic ? "public" : "internal";
					var methods = new[]
					{
						("TryParseArgh", $"global::Nullean.Argh.{arghGeneratedRootTypeName}.TryParseDto_{DtoMethodSuffix(fq)}"),
						("TryParseArghExact", $"global::Nullean.Argh.{arghGeneratedRootTypeName}.TryParseDtoExact_{DtoMethodSuffix(fq)}"),
					};
					using (w.Block($"extension({fq})"))
					{
						foreach (var (name, target) in methods)
							w.Arrow($"{vis} static bool {name}(string[] args, [NotNullWhen(true)] out {fq}? value)", $"{target}(args, out value)");
						foreach (var (name, target) in methods)
						{
							using (w.Block($"{vis} static bool {name}(string[] args, {optionsParam}, [NotNullWhen(true)] out {fq}? value)"))
								w.Using(pushOptions, $"return {target}(args, out value);");
						}
					}

					w.Line();
				}
			}
		}

		AddSource(context, "ArghTypeBindingExtensions.g.cs", w);
	}



}
