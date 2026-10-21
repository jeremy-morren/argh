using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Argh;

public sealed partial class CliParserGenerator
{
	/// <summary>
	/// An <c>[Obsolete]</c> or <c>[Experimental]</c> symbol that the generated code references for one registration.
	/// Generated files suppress the compiler's warnings for these (as the System.Text.Json source generator does for
	/// <c>CS0612</c>/<c>CS0618</c>), and the generator reports each one at the registration call instead, where the user
	/// can see it and suppress it.
	/// </summary>
	/// <param name="DiagnosticId"><c>AGH0036</c>/<c>AGH0037</c>, the <c>DiagnosticId</c> of an <c>[Obsolete]</c> that sets one, or the <c>[Experimental]</c> ID.</param>
	/// <param name="GeneratedCodeSuppressionId">The ID the compiler would report in generated code, when it isn't <c>CS0612</c>/<c>CS0618</c>; otherwise <see langword="null"/>.</param>
	private readonly record struct SymbolUsage(
		string DiagnosticId,
		bool IsError,
		string Message,
		string HelpLink,
		string? GeneratedCodeSuppressionId,
		SourceSpanInfo Span);

	/// <summary>Warning IDs every generated file suppresses: the compiler's obsolete warnings.</summary>
	private static readonly ImmutableArray<string> ObsoleteWarningIds = ImmutableArray.Create("CS0612", "CS0618");

	/// <summary>
	/// Collects the obsolete and experimental symbols that the code generated for <paramref name="invocation"/> references.
	/// Symbols the invocation's own syntax names (type arguments, a method-group handler) are left out, because the
	/// compiler already reports them at the same place.
	/// </summary>
	private static ImmutableArray<SymbolUsage> CollectSymbolUsages(
		InvocationExpressionSyntax invocation,
		IMethodSymbol builderMethod,
		SemanticModel semanticModel,
		CancellationToken ct)
	{
		var walker = new SymbolUsageWalker();
		foreach (var typeArgument in builderMethod.TypeArguments)
			walker.MarkReferencedByCaller(typeArgument);

		var typeArg = builderMethod.TypeArguments.Length > 0 ? builderMethod.TypeArguments[0] as INamedTypeSymbol : null;
		var args = invocation.ArgumentList.Arguments;
		switch (builderMethod.Name)
		{
			case "UseGlobalOptions" or "UseNamespaceOptions" when typeArg is not null:
				walker.AddOptionsType(typeArg);
				break;
			case "UseMiddleware" when typeArg is not null:
				walker.AddMiddleware(typeArg);
				break;
			case "Map" or "MapAndRootAlias" or "MapNamespace" when typeArg is not null:
				walker.AddCommandType(typeArg);
				break;
			case "Map" when args.Count >= 2:
				AddHandler(args[1].Expression);
				break;
			case "MapRoot" when args.Count >= 1:
				AddHandler(args[0].Expression);
				break;
		}

		var location = invocation.Expression is MemberAccessExpressionSyntax member ? member.Name.GetLocation() : invocation.GetLocation();
		var inObsoleteContext = IsInObsoleteContext(semanticModel.GetEnclosingSymbol(invocation.SpanStart, ct));
		return walker.ToUsages(SourceSpanInfo.From(location), inObsoleteContext);

		void AddHandler(ExpressionSyntax handlerExpr)
		{
			// A lambda declares its parameter types in the user's code, where the compiler already reports them.
			if (handlerExpr is LambdaExpressionSyntax)
				return;
			if (ResolveHandlerMethodForAnalyze(semanticModel, handlerExpr) is not { } handler)
				return;
			walker.MarkReferencedByCaller(handler);
			walker.AddMethod(handler);
		}
	}

	/// <summary>The compiler doesn't report obsolete usages inside an <c>[Obsolete]</c> member or type; neither does Argh.</summary>
	private static bool IsInObsoleteContext(ISymbol? symbol)
	{
		for (var s = symbol; s is not null; s = s.ContainingSymbol)
		{
			if (TryGetUsageAttribute(s, "System.ObsoleteAttribute") is not null)
				return true;
		}

		return false;
	}

