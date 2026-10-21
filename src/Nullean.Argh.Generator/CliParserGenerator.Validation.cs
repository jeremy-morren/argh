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
	private static void ReportBoolNegationSwitchConflictsAcc(
		DiagnosticAccumulator acc,
		Location fallbackLocation,
		ImmutableArray<ParameterModel> parameters,
		IMethodSymbol method)
	{
		var locByParamName = new Dictionary<string, Location>(StringComparer.Ordinal);
		foreach (var sym in method.Parameters)
		{
			if (sym.Locations.Length == 0)
				continue;
			var loc = sym.Locations[0];
			if (loc.IsInSource)
				locByParamName[sym.Name] = loc;
		}

		foreach (var nullable in parameters)
		{
			if (nullable.Kind != ParameterKind.Flag || nullable.Special != BoolSpecialKind.NullableBool)
				continue;
			var negCli = "no-" + nullable.CliLongName;
			foreach (var plain in parameters)
			{
				if (plain.Kind != ParameterKind.Flag || plain.Special != BoolSpecialKind.Bool)
					continue;
				if (!string.Equals(plain.CliLongName, negCli, StringComparison.OrdinalIgnoreCase))
					continue;
				var loc = locByParamName.TryGetValue(plain.SymbolName, out var l) ? l : fallbackLocation;
				acc.Add(BoolFlagCollidesWithNullableNegation, loc, plain.SymbolName, plain.CliLongName);
			}
		}
	}




	/// <summary>DiagnosticAccumulator-based overload for Select-step analysis.</summary>
	private static void ReportDuplicateCliNamesAcc(DiagnosticAccumulator acc, Location location, ImmutableArray<ParameterModel> parameters)
	{
		var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var p in parameters)
		{
			if (p.Kind != ParameterKind.Flag) continue;
			void check(string name)
			{
				if (string.IsNullOrEmpty(name)) return;
				if (seen.TryGetValue(name, out var first))
				{
					if (!string.Equals(first, p.SymbolName, StringComparison.Ordinal))
						acc.Add(DuplicateCliNames, location, name);
				}
				else
					seen[name] = p.SymbolName;
			}
			check(p.CliLongName);
			foreach (var al in p.Aliases) check(al);
			if (p.Special == BoolSpecialKind.NullableBool) check("no-" + p.CliLongName);
		}
	}

	/// <summary>
	/// Each generated <c>TryApplyShortFlag</c> groups all flag-like params in one scope (global prefetch, namespace
	/// prefetch, or a single handler). Duplicate single-letter shortcuts would emit invalid duplicate <c>case</c> labels.
	/// </summary>
	private static void ValidateDuplicateShortOptionLetters(SourceProductionContext context, AppEmitModel app)
	{
		if (app.GlobalOptionsModel is { FlattenedMembers: var gm } && !gm.IsDefaultOrEmpty)
			ReportDuplicateShortsAmongMembers(context, Location.None, gm, "global options");

		static void walkNs(RegistryNode node, SourceProductionContext ctx)
		{
			if (node.CommandNamespaceOptionsModel is { FlattenedMembers: var nm } && !nm.IsDefaultOrEmpty)
			{
				var loc = node.CommandNamespaceOptionsLocation ?? Location.None;
				ReportDuplicateShortsAmongMembers(ctx, loc, nm, "namespace-scoped options");
			}

			foreach (var ch in node.Children)
				walkNs(ch.Node, ctx);
		}

		walkNs(app.Root, context);

		foreach (var cmd in app.AllCommands)
		{
			if (cmd.Parameters.IsDefaultOrEmpty)
				continue;
			var loc = cmd.HandlerSpanInfo.ToLocation();
			var scope =
				cmd.RoutePrefix.IsDefaultOrEmpty
					? $"command '{cmd.CommandName}'"
					: $"command '{string.Join(" ", cmd.RoutePrefix)} {cmd.CommandName}'";
			ReportDuplicateShortsAmongMembers(context, loc, cmd.Parameters, scope);
		}
	}

	private static void ReportDuplicateShortsAmongMembers(
		SourceProductionContext context,
		Location location,
		ImmutableArray<ParameterModel> members,
		string scopeDescription)
	{
		var byChar = new Dictionary<char, string>();
		foreach (var p in members)
		{
			if (!IsEmittedFlagLike(p.Kind))
				continue;
			if (p.ShortOpt is not char ch)
				continue;
			if (byChar.TryGetValue(ch, out var firstLong))
			{
				context.ReportDiagnostic(Diagnostic.Create(
					DuplicateShortOption,
					location,
					ch.ToString(),
					firstLong,
					p.CliLongName,
					scopeDescription));
			}
			else
			{
				byChar[ch] = p.CliLongName;
			}
		}
	}

	/// <summary>DiagnosticAccumulator-based overload for Select-step analysis.</summary>
	private static void ValidateExpandedParameterLayoutAcc(DiagnosticAccumulator acc, Location location, ImmutableArray<ParameterModel> expanded)
	{
		var seenFlag = false;
		foreach (var p in expanded)
		{
			if (p.Kind == ParameterKind.Injected) continue;
			if (p.Kind == ParameterKind.Flag) { seenFlag = true; continue; }
			// A variadic positional is allowed after flags — C# requires params to be last.
			if (p.Kind == ParameterKind.Positional && seenFlag && !p.IsVariadic)
			{
				acc.Add(ArgumentOrder, location);
				return;
			}
		}
	}

	private static void ValidateVariadicPositionalIsLastAcc(DiagnosticAccumulator acc, Location location, ImmutableArray<ParameterModel> parameters)
	{
		var sawVariadic = false;
		foreach (var p in parameters)
		{
			if (p.Kind != ParameterKind.Positional) continue;
			if (sawVariadic) { acc.Add(VariadicMustBeLastPositional, location); return; }
			if (p.IsVariadic) sawVariadic = true;
		}
	}

	/// <summary>DiagnosticAccumulator-based overload for Select-step analysis.</summary>
	private static ImmutableArray<ParameterModel> FlattenAsParametersTypeAcc(
		DiagnosticAccumulator acc,
		Location location,
		IParameterSymbol methodParam,
		INamedTypeSymbol type,
		string? prefix,
		Compilation? compilation,
		CSharpParseOptions parseOptions)
	{
		var pfx = string.IsNullOrWhiteSpace(prefix) ? "" : Naming.ToCliLongName(prefix!.Trim()) + "-";
		var owner = methodParam.Name;
		var typeFq = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var primary = TryGetPrimaryConstructor(type);
		var ctorNames = new HashSet<string>(StringComparer.Ordinal);
		var list = new List<ParameterModel>();
		var order = 0;
		if (primary is not null)
		{
			foreach (var cp in primary.Parameters)
			{
				ctorNames.Add(cp.Name);
				list.Add(ParameterModel.FromAsParametersCtorParameter(owner, typeFq, type, cp, pfx, order++, compilation, parseOptions,
					acc,
					location));
			}
		}
		var chain = new List<INamedTypeSymbol>();
		for (var t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
			chain.Add(t);
		var seenPropNames = new HashSet<string>(StringComparer.Ordinal);
		for (var i = chain.Count - 1; i >= 0; i--)
		{
			var tt = chain[i];
			foreach (var member in tt.GetMembers())
			{
				if (member is not IPropertySymbol prop) continue;
				if (prop.DeclaredAccessibility != Accessibility.Public || prop.IsStatic || prop.IsIndexer) continue;
				if (!IsSettableForAsParameters(prop)) continue;
				if (ctorNames.Contains(prop.Name)) continue;
				if (!seenPropNames.Add(prop.Name)) continue;
				list.Add(ParameterModel.FromAsParametersInitProperty(methodParamName: owner, typeFq, prop, pfx, order++, compilation, parseOptions,
					acc,
					location));
			}
		}
		if (list.Count == 0)
			acc.Add(AsParametersEmptyType, location, type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat));
		return list.ToImmutableArray();
	}


	private static string? TryGetStringLiteral(ExpressionSyntax expr) =>
		expr switch
		{
			LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } lit => lit.Token.ValueText,
			_ => null
		};

	/// <summary>Unique per compilation assembly so generated CLI types do not collide across referenced assemblies (e.g. CS0436 with InternalsVisibleTo).</summary>
	private static ImmutableArray<ValidationConstraint> ReadValidationConstraints(ISymbol attributeHost, CliScalarKind scalarKind,
		string primitiveTypeName, bool isCollection = false, CliScalarKind? filesystemScalarKind = null)
	{
		var fsKind = filesystemScalarKind ?? scalarKind;
		var builder = ImmutableArray.CreateBuilder<ValidationConstraint>();
		foreach (var attr in attributeHost.GetAttributes())
		{
			var fqn = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "";
			switch (fqn)
			{
				case "global::System.ComponentModel.DataAnnotations.RangeAttribute":
					if (TryBuildRangeConstraint(attr, attributeHost) is { } range)
						builder.Add(range);
					break;
				case "global::Nullean.Argh.TimeSpanRangeAttribute":
					if (primitiveTypeName is "TimeSpan" or "TimeSpan?" &&
					    attr.ConstructorArguments.Length >= 2)
						builder.Add(new TimeSpanRangeConstraint(
							attr.ConstructorArguments[0].ToCSharpString(),
							attr.ConstructorArguments[1].ToCSharpString()));
					break;
				case "global::System.ComponentModel.DataAnnotations.StringLengthAttribute":
					if (attr.ConstructorArguments.Length >= 1)
					{
						var max = (int?)(int?)attr.ConstructorArguments[0].Value;
						int? min = null;
						foreach (var n in attr.NamedArguments)
							if (n.Key == "MinimumLength") min = (int?)n.Value.Value;
						if (isCollection)
							builder.Add(new CollectionCountConstraint(min, max));
						else
							builder.Add(new StringLengthConstraint(min, max));
					}
					break;
				case "global::System.ComponentModel.DataAnnotations.MinLengthAttribute":
					if (attr.ConstructorArguments.Length >= 1)
					{
						if (isCollection)
							builder.Add(new CollectionCountConstraint((int?)attr.ConstructorArguments[0].Value, null));
						else
							builder.Add(new StringLengthConstraint((int?)attr.ConstructorArguments[0].Value, null));
					}
					break;
				case "global::System.ComponentModel.DataAnnotations.MaxLengthAttribute":
					if (attr.ConstructorArguments.Length >= 1)
					{
						if (isCollection)
							builder.Add(new CollectionCountConstraint(null, (int?)attr.ConstructorArguments[0].Value));
						else
							builder.Add(new StringLengthConstraint(null, (int?)attr.ConstructorArguments[0].Value));
					}
					break;
				case "global::System.ComponentModel.DataAnnotations.LengthAttribute":
					if (attr.ConstructorArguments.Length >= 2)
					{
						if (isCollection)
							builder.Add(new CollectionCountConstraint((int?)attr.ConstructorArguments[0].Value, (int?)attr.ConstructorArguments[1].Value));
						else
							builder.Add(new StringLengthConstraint((int?)attr.ConstructorArguments[0].Value, (int?)attr.ConstructorArguments[1].Value));
					}
					break;
				case "global::System.ComponentModel.DataAnnotations.RegularExpressionAttribute":
					if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is string pat)
						builder.Add(new RegexConstraint(pat));
					break;
				case "global::System.ComponentModel.DataAnnotations.AllowedValuesAttribute":
					if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Kind == TypedConstantKind.Array)
					{
						var args = attr.ConstructorArguments[0].Values;
						if (!args.IsEmpty)
						{
							var values = ReadMembershipValues(args, attributeHost, isCollection);
							builder.Add(new AllowedValuesConstraint(values.Literals, values.Display, values.CultureTypes, values.EqualsCast));
						}
					}
					break;
				case "global::System.ComponentModel.DataAnnotations.DeniedValuesAttribute":
					if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Kind == TypedConstantKind.Array)
					{
						var args = attr.ConstructorArguments[0].Values;
						if (!args.IsEmpty)
						{
							var values = ReadMembershipValues(args, attributeHost, isCollection);
							builder.Add(new DeniedValuesConstraint(values.Literals, values.Display, values.CultureTypes, values.EqualsCast));
						}
					}
					break;
				case "global::System.ComponentModel.DataAnnotations.EmailAddressAttribute":
					builder.Add(new EmailConstraint());
					break;
				case "global::System.ComponentModel.DataAnnotations.UrlAttribute":
					if (scalarKind == CliScalarKind.Uri)
						builder.Add(new UriSchemeConstraint(ImmutableArray.Create("http", "https")));
					else
						builder.Add(new UrlConstraint());
					break;
				case "global::System.ComponentModel.DataAnnotations.FileExtensionsAttribute":
				{
					if (fsKind != CliScalarKind.FileInfo)
						break;
					string? extsStr = null;
					foreach (var n in attr.NamedArguments)
						if (n.Key == "Extensions") extsStr = n.Value.Value as string;
					extsStr ??= "png,jpg,jpeg,gif";
					var exts = extsStr.Split(',').Select(e => e.Trim().TrimStart('.')).ToImmutableArray();
					builder.Add(new FileExtensionsConstraint(exts));
					break;
				}
				case "global::Nullean.Argh.UriSchemeAttribute":
					// Elsewhere AGH0024 reports it: the check reads Uri.Scheme.
					if (scalarKind == CliScalarKind.Uri && attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Kind == TypedConstantKind.Array)
					{
						var schemes = attr.ConstructorArguments[0].Values
							.Select(v => v.Value as string).Where(s => s is not null).Select(s => s!)
							.ToImmutableArray();
						if (!schemes.IsEmpty) builder.Add(new UriSchemeConstraint(schemes));
					}
					break;
				case UriOptionsAttributeFq:
					// Absolute, the default, needs no constraint. On a collection it applies to each element.
					if (fsKind == CliScalarKind.Uri && ReadUriKind(attr) is var uriKind && uriKind != UriKindAbsolute)
						builder.Add(new UriKindConstraint(uriKind == UriKindRelative ? "relative" : "relativeOrAbsolute"));
					break;
				case "global::Nullean.Argh.ExistingAttribute":
					if (fsKind is CliScalarKind.FileInfo or CliScalarKind.DirectoryInfo)
						builder.Add(new ExistingPathConstraint());
					break;
				case "global::Nullean.Argh.NonExistingAttribute":
					if (fsKind is CliScalarKind.FileInfo or CliScalarKind.DirectoryInfo)
						builder.Add(new NonExistingPathConstraint());
					break;
				case "global::Nullean.Argh.RejectSymbolicLinksAttribute":
					if (fsKind is CliScalarKind.FileInfo or CliScalarKind.DirectoryInfo)
						builder.Add(new RejectSymbolicLinksConstraint());
					break;
			}
		}
		return OrderPathValidations(builder.ToImmutable());
	}

	private static ImmutableArray<ValidationConstraint> OrderPathValidations(ImmutableArray<ValidationConstraint> validations)
	{
		if (validations.IsDefaultOrEmpty)
			return validations;

		var hasReject = false;
		foreach (var c in validations)
		{
			if (c is RejectSymbolicLinksConstraint)
			{
				hasReject = true;
				break;
			}
		}

		if (!hasReject)
			return validations;

		var b = ImmutableArray.CreateBuilder<ValidationConstraint>(validations.Length);
		foreach (var c in validations)
		{
			if (c is RejectSymbolicLinksConstraint)
				b.Add(c);
		}

		foreach (var c in validations)
		{
			if (c is not RejectSymbolicLinksConstraint)
				b.Add(c);
		}

		return b.ToImmutable();
	}

	private static string ResolveEnumMemberCliName(ImmutableArray<string> cliNames, int index, string memberName)
		=> !cliNames.IsDefaultOrEmpty ? cliNames[index] : memberName.ToLowerInvariant();

	private static string? BuildValidationLine(ParameterModel p)
	{
		var tokens = new List<string>();

		if (p.ScalarKind == CliScalarKind.Enum && !p.EnumMemberNames.IsDefaultOrEmpty)
		{
			tokens.Add("One of: <" + string.Join("|", p.EnumMemberNames.Select((m, i) => ResolveEnumMemberCliName(p.EnumMemberCliNames, i, m))) + ">");
			if (p.EnumMemberDocs is { Count: > 0 } docs)
			{
				var memberDescParts = new List<string>();
				for (var i = 0; i < p.EnumMemberNames.Length; i++)
				{
					var member = p.EnumMemberNames[i];
					var cliName = ResolveEnumMemberCliName(p.EnumMemberCliNames, i, member);
					if (docs.TryGetValue(member, out var memberDoc) && !string.IsNullOrWhiteSpace(memberDoc))
						memberDescParts.Add($"{cliName}: {memberDoc.Trim()}");
				}
				if (memberDescParts.Count > 0)
					tokens.Add("(" + string.Join("; ", memberDescParts) + ")");
			}
		}

		if (p.IsCollection && p.ElementScalarKind == CliScalarKind.Enum && !p.ElementEnumMemberNames.IsDefaultOrEmpty)
		{
			var label = p.CollectionTargetIsReadOnlySet ? "Combination of:" : "One or more of:";
			tokens.Add(label + " <" + string.Join("|", p.ElementEnumMemberNames.Select((m, i) => ResolveEnumMemberCliName(p.ElementEnumMemberCliNames, i, m))) + ">");
			if (p.ElementEnumMemberDocs is { Count: > 0 } elemDocs)
			{
				var memberDescParts = new List<string>();
				for (var i = 0; i < p.ElementEnumMemberNames.Length; i++)
				{
					var member = p.ElementEnumMemberNames[i];
					var cliName = ResolveEnumMemberCliName(p.ElementEnumMemberCliNames, i, member);
					if (elemDocs.TryGetValue(member, out var memberDoc) && !string.IsNullOrWhiteSpace(memberDoc))
						memberDescParts.Add($"{cliName}: {memberDoc.Trim()}");
				}
				if (memberDescParts.Count > 0)
					tokens.Add("(" + string.Join("; ", memberDescParts) + ")");
			}
		}

		if (!p.Validations.IsDefaultOrEmpty)
		{
			foreach (var v in p.Validations)
			{
				switch (v)
				{
					case RangeConstraint { MinLiteral: { } min, MaxLiteral: { } max } r:
						tokens.Add($"[range: {RangeBoundText(r, min)}-{RangeBoundText(r, max)}]");
						break;
					case RangeConstraint { MinLiteral: { } min } r:
						tokens.Add($"[min: {RangeBoundText(r, min)}]");
						break;
					case RangeConstraint { MaxLiteral: { } max } r:
						tokens.Add($"[max: {RangeBoundText(r, max)}]");
						break;
					case CollectionCountConstraint cc when cc.Min.HasValue && cc.Max.HasValue:
						tokens.Add($"[count: {cc.Min}-{cc.Max}]");
						break;
					case CollectionCountConstraint cc when cc.Min.HasValue:
						tokens.Add($"[min-count: {cc.Min}]");
						break;
					case CollectionCountConstraint cc when cc.Max.HasValue:
						tokens.Add($"[max-count: {cc.Max}]");
						break;
					case StringLengthConstraint s when s.Min.HasValue && s.Max.HasValue:
						tokens.Add($"[length: {s.Min}-{s.Max}]");
						break;
					case StringLengthConstraint s when s.Min.HasValue:
						tokens.Add($"[min-length: {s.Min}]");
						break;
					case StringLengthConstraint s when s.Max.HasValue:
						tokens.Add($"[max-length: {s.Max}]");
						break;
					case RegexConstraint rx:
						tokens.Add($"[pattern: {rx.Pattern}]");
						break;
					case AllowedValuesConstraint av:
						tokens.Add("[allowed: " + string.Join("|", CultureDisplayValues(av.Values, av.DisplayValues, av.CultureTypes)) + "]");
						break;
					case DeniedValuesConstraint dv:
						tokens.Add("[denied: " + string.Join("|", CultureDisplayValues(dv.Values, dv.DisplayValues, dv.CultureTypes)) + "]");
						break;
					case EmailConstraint:
						tokens.Add("[email]");
						break;
					case UrlConstraint:
						tokens.Add("[url]");
						break;
					case UriSchemeConstraint us:
						tokens.Add("[schemes: " + string.Join("|", us.Schemes) + "]");
						break;
					case UriKindConstraint uk:
						tokens.Add(uk.Kind == "relative" ? "[relative]" : "[relative or absolute]");
						break;
					case FileExtensionsConstraint fe:
						tokens.Add("[extensions: " + string.Join("|", fe.Extensions) + "]");
						break;
					case ExistingPathConstraint:
						tokens.Add("[existing]");
						break;
					case NonExistingPathConstraint:
						tokens.Add("[unused path]");
						break;
					case RejectSymbolicLinksConstraint:
						tokens.Add("[no symlinks]");
						break;
					case TimeSpanRangeConstraint ts:
						tokens.Add($"[time-span-range: {ts.MinLiteral.Trim('"')}-{ts.MaxLiteral.Trim('"')}]");
						break;
				}
			}
		}

		if (p.ExpandUserProfileBeforeBind)
			tokens.Add("[expand ~ profile]");

		return tokens.Count > 0 ? string.Join(" ", tokens) : null;
	}

	private abstract record ValidationConstraint;
	private sealed record CollectionCountConstraint(int? Min, int? Max) : ValidationConstraint;
	/// <param name="MinLiteral">The lower bound as a C# literal of <paramref name="CultureType"/>; <see langword="null"/> when unbounded.</param>
	/// <param name="MaxLiteral">The upper bound as a C# literal of <paramref name="CultureType"/>; <see langword="null"/> when unbounded.</param>
	/// <param name="CultureType">The C# keyword of a numeric bound, which help and errors format with the parse culture; empty otherwise.</param>
	/// <param name="CompareCast">
	/// The parameter type each bound is cast to before comparing, for numeric types C# can't compare with an <c>int</c> or
	/// <c>double</c> literal (such as <c>Half</c>); <see langword="null"/> otherwise.
	/// </param>
	/// <param name="SchemaMin">The lower bound as invariant text for <c>__schema</c>; <see langword="null"/> when unbounded.</param>
	/// <param name="SchemaMax">The upper bound as invariant text for <c>__schema</c>; <see langword="null"/> when unbounded.</param>
	private sealed record RangeConstraint(
		string? MinLiteral, string? MaxLiteral, string CultureType, string? CompareCast, string? SchemaMin, string? SchemaMax) : ValidationConstraint;

	/// <summary>
	/// One bound of a <c>[Range]</c>: its numeric value, the typed C# literal emitted for it and, for bounds parsed from
	/// text, the value in its own type, which the unbounded test compares exactly where doubles would round.
	/// </summary>
	private readonly record struct RangeBound(double Value, string Literal, object? Exact = null);

	/// <summary>
	/// Reads a <c>[Range]</c> attribute. A bound at or beyond the parameter type's own <c>MinValue</c>/<c>MaxValue</c> (or an
	/// infinity, or <c>NaN</c>) is unbounded, compared by value, so <c>float.MaxValue</c> and <c>Single.MaxValue</c>, or
	/// <c>long.MaxValue</c> passed through the <c>double</c> constructor, all mean "no limit". Returns <see langword="null"/>
	/// when neither bound limits the value, or the bounds can't be read; <paramref name="report"/> then gets the reason for
	/// bounds that are wrong rather than absent, with the attribute argument at fault.
	/// </summary>
	private static RangeConstraint? TryBuildRangeConstraint(AttributeData attr, ISymbol host, Action<string, SyntaxNode?>? report = null)
	{
		var args = attr.ConstructorArguments;
		var valueType = UnwrapNullable(MemberType(host));
		var limits = valueType is null ? null : NumericLimits(valueType);
		var isDecimal = valueType?.SpecialType == SpecialType.System_Decimal;
		var compareCast = valueType is not null && limits is not null && !IsCSharpNumericKeywordType(valueType)
			? valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
			: null;
		var syntaxArgs = (attr.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax)?.ArgumentList?.Arguments
			.Where(static a => a.NameEquals is null && a.NameColon is null).ToList();
		ExpressionSyntax? ArgSyntax(int i) => syntaxArgs is { } list && list.Count == args.Length ? list[i].Expression : null;

		RangeBound? min, max;
		if (args.Length == 3 && args[0].Kind == TypedConstantKind.Type)
		{
			// [Range(typeof(T), "min", "max")]: the bounds are text in T, parsed now with the invariant culture rather than
			// at runtime with the current one.
			var operandType = args[0].Value as ITypeSymbol;
			if (operandType is null || ClrNumericType(operandType) is null)
			{
				report?.Invoke($"typeof({operandType?.ToDisplayString() ?? "?"}) isn't a numeric type; only the C# numeric types (int, long, double, decimal, ...) are checked.", ArgSyntax(0));
				return null;
			}
			if (limits is null)
			{
				report?.Invoke($"[Range(typeof({operandType.ToDisplayString()}), ...)] needs a numeric parameter, not {valueType?.ToDisplayString() ?? "?"}.", ArgSyntax(0));
				return null;
			}

			// A decimal parameter keeps decimal bounds; decimal bounds on a double, float or Half become doubles, which those compare with.
			var boundType = isDecimal || operandType.SpecialType != SpecialType.System_Decimal || IsIntegralKeywordType(valueType!)
				? (isDecimal ? valueType! : operandType)
				: null;
			min = BoundFromString(args[1].Value as string, operandType, boundType, ArgSyntax(1), report);
			max = BoundFromString(args[2].Value as string, operandType, boundType, ArgSyntax(2), report);
			if (min is null || max is null)
				return null;
		}
		else if (args.Length >= 2 && args[0].Value is int or double && args[1].Value is int or double)
		{
			min = BoundFromConstant(args[0], ArgSyntax(0), isDecimal);
			max = BoundFromConstant(args[1], ArgSyntax(1), isDecimal);
		}
		else
			return null;

		var minValue = min.Value.Value;
		var maxValue = max.Value.Value;
		var exactLimits = valueType is null ? null : ExactLimits(valueType);
		var minUnbounded = double.IsNaN(minValue) || double.IsNegativeInfinity(minValue)
		                   || (exactLimits is { } elo && ExactDecimal(min.Value.Exact) is { } exactMin
			                   ? exactMin <= elo.Min
			                   : limits is { } lo && minValue <= lo.Min);
		var maxUnbounded = double.IsNaN(maxValue) || double.IsPositiveInfinity(maxValue)
		                   || (exactLimits is { } ehi && ExactDecimal(max.Value.Exact) is { } exactMax
			                   ? exactMax >= ehi.Max
			                   : limits is { } hi && maxValue >= hi.Max);
		if (minUnbounded && maxUnbounded)
			return null;

		var minLiteral = minUnbounded ? null : min.Value.Literal;
		var maxLiteral = maxUnbounded ? null : max.Value.Literal;
		return new RangeConstraint(
			minLiteral,
			maxLiteral,
			LiteralKeyword(minLiteral ?? maxLiteral!),
			compareCast,
			minLiteral is null ? null : NumericLiteralText(minLiteral),
			maxLiteral is null ? null : NumericLiteralText(maxLiteral));
	}

	/// <summary>A bound from the <c>int</c> or <c>double</c> constructor, as a literal of that type, or of <c>decimal</c> for a decimal parameter.</summary>
	private static RangeBound BoundFromConstant(TypedConstant arg, ExpressionSyntax? syntax, bool decimalBound)
	{
		var value = Convert.ToDouble(arg.Value, CultureInfo.InvariantCulture);
		var literal = FormatNumericLiteral(arg.Value)!;
		if (!decimalBound || double.IsInfinity(value) || double.IsNaN(value) || Math.Abs(value) >= (double)decimal.MaxValue)
			return new RangeBound(value, literal);

		// RangeAttribute has no decimal constructor, so a decimal bound arrives as an int or a double. The source literal
		// with an m suffix keeps it exact (0.1 stays 0.1m); anything else falls back to converting the constant.
		var text = DecimalLiteralText(syntax);
		return new RangeBound(value, text is null ? FormatNumericLiteral((decimal)value)! : text + "m");
	}

	/// <summary>
	/// A bound from the <c>(Type, string, string)</c> constructor: <paramref name="text"/> must parse (<see cref="ParseNumericInvariant"/>) as
	/// <paramref name="operandType"/>; the literal is then <paramref name="boundType"/>, or <c>double</c> when that's
	/// <see langword="null"/>. Reports and returns <see langword="null"/> when the text isn't a valid number.
	/// </summary>
	private static RangeBound? BoundFromString(string? text, ITypeSymbol operandType, ITypeSymbol? boundType, ExpressionSyntax? syntax,
		Action<string, SyntaxNode?>? report)
	{
		var trimmed = text?.Trim() ?? "";
		if (ParseNumericInvariant(trimmed, operandType) is not { } parsed)
		{
			var example = operandType.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal
				? "1.5"
				: "1e3";
			report?.Invoke($"\"{text}\" isn't a valid {operandType.ToDisplayString()} (bounds are parsed with the invariant culture, e.g. \"{example}\").", syntax);
			return null;
		}

		var value = Convert.ToDouble(parsed, CultureInfo.InvariantCulture);
		// Beyond the bound type (a decimal parameter given typeof(double), "1e300") it can only mean unbounded.
		var typed = boundType is null
			? value
			: SymbolEqualityComparer.Default.Equals(boundType, operandType) ? parsed : ParseNumericInvariant(trimmed, boundType) ?? value;
		return new RangeBound(value, FormatNumericLiteral(typed)!, typed);
	}

	/// <summary>The value range of an integer or <c>decimal</c> type as decimals, which hold every value exactly.</summary>
	private static (decimal Min, decimal Max)? ExactLimits(ITypeSymbol type) =>
		type.SpecialType switch
		{
			SpecialType.System_SByte => (sbyte.MinValue, sbyte.MaxValue),
			SpecialType.System_Byte => (byte.MinValue, byte.MaxValue),
			SpecialType.System_Int16 => (short.MinValue, short.MaxValue),
			SpecialType.System_UInt16 => (ushort.MinValue, ushort.MaxValue),
			SpecialType.System_Int32 => (int.MinValue, int.MaxValue),
			SpecialType.System_UInt32 => (uint.MinValue, uint.MaxValue),
			SpecialType.System_Int64 => (long.MinValue, long.MaxValue),
			SpecialType.System_UInt64 => (ulong.MinValue, ulong.MaxValue),
			SpecialType.System_Decimal => (decimal.MinValue, decimal.MaxValue),
			_ => null,
		};

	/// <summary>An integer or <c>decimal</c> value as a decimal; <see langword="null"/> for floating point and anything else.</summary>
	private static decimal? ExactDecimal(object? value) =>
		value is sbyte or byte or short or ushort or int or uint or long or ulong or decimal
			? Convert.ToDecimal(value, CultureInfo.InvariantCulture)
			: null;

	/// <summary>The C# keyword of a literal made by <see cref="FormatNumericLiteral"/>.</summary>
	private static string LiteralKeyword(string literal) =>
		literal.StartsWith("global::System.Double.", StringComparison.Ordinal) ? "double"
		: literal.StartsWith("global::System.Single.", StringComparison.Ordinal) ? "float"
		: literal.StartsWith("(", StringComparison.Ordinal) ? literal.Substring(1, literal.IndexOf(')') - 1)
		: literal.EndsWith("UL", StringComparison.Ordinal) ? "ulong"
		: literal.EndsWith("L", StringComparison.Ordinal) ? "long"
		: literal.EndsWith("U", StringComparison.Ordinal) ? "uint"
		: literal.EndsWith("m", StringComparison.Ordinal) ? "decimal"
		: literal.EndsWith("d", StringComparison.Ordinal) ? "double"
		: literal.EndsWith("f", StringComparison.Ordinal) ? "float"
		: "int";

	private static bool IsIntegralKeywordType(ITypeSymbol type) =>
		IsCSharpNumericKeywordType(type)
		&& type.SpecialType is not (SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal);

	/// <summary>Reports each <c>[Range]</c> on <paramref name="host"/> whose bounds can't be checked (AGH0038).</summary>
	/// <summary>
	/// The per-member diagnostics of an options type (its own members and inherited ones, the members it binds), carried as
	/// data on the <c>UseGlobalOptions</c>/<c>UseNamespaceOptions</c> invocation record.
	/// </summary>
	private static ImmutableArray<PendingDiagnostic> ReportOptionsMemberIssues(INamedTypeSymbol type)
	{
		var acc = new DiagnosticAccumulator();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (var t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
		{
			foreach (var member in t.GetMembers())
			{
				var bound = member switch
				{
					IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsIndexer: false, GetMethod: not null, SetMethod: not null } => true,
					IFieldSymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } => true,
					_ => false,
				};
				if (bound && seen.Add(member.Name))
					ReportRangeAttributeIssues(member, member.Name, acc, member.Locations.FirstOrDefault());
			}
		}
		return acc.ToImmutable();
	}

	private static void ReportEmbedded(SourceProductionContext context, ImmutableArray<PendingDiagnostic> diagnostics)
	{
		foreach (var pd in diagnostics.IsDefault ? ImmutableArray<PendingDiagnostic>.Empty : diagnostics)
			context.ReportDiagnostic(Diagnostic.Create(GetDescriptorById(pd.DescriptorId), pd.Span.ToLocation(), pd.Arg0, pd.Arg1));
	}

	private static void ReportRangeAttributeIssues(ISymbol host, string declaredName, DiagnosticAccumulator? acc, Location? fallbackLocation)
	{
		if (acc is null)
			return;
		foreach (var attr in host.GetAttributes())
		{
			if (attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::System.ComponentModel.DataAnnotations.RangeAttribute")
				continue;
			TryBuildRangeConstraint(attr, host, (reason, at) =>
				acc.Add(InvalidRangeBounds,
					at?.GetLocation() ?? attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? host.Locations.FirstOrDefault() ?? fallbackLocation ?? Location.None,
					declaredName, reason));
		}
	}

	/// <summary>
	/// The source text of a numeric literal (optionally negated) that is also a valid decimal literal once an <c>m</c> is
	/// added, without its <c>d</c>/<c>f</c> suffix; <see langword="null"/> for anything else (hex, constants, casts).
	/// </summary>
	private static string? DecimalLiteralText(ExpressionSyntax? expr)
	{
		var sign = "";
		if (expr is PrefixUnaryExpressionSyntax unary
		    && (unary.IsKind(SyntaxKind.UnaryMinusExpression) || unary.IsKind(SyntaxKind.UnaryPlusExpression)))
		{
			if (unary.IsKind(SyntaxKind.UnaryMinusExpression))
				sign = "-";
			expr = unary.Operand;
		}

		if (expr is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.NumericLiteralExpression))
			return null;
		var text = literal.Token.Text;
		if (text.Length > 1 && text[0] == '0' && text[1] is 'x' or 'X' or 'b' or 'B')
			return null;
		text = text.TrimEnd('d', 'D', 'f', 'F');
		return decimal.TryParse(text.Replace("_", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out _)
			? sign + text
			: null;
	}

	/// <summary>The value range of a numeric type as doubles, or <see langword="null"/> for a non-numeric type.</summary>
	private static (double Min, double Max)? NumericLimits(ITypeSymbol type) =>
		type.SpecialType switch
		{
			SpecialType.System_SByte => (sbyte.MinValue, sbyte.MaxValue),
			SpecialType.System_Byte => (byte.MinValue, byte.MaxValue),
			SpecialType.System_Int16 => (short.MinValue, short.MaxValue),
			SpecialType.System_UInt16 => (ushort.MinValue, ushort.MaxValue),
			SpecialType.System_Int32 => (int.MinValue, int.MaxValue),
			SpecialType.System_UInt32 => (uint.MinValue, uint.MaxValue),
			SpecialType.System_Int64 or SpecialType.System_IntPtr => (long.MinValue, long.MaxValue),
			SpecialType.System_UInt64 or SpecialType.System_UIntPtr => (ulong.MinValue, ulong.MaxValue),
			SpecialType.System_Single => (float.MinValue, float.MaxValue),
			SpecialType.System_Double => (double.MinValue, double.MaxValue),
			SpecialType.System_Decimal => ((double)decimal.MinValue, (double)decimal.MaxValue),
			_ => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) switch
			{
				"global::System.Half" => (-65504d, 65504d),
				"global::System.Int128" => (-1.7014118346046923E+38, 1.7014118346046923E+38),
				"global::System.UInt128" => (0d, 3.402823669209385E+38),
				// BigInteger has no limits, so only double.MinValue/MaxValue and the infinities mean "unbounded".
				"global::System.Numerics.BigInteger" => (double.MinValue, double.MaxValue),
				_ => null,
			},
		};

	/// <summary>Whether <paramref name="type"/> is a numeric type C# compares directly with an <c>int</c> or <c>double</c> literal.</summary>
	private static bool IsCSharpNumericKeywordType(ITypeSymbol type) =>
		type.SpecialType is SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16
			or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64
			or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal
			or SpecialType.System_IntPtr or SpecialType.System_UIntPtr;

	private static ITypeSymbol? UnwrapNullable(ITypeSymbol? type) =>
		type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
			? nullable.TypeArguments[0]
			: type;

	/// <summary>A range bound as help and errors show it: numeric bounds are formatted with the parse culture.</summary>
	private static string RangeBoundText(RangeConstraint r, string literal) =>
		r.CultureType.Length > 0 ? RuntimeCultureText(r.CultureType, literal) : literal.Trim('"');
	private sealed record TimeSpanRangeConstraint(string MinLiteral, string MaxLiteral) : ValidationConstraint;
	private sealed record StringLengthConstraint(int? Min, int? Max) : ValidationConstraint;
	private sealed record RegexConstraint(string Pattern) : ValidationConstraint;
	/// <param name="Values">C# literals, for the emitted equality checks (e.g. <c>"dev"</c> with its quotes).</param>
	/// <param name="DisplayValues">The values as the user types them in the invariant culture, for <c>__schema</c> and non-numeric values.</param>
	/// <param name="CultureTypes">
	/// Per value, the C# keyword of a numeric constant, which help, errors and completion format with the parse culture
	/// (see <see cref="CultureDisplayValues"/>); empty for other values.
	/// </param>
	/// <param name="EqualsCast">
	/// For a floating-point parameter, which is compared with <c>.Equals</c> so <c>NaN</c> matches <c>NaN</c>: an empty
	/// string, or the parameter type each literal is cast to when it isn't a C# keyword type (such as <c>Half</c>).
	/// <see langword="null"/> for other parameters, which compare with <c>==</c>.
	/// </param>
	private sealed record AllowedValuesConstraint(
		ImmutableArray<string> Values, ImmutableArray<string> DisplayValues, ImmutableArray<string> CultureTypes, string? EqualsCast)
		: ValidationConstraint;
	/// <inheritdoc cref="AllowedValuesConstraint"/>
	private sealed record DeniedValuesConstraint(
		ImmutableArray<string> Values, ImmutableArray<string> DisplayValues, ImmutableArray<string> CultureTypes, string? EqualsCast)
		: ValidationConstraint;

	/// <summary>
	/// The values of <c>[AllowedValues]</c>/<c>[DeniedValues]</c>. A numeric constant is converted to the parameter's
	/// numeric type when nothing is lost (a <c>double</c> becomes the nearest <c>float</c> for a <c>float</c>), so
	/// <c>[AllowedValues(1, 2)]</c> on a <c>double</c> compares with <c>1d</c> and <c>2d</c>.
	/// </summary>
	private static (ImmutableArray<string> Literals, ImmutableArray<string> Display, ImmutableArray<string> CultureTypes, string? EqualsCast)
		ReadMembershipValues(ImmutableArray<TypedConstant> args, ISymbol host, bool isCollection)
	{
		var valueType = isCollection ? null : UnwrapNullable(MemberType(host));
		var literals = ImmutableArray.CreateBuilder<string>(args.Length);
		var display = ImmutableArray.CreateBuilder<string>(args.Length);
		var cultureTypes = ImmutableArray.CreateBuilder<string>(args.Length);
		foreach (var arg in args)
		{
			var value = arg.IsNull || arg.Kind != TypedConstantKind.Primitive ? null : arg.Value;
			var converted = ConvertNumericConstant(value, valueType) ?? value;
			if (FormatNumericLiteral(converted) is { } literal)
			{
				literals.Add(literal);
				display.Add(NumericLiteralText(literal));
				cultureTypes.Add(NumericKeyword(converted)!);
			}
			else
			{
				literals.Add(arg.ToCSharpString());
				display.Add(ConstantDisplayValue(arg));
				cultureTypes.Add("");
			}
		}

		var equalsCast = valueType switch
		{
			{ SpecialType: SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal } => "",
			{ } t when t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Half" => "global::System.Half",
			_ => null,
		};
		return (literals.MoveToImmutable(), display.MoveToImmutable(), cultureTypes.MoveToImmutable(), equalsCast);
	}

	private static ITypeSymbol? MemberType(ISymbol host) =>
		host switch
		{
			IParameterSymbol p => p.Type,
			IPropertySymbol prop => prop.Type,
			IFieldSymbol field => field.Type,
			_ => null,
		};

	/// <summary>
	/// The values as help and errors show them: numeric constants become runtime-formatted markers (see
	/// <see cref="RuntimeCultureText"/>), so users see them in the culture they type in; other values are the invariant text.
	/// </summary>
	private static IEnumerable<string> CultureDisplayValues(ImmutableArray<string> values, ImmutableArray<string> displayValues, ImmutableArray<string> cultureTypes) =>
		displayValues.Select((display, i) => cultureTypes[i].Length > 0 ? RuntimeCultureText(cultureTypes[i], values[i]) : display);

	/// <summary>The value of an attribute argument as the user types it on the command line.</summary>
	private static string ConstantDisplayValue(TypedConstant v)
	{
		if (v.IsNull)
			return "null";
		return v.Value switch
		{
			string s => s,
			char c => c.ToString(),
			bool b => b ? "true" : "false",
			IFormattable f when v.Type?.TypeKind != TypeKind.Enum => f.ToString(null, CultureInfo.InvariantCulture),
			_ => v.ToCSharpString(),
		};
	}
	private sealed record EmailConstraint : ValidationConstraint;
	private sealed record UrlConstraint : ValidationConstraint;
	private sealed record UriSchemeConstraint(ImmutableArray<string> Schemes) : ValidationConstraint;
	/// <summary>
	/// A <c>Uri</c> that isn't absolute, from <c>[UriOptions]</c>: <paramref name="Kind"/> is <c>relative</c> or
	/// <c>relativeOrAbsolute</c>. Parsing applies it, not a check after binding. Help shows it; <c>__schema</c> doesn't,
	/// because cli-schema v1 has no such constraint kind.
	/// </summary>
	private sealed record UriKindConstraint(string Kind) : ValidationConstraint;
	private sealed record FileExtensionsConstraint(ImmutableArray<string> Extensions) : ValidationConstraint;
	private sealed record ExistingPathConstraint : ValidationConstraint;
	private sealed record NonExistingPathConstraint : ValidationConstraint;
	private sealed record RejectSymbolicLinksConstraint : ValidationConstraint;

}