	private static AttributeData? TryGetUsageAttribute(ISymbol symbol, string attributeMetadataName)
	{
		foreach (var attr in symbol.GetAttributes())
		{
			if (attr.AttributeClass?.ToDisplayString() == attributeMetadataName)
				return attr;
		}

		return null;
	}

	/// <summary>Reports collected usages at the registration call, rebuilding each location in its syntax tree so <c>#pragma</c> applies.</summary>
	private static void ReportSymbolUsages(SourceProductionContext context, ImmutableArray<AnalyzedInvocation> analyzed, Compilation compilation)
	{
		foreach (var ai in analyzed)
		{
			foreach (var usage in ai.SymbolUsages)
			{
				var descriptor = usage.DiagnosticId switch
				{
					"AGH0036" => ObsoleteApiInGeneratedCode,
					"AGH0037" => ObsoleteApiErrorInGeneratedCode,
					_ => new DiagnosticDescriptor(
						usage.DiagnosticId,
						"API used by generated code is obsolete or experimental",
						"{0}",
						"Argh",
						usage.IsError ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
						isEnabledByDefault: true,
						helpLinkUri: usage.HelpLink.Length > 0 ? usage.HelpLink : null)
				};
				context.ReportDiagnostic(Diagnostic.Create(descriptor, ToSourceLocation(usage.Span, compilation), usage.Message));
			}
		}
	}

	private static Location ToSourceLocation(SourceSpanInfo span, Compilation compilation)
	{
		foreach (var tree in compilation.SyntaxTrees)
		{
			if (string.Equals(tree.FilePath, span.FilePath, StringComparison.Ordinal))
				return Location.Create(tree, new TextSpan(span.Start, span.Length));
		}

		return span.ToLocation();
	}

	/// <summary>The <c>#pragma warning disable</c> line every generated file starts with.</summary>
	private static string GeneratedCodeSuppressionPragma(ImmutableArray<AnalyzedInvocation> analyzed)
	{
		var ids = new SortedSet<string>(StringComparer.Ordinal);
		foreach (var ai in analyzed)
		{
			foreach (var usage in ai.SymbolUsages)
			{
				if (usage.GeneratedCodeSuppressionId is { } id)
					ids.Add(id);
			}
		}

		return "#pragma warning disable " + string.Join(", ", ObsoleteWarningIds.Concat(ids.Where(id => !ObsoleteWarningIds.Contains(id))));
	}

	/// <summary>
	/// Walks the symbols the generated code references for one registration, using the same member selection as
	/// the model builders, and records the ones marked <c>[Obsolete]</c> or <c>[Experimental]</c>.
	/// </summary>
	private sealed class SymbolUsageWalker
	{
		private readonly HashSet<ISymbol> _visited = new(SymbolEqualityComparer.Default);
		private readonly HashSet<ISymbol> _referencedByCaller = new(SymbolEqualityComparer.Default);
		private readonly List<(ISymbol Symbol, AttributeData Attribute, bool IsExperimental)> _found = new();

		/// <summary>The caller's syntax names <paramref name="symbol"/> (and its containing types), so the compiler reports it there already.</summary>
		public void MarkReferencedByCaller(ISymbol symbol)
		{
			switch (symbol)
			{
				case INamedTypeSymbol named:
					for (var t = named; t is not null; t = t.ContainingType)
						_referencedByCaller.Add(t.OriginalDefinition);
					foreach (var ta in named.TypeArguments)
						MarkReferencedByCaller(ta);
					break;
				case IArrayTypeSymbol array:
					MarkReferencedByCaller(array.ElementType);
					break;
				case IMethodSymbol method:
					_referencedByCaller.Add(method.OriginalDefinition);
					if (method.ContainingType is { } containing)
						MarkReferencedByCaller(containing);
					break;
			}
		}

		public void AddCommandType(INamedTypeSymbol type)
		{
			AddType(type);
			var hasInstanceCommand = false;
			foreach (var member in type.GetMembers())
			{
				if (member is not IMethodSymbol method || method.MethodKind != MethodKind.Ordinary) continue;
				if (method.AssociatedSymbol is not null) continue;
				if (method.DeclaredAccessibility != Accessibility.Public) continue;
				hasInstanceCommand |= !method.IsStatic;
				AddMethod(method);
			}

			if (hasInstanceCommand)
				Visit(FindParameterlessConstructor(type));
		}

		public void AddMethod(IMethodSymbol method)
		{
			Visit(method);
			if (method.ContainingType is { } containing)
				AddType(containing);
			foreach (var p in method.Parameters)
				AddParameter(p);
			// Same attributes as CommandModel.CollectCommandMiddleware: [Middleware<T>] on the handler.
			foreach (var attr in method.GetAttributes())
			{
				if (attr.AttributeClass is { Name: "MiddlewareAttribute", TypeArguments.Length: 1 } ac
				    && ac.TypeArguments[0] is INamedTypeSymbol middleware && middleware.TypeKind != TypeKind.Error)
					AddMiddleware(middleware);
			}
		}

		public void AddOptionsType(INamedTypeSymbol type)
		{
			AddType(type);
			Visit(FindParameterlessConstructor(type));
			// Same members as BuildFlattenedOptionsMembers: public instance get/set properties and public instance fields.
			for (var t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
			{
				foreach (var member in t.GetMembers())
				{
					switch (member)
					{
						case IPropertySymbol prop when prop.DeclaredAccessibility == Accessibility.Public && !prop.IsStatic && !prop.IsIndexer
						                                && prop.GetMethod is not null && prop.SetMethod is not null:
							AddMember(prop, prop.Type);
							break;
						case IFieldSymbol field when field.DeclaredAccessibility == Accessibility.Public && !field.IsStatic && !field.IsImplicitlyDeclared:
							AddMember(field, field.Type);
							break;
					}
				}
			}
		}

		public void AddMiddleware(INamedTypeSymbol type)
		{
			AddType(type);
			Visit(FindParameterlessConstructor(type));
		}

		private void AddParameter(IParameterSymbol p)
		{
			if (IsInjectedType(p.Type))
			{
				AddType(p.Type);
				return;
			}

			if (HasAsParametersAttribute(p) && p.Type is INamedTypeSymbol dto && dto.TypeKind != TypeKind.Error)
			{
				AddAsParametersType(dto);
				return;
			}

			AddValue(p, p.Type);
		}

		/// <summary>Same members as FlattenAsParametersTypeAcc: the primary constructor, then settable public instance properties.</summary>
		private void AddAsParametersType(INamedTypeSymbol type)
		{
			AddType(type);
			var ctorNames = new HashSet<string>(StringComparer.Ordinal);
			if (TryGetPrimaryConstructor(type) is { } primary)
			{
				Visit(primary);
				foreach (var cp in primary.Parameters)
				{
					ctorNames.Add(cp.Name);
					AddValue(cp, cp.Type);
					if (TryFindCtorMirroredProperty(type, cp.Name) is { } mirrored)
						AddParser(mirrored);
				}
			}
			else
			{
				Visit(FindParameterlessConstructor(type));
			}

			for (var t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
			{
				foreach (var member in t.GetMembers())
				{
					if (member is not IPropertySymbol prop) continue;
					if (prop.DeclaredAccessibility != Accessibility.Public || prop.IsStatic || prop.IsIndexer) continue;
					if (!IsSettableForAsParameters(prop) || ctorNames.Contains(prop.Name)) continue;
					AddMember(prop, prop.Type);
				}
			}
		}

		private void AddMember(ISymbol member, ITypeSymbol type)
		{
			Visit(member);
			if (member is IPropertySymbol prop)
			{
				Visit(prop.GetMethod);
				Visit(prop.SetMethod);
			}

			AddValue(member, type);
		}

		private void AddValue(ISymbol owner, ITypeSymbol type)
		{
			AddType(type);
			AddParser(owner);
		}

		private void AddParser(ISymbol owner)
		{
			var attr = TryGetUsageAttribute(owner, "Nullean.Argh.ArgumentParserAttribute");
			if (attr is null || attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Value is not INamedTypeSymbol parser)
				return;
			AddType(parser);
			Visit(FindParameterlessConstructor(parser));
			foreach (var member in parser.GetMembers("TryParse"))
				Visit(member);
		}

		private void AddType(ITypeSymbol? type)
		{
			switch (type)
			{
				case IArrayTypeSymbol array:
					AddType(array.ElementType);
					break;
				case INamedTypeSymbol named:
					foreach (var ta in named.TypeArguments)
						AddType(ta);
					for (var t = named; t is not null; t = t.ContainingType)
						Visit(t.OriginalDefinition);
					if (named.TypeKind == TypeKind.Enum)
					{
						// The generated parse switch names every member (see GetEnumMemberNames).
						foreach (var m in named.GetMembers())
						{
							if (m is IFieldSymbol { HasConstantValue: true, IsImplicitlyDeclared: false })
								Visit(m);
						}
					}

					break;
			}
		}

		private void Visit(ISymbol? symbol)
		{
			if (symbol is null || !_visited.Add(symbol.OriginalDefinition) || _referencedByCaller.Contains(symbol.OriginalDefinition))
				return;
			if (TryGetUsageAttribute(symbol, "System.ObsoleteAttribute") is { } obsolete)
				_found.Add((symbol, obsolete, false));
			var experimental = TryGetUsageAttribute(symbol, "System.Diagnostics.CodeAnalysis.ExperimentalAttribute");
			// An [Experimental] assembly or module makes every type in it experimental, as it does for the compiler.
			if (experimental is null && symbol is INamedTypeSymbol)
				experimental = (symbol.ContainingModule is { } module ? TryGetUsageAttribute(module, "System.Diagnostics.CodeAnalysis.ExperimentalAttribute") : null)
				               ?? (symbol.ContainingAssembly is { } assembly ? TryGetUsageAttribute(assembly, "System.Diagnostics.CodeAnalysis.ExperimentalAttribute") : null);
			if (experimental is not null)
				_found.Add((symbol, experimental, true));
		}

		private static IMethodSymbol? FindParameterlessConstructor(INamedTypeSymbol type) =>
			type.InstanceConstructors.FirstOrDefault(c => c.Parameters.Length == 0 && !c.IsImplicitlyDeclared);

		public ImmutableArray<SymbolUsage> ToUsages(SourceSpanInfo span, bool inObsoleteContext)
		{
			if (_found.Count == 0)
				return ImmutableArray<SymbolUsage>.Empty;

			var usages = ImmutableArray.CreateBuilder<SymbolUsage>(_found.Count);
			foreach (var (symbol, attribute, isExperimental) in _found)
			{
				var display = symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
				var urlFormat = NamedString(attribute, "UrlFormat");
				if (isExperimental)
				{
					// [Experimental] requires a diagnostic ID; the compiler reports the usage under it.
					if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not string { Length: > 0 } id)
						continue;
					var message = NamedString(attribute, "Message") is { Length: > 0 } custom
						? $"'{display}' is for evaluation purposes only and is subject to change or removal in future updates: '{custom}' Argh's generated code uses it for this registration."
						: $"'{display}' is for evaluation purposes only and is subject to change or removal in future updates. Argh's generated code uses it for this registration. Suppress this diagnostic to proceed.";
					usages.Add(new SymbolUsage(id, IsError: true, message, FormatHelpLink(urlFormat, id), id, span));
					continue;
				}

				if (inObsoleteContext)
					continue;
				var obsoleteMessage = attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string m && m.Length > 0 ? m : null;
				var isError = attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is true;
				var customId = NamedString(attribute, "DiagnosticId") is { Length: > 0 } cid ? cid : null;
				var diagnosticId = customId ?? (isError ? "AGH0037" : "AGH0036");
				var text = obsoleteMessage is null
					? $"'{display}' is obsolete. Argh's generated code uses it for this registration."
					: $"'{display}' is obsolete: '{obsoleteMessage}' Argh's generated code uses it for this registration.";
				usages.Add(new SymbolUsage(diagnosticId, isError, text, FormatHelpLink(urlFormat, diagnosticId), customId, span));
			}

			return usages.ToImmutable();
		}

		private static string? NamedString(AttributeData attribute, string name)
		{
			foreach (var kv in attribute.NamedArguments)
			{
				if (kv.Key == name && kv.Value.Value is string s)
					return s;
			}

			return null;
		}

		private static string FormatHelpLink(string? urlFormat, string id)
		{
			if (string.IsNullOrEmpty(urlFormat))
				return "";
			try
			{
				return string.Format(CultureInfo.InvariantCulture, urlFormat!, id);
			}
			catch (FormatException)
			{
				return urlFormat!;
			}
		}
	}
}
