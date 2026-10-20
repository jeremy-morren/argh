using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Nullean.Argh;

public sealed partial class CliParserGenerator
{
	private static void EmitRootCompleteBlock(StringBuilder sb, string indent)
	{
		sb.AppendLine(indent + "if (CompletionProtocol.IsCompleteInvocation(args))");
		sb.AppendLine(indent + "{");
		sb.AppendLine(indent + "\tif (!CompletionProtocol.TryParseCompleteInvocation(args, out var __shell, out var __words))");
		sb.AppendLine(indent + "\t{");
		sb.AppendLine(indent + "\t\tConsole.Error.WriteLine(\"Error: expected '__complete <bash|zsh|fish> -- [words...]'\");");
		sb.AppendLine(indent + "\t\treturn 2;");
		sb.AppendLine(indent + "\t}");
		sb.AppendLine(indent + "\tComplete(__shell, __words);");
		sb.AppendLine(indent + "\treturn 0;");
		sb.AppendLine(indent + "}");
		sb.AppendLine();
	}

	private static void EmitCompletionForApp(StringBuilder sb, AppEmitModel app)
	{
		EmitCompletionHierarchical(sb, app);
	}


	private static void EmitCompletionHierarchical(StringBuilder sb, AppEmitModel app)
	{
		var globalMembers = app.GlobalOptionsModel is { FlattenedMembers.Length: > 0 }
			? app.GlobalOptionsModel.FlattenedMembers
			: ImmutableArray<ParameterModel>.Empty;

		sb.AppendLine("\t\tprivate static readonly string[] __c_meta_root = new string[] { \"--help\", \"-h\", \"--version\" };");
		EmitCompletionStringArray(sb, "__c_flags_global", CollectCompletionFlagStrings(globalMembers).ToList());
		EmitNextSegmentsArray(sb, "__c_next_Root", app.Root);

		foreach ((var node, var path) in EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty))
		{
			var key = CommandNamespacePathKey(path);
			EmitNextSegmentsArray(sb, $"__c_next_{key}", node);
			var nsMembers = node.CommandNamespaceOptionsModel is { FlattenedMembers.Length: > 0 }
				? node.CommandNamespaceOptionsModel.FlattenedMembers
				: ImmutableArray<ParameterModel>.Empty;
			var mergedNs = MergeCompletionFlags(globalMembers, nsMembers);
			mergedNs.AddRange(s_helpFlags);
			mergedNs.Sort(StringComparer.OrdinalIgnoreCase);
			EmitCompletionStringArray(sb, $"__c_flags_ns_{key}", mergedNs);
		}

		foreach (var cmd in app.AllCommands.Where(c => !c.IsRootDefault))
		{
			var chain = BuildOptionsInjectionChain(app, cmd);
			var list = new List<string>(s_helpFlags);
			list.AddRange(CollectCompletionFlagStrings(globalMembers));
			foreach (var (_, _, _, _, _, flat, _) in chain)
				list.AddRange(CollectCompletionFlagStrings(flat));
			list.AddRange(CollectCompletionFlagStrings(cmd.Parameters));
			var distinct = list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.Ordinal).ToList();
			EmitCompletionStringArray(sb, $"__c_flags_cmd_{cmd.RunMethodName}", distinct);
		}

		EmitCompleteHierarchicalBody(sb, app, globalMembers);
		EmitFlagValueCompletion(sb, app, globalMembers);
	}

	private static readonly string[] s_helpFlags = { "--help", "-h" };

	/// <summary>
	/// Emits value tables for flags with a closed set of values (enums, enum collections, <c>[AllowedValues]</c>) and
	/// <c>TryWriteFlagValues</c>, which completes them after <c>--flag </c>, <c>--flag=</c> (one word) or <c>--flag = </c> (bash splits at <c>=</c>).
	/// </summary>
	private static void EmitFlagValueCompletion(StringBuilder sb, AppEmitModel app, ImmutableArray<ParameterModel> globalMembers)
	{
		var tables = new Dictionary<string, string>(StringComparer.Ordinal);
		var scopes = new List<(string Scope, List<(List<string> Spellings, string Table)> Flags)>();

		void AddScope(string scope, IEnumerable<ParameterModel> members)
		{
			var flags = new List<(List<string>, string)>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var p in members)
			{
				var values = CompletionValuesFor(p);
				if (values is null)
					continue;
				var spellings = ExpandFlagStrings(p).Where(s => !s.StartsWith("--no-", StringComparison.Ordinal) && seen.Add(s)).ToList();
				if (spellings.Count == 0)
					continue;
				var key = string.Join("\n", values);
				if (!tables.TryGetValue(key, out var table))
				{
					table = $"__c_values_{tables.Count}";
					tables.Add(key, table);
					EmitCompletionStringArray(sb, table, values);
				}

				flags.Add((spellings, table));
			}

			if (flags.Count > 0)
				scopes.Add((scope, flags));
		}

		// Innermost members first so they win over a global or namespace flag spelled the same way.
		AddScope("Root", globalMembers);
		foreach ((var node, var path) in EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty))
		{
			var nsMembers = node.CommandNamespaceOptionsModel is { FlattenedMembers.Length: > 0 }
				? node.CommandNamespaceOptionsModel.FlattenedMembers
				: ImmutableArray<ParameterModel>.Empty;
			AddScope(CommandNamespacePathKey(path), nsMembers.Concat(globalMembers));
		}

		foreach (var cmd in app.AllCommands.Where(c => !c.IsRootDefault))
		{
			var members = new List<ParameterModel>(cmd.Parameters);
			foreach (var (_, _, _, _, _, flat, _) in BuildOptionsInjectionChain(app, cmd).Reverse())
				members.AddRange(flat);
			members.AddRange(globalMembers);
			AddScope("cmd:" + cmd.RunMethodName, members);
		}

		sb.AppendLine("\t\tprivate static bool TryWriteFlagValues(string scope, string[] committed, int start, string partial)");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\tif (partial.Length > 2 && partial[0] == '-' && partial[1] == '-')");
		sb.AppendLine("\t\t\t{");
		sb.AppendLine("\t\t\t\tvar eq = partial.IndexOf('=');");
		sb.AppendLine("\t\t\t\treturn eq > 2 && WriteFlagValues(scope, partial.Substring(0, eq), partial.Substring(eq + 1), partial.Substring(0, eq + 1));");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t\tif (partial.Length > 0 && partial[0] == '-')");
		sb.AppendLine("\t\t\t\treturn false;");
		sb.AppendLine("\t\t\tvar n = committed.Length;");
		sb.AppendLine("\t\t\tif (n - start >= 1 && committed[n - 1].Length > 1 && committed[n - 1][0] == '-')");
		sb.AppendLine("\t\t\t\treturn WriteFlagValues(scope, committed[n - 1], partial, \"\");");
		sb.AppendLine("\t\t\tif (n - start >= 2 && committed[n - 1] == \"=\" && committed[n - 2].Length > 1 && committed[n - 2][0] == '-')");
		sb.AppendLine("\t\t\t\treturn WriteFlagValues(scope, committed[n - 2], partial, \"\");");
		sb.AppendLine("\t\t\treturn false;");
		sb.AppendLine("\t\t}");
		sb.AppendLine();

		sb.AppendLine("\t\tprivate static bool WriteFlagValues(string scope, string flag, string partial, string insertPrefix)");
		sb.AppendLine("\t\t{");
		if (scopes.Count == 0)
		{
			sb.AppendLine("\t\t\t_ = scope; _ = flag; _ = partial; _ = insertPrefix;");
			sb.AppendLine("\t\t\treturn false;");
			sb.AppendLine("\t\t}");
			sb.AppendLine();
			return;
		}

		sb.AppendLine("\t\t\tswitch (scope)");
		sb.AppendLine("\t\t\t{");
		foreach (var (scope, flags) in scopes)
		{
			sb.AppendLine($"\t\t\t\tcase \"{Escape(scope)}\":");
			foreach (var (spellings, table) in flags)
			{
				var conditions = spellings.Select(s =>
					$"string.Equals(flag, \"{Escape(s)}\", StringComparison.{(s.StartsWith("--", StringComparison.Ordinal) ? "OrdinalIgnoreCase" : "Ordinal")})");
				sb.AppendLine($"\t\t\t\t\tif ({string.Join(" || ", conditions)})");
				sb.AppendLine("\t\t\t\t\t{");
				sb.AppendLine($"\t\t\t\t\t\tCompletionWriter.WriteFiltered({table}, partial, insertPrefix);");
				sb.AppendLine("\t\t\t\t\t\treturn true;");
				sb.AppendLine("\t\t\t\t\t}");
			}

			sb.AppendLine("\t\t\t\t\treturn false;");
		}

		sb.AppendLine("\t\t\t\tdefault:");
		sb.AppendLine("\t\t\t\t\treturn false;");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t}");
		sb.AppendLine();
	}

	/// <summary>The closed set of values a flag accepts, or <see langword="null"/> when its values are open-ended.</summary>
	private static List<string>? CompletionValuesFor(ParameterModel p)
	{
		if (p.Kind != ParameterKind.Flag && p.Kind != ParameterKind.OptionsInjected)
			return null;
		if (p.ScalarKind == CliScalarKind.Enum && !p.EnumMemberNames.IsDefaultOrEmpty)
			return p.EnumMemberNames.Select((m, i) => ResolveEnumMemberCliName(p.EnumMemberCliNames, i, m)).ToList();
		if (p.IsCollection && p.ElementScalarKind == CliScalarKind.Enum && !p.ElementEnumMemberNames.IsDefaultOrEmpty)
			return p.ElementEnumMemberNames.Select((m, i) => ResolveEnumMemberCliName(p.ElementEnumMemberCliNames, i, m)).ToList();
		if (!p.Validations.IsDefaultOrEmpty)
		{
			foreach (var c in p.Validations)
			{
				if (c is AllowedValuesConstraint av)
					return av.Values.Zip(av.DisplayValues, (literal, value) => (literal, value))
						.Where(x => x.literal != "null")
						.Select(x => x.value)
						.ToList();
			}
		}

		return null;
	}

	private static void EmitCompleteHierarchicalBody(StringBuilder sb, AppEmitModel app, ImmutableArray<ParameterModel> globalMembers)
	{
		sb.AppendLine("\t\tprivate static void Complete(CompletionShell shell, ReadOnlySpan<string> words)");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\t_ = shell;");
		sb.AppendLine("\t\t\tif (words.Length == 0)");
		sb.AppendLine("\t\t\t{");
		sb.AppendLine("\t\t\t\tCompletionWriter.WriteFiltered(__c_next_Root, \"\");");
		sb.AppendLine("\t\t\t\treturn;");
		sb.AppendLine("\t\t\t}");
		// Index and Range need System.Index/System.Range (.NET Core 3.0, .NET Standard 2.1); Slice works everywhere.
		sb.AppendLine("#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER");
		sb.AppendLine("\t\t\tvar committed = words.Length > 1 ? words[..^1].ToArray() : Array.Empty<string>();");
		sb.AppendLine("\t\t\tvar partial = words[^1];");
		sb.AppendLine("#else");
		sb.AppendLine("\t\t\tvar committed = words.Length > 1 ? words.Slice(0, words.Length - 1).ToArray() : Array.Empty<string>();");
		sb.AppendLine("\t\t\tvar partial = words[words.Length - 1];");
		sb.AppendLine("#endif");
		sb.AppendLine("\t\t\tvar pos = 0;");
		if (globalMembers.Length > 0)
			sb.AppendLine("\t\t\tpos = ConsumeForComplete_Global(committed, pos, committed.Length);");

		sb.AppendLine("\t\t\tif (pos == committed.Length)");
		sb.AppendLine("\t\t\t{");
		sb.AppendLine("\t\t\t\tif (TryWriteFlagValues(\"Root\", committed, 0, partial))");
		sb.AppendLine("\t\t\t\t\treturn;");
		sb.AppendLine("\t\t\t\tif (partial.Length > 0 && partial[0] == '-')");
		sb.AppendLine("\t\t\t\t\tCompletionWriter.WriteFiltered(MergeSpans(__c_flags_global, __c_meta_root), partial);");
		sb.AppendLine("\t\t\t\telse");
		sb.AppendLine("\t\t\t\t\tCompletionWriter.WriteFiltered(__c_next_Root, partial);");
		sb.AppendLine("\t\t\t\treturn;");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t\tvar current = \"Root\";");
		sb.AppendLine("\t\t\twhile (pos < committed.Length)");
		sb.AppendLine("\t\t\t{");
		sb.AppendLine("\t\t\t\tvar tok = committed[pos];");
		sb.AppendLine("\t\t\t\tif (tok.Length > 0 && tok[0] == '-')");
		sb.AppendLine("\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\tTryWriteFlagValues(current, committed, pos, partial);");
		sb.AppendLine("\t\t\t\t\treturn;");
		sb.AppendLine("\t\t\t\t}");
		sb.AppendLine("\t\t\t\tif (TryDescendNamespace(ref current, tok))");
		sb.AppendLine("\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\tpos++;");
		sb.AppendLine("\t\t\t\t\tvar nsStart = pos;");
		sb.AppendLine("\t\t\t\t\tpos = ConsumeForNamespace(current, committed, pos, committed.Length);");
		sb.AppendLine("\t\t\t\t\tif (pos == committed.Length)");
		sb.AppendLine("\t\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\t\tif (!TryWriteFlagValues(current, committed, nsStart, partial))");
		sb.AppendLine("\t\t\t\t\t\t\tWriteAtNamespace(current, partial);");
		sb.AppendLine("\t\t\t\t\t\treturn;");
		sb.AppendLine("\t\t\t\t\t}");
		sb.AppendLine("\t\t\t\t\tcontinue;");
		sb.AppendLine("\t\t\t\t}");
		sb.AppendLine("\t\t\t\tif (TryMatchCommand(current, tok, out var runName))");
		sb.AppendLine("\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\tpos++;");
		sb.AppendLine("\t\t\t\t\tif (TryWriteFlagValues(\"cmd:\" + runName, committed, pos, partial))");
		sb.AppendLine("\t\t\t\t\t\treturn;");
		sb.AppendLine("\t\t\t\t\tpos = ConsumeCommandTail(runName, committed, pos, committed.Length);");
		sb.AppendLine("\t\t\t\t\tif (partial.Length > 0 && partial[0] == '-')");
		sb.AppendLine("\t\t\t\t\t\tCompletionWriter.WriteFiltered(FlagsForCommand(runName), partial);");
		sb.AppendLine("\t\t\t\t\treturn;");
		sb.AppendLine("\t\t\t\t}");
		sb.AppendLine("\t\t\t\treturn;");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t}");
		sb.AppendLine();

		EmitConsumeForCompleteGlobal(sb, globalMembers);
		EmitMergeSpansHelper(sb);
		EmitFlagsForCommandSwitch(sb, app);
		EmitConsumeCommandTailSwitch(sb, app);
		EmitNamespaceConsumeAndDescend(sb, app);
		EmitTryDescendNamespace(sb, app);
		EmitTryMatchCommandMethod(sb, app);
		EmitWriteAtNamespace(sb, app);
	}

	private static void EmitMergeSpansHelper(StringBuilder sb)
	{
		sb.AppendLine("\t\tprivate static ReadOnlySpan<string> MergeSpans(ReadOnlySpan<string> a, ReadOnlySpan<string> b)");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\tif (a.Length == 0) return b;");
		sb.AppendLine("\t\t\tif (b.Length == 0) return a;");
		sb.AppendLine("\t\t\tvar r = new string[a.Length + b.Length];");
		sb.AppendLine("\t\t\ta.CopyTo(r.AsSpan(0, a.Length));");
		sb.AppendLine("\t\t\tb.CopyTo(r.AsSpan(a.Length));");
		sb.AppendLine("\t\t\treturn r;");
		sb.AppendLine("\t\t}");
		sb.AppendLine();
	}

	private static void EmitFlagsForCommandSwitch(StringBuilder sb, AppEmitModel app)
	{
		sb.AppendLine("\t\tprivate static ReadOnlySpan<string> FlagsForCommand(string runMethodName)");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\tswitch (runMethodName)");
		sb.AppendLine("\t\t\t{");
		foreach (var cmd in app.AllCommands.Where(c => !c.IsRootDefault))
			sb.AppendLine($"\t\t\t\tcase \"{Escape(cmd.RunMethodName)}\": return __c_flags_cmd_{cmd.RunMethodName};");

		sb.AppendLine("\t\t\t\tdefault: return default;");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t}");
		sb.AppendLine();
	}

	private static void EmitConsumeCommandTailSwitch(StringBuilder sb, AppEmitModel app)
	{
		sb.AppendLine("\t\tprivate static int ConsumeCommandTail(string runMethodName, string[] committed, int pos, int end)");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\tswitch (runMethodName)");
		sb.AppendLine("\t\t\t{");
		foreach (var cmd in app.AllCommands.Where(c => !c.IsRootDefault))
			sb.AppendLine($"\t\t\t\tcase \"{Escape(cmd.RunMethodName)}\": return ConsumeForComplete_Command_{cmd.RunMethodName}(committed, pos, end);");

		sb.AppendLine("\t\t\t\tdefault: return pos;");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t}");
		sb.AppendLine();
		foreach (var cmd in app.AllCommands.Where(c => !c.IsRootDefault))
			EmitOptionsConsumeForComplete(sb, $"ConsumeForComplete_Command_{cmd.RunMethodName}", cmd.Parameters);
	}

	private static void EmitWriteAtNamespace(StringBuilder sb, AppEmitModel app)
	{
		sb.AppendLine("\t\tprivate static void WriteAtNamespace(string current, string partial)");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\tif (partial.Length > 0 && partial[0] == '-')");
		sb.AppendLine("\t\t\t{");
		sb.AppendLine("\t\t\t\tswitch (current)");
		sb.AppendLine("\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\tcase \"Root\":");
		sb.AppendLine("\t\t\t\t\t\tCompletionWriter.WriteFiltered(MergeSpans(__c_flags_global, __c_meta_root), partial);");
		sb.AppendLine("\t\t\t\t\t\tbreak;");
		foreach ((var _, var path) in EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty))
		{
			var key = CommandNamespacePathKey(path);
			sb.AppendLine($"\t\t\t\t\tcase \"{Escape(key)}\":");
			sb.AppendLine($"\t\t\t\t\t\tCompletionWriter.WriteFiltered(__c_flags_ns_{key}, partial);");
			sb.AppendLine("\t\t\t\t\t\tbreak;");
		}

		sb.AppendLine("\t\t\t\t\tdefault:");
		sb.AppendLine("\t\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\t}");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t\telse");
		sb.AppendLine("\t\t\t{");
		sb.AppendLine("\t\t\t\tswitch (current)");
		sb.AppendLine("\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\tcase \"Root\":");
		sb.AppendLine("\t\t\t\t\t\tCompletionWriter.WriteFiltered(__c_next_Root, partial);");
		sb.AppendLine("\t\t\t\t\t\tbreak;");
		foreach ((var node, var path) in EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty))
		{
			var key = CommandNamespacePathKey(path);
			sb.AppendLine($"\t\t\t\t\tcase \"{Escape(key)}\":");
			sb.AppendLine($"\t\t\t\t\t\tCompletionWriter.WriteFiltered(__c_next_{key}, partial);");
			sb.AppendLine("\t\t\t\t\t\tbreak;");
		}

		sb.AppendLine("\t\t\t\t\tdefault:");
		sb.AppendLine("\t\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\t}");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t}");
		sb.AppendLine();
	}

	private static void EmitTryDescendNamespace(StringBuilder sb, AppEmitModel app)
	{
		sb.AppendLine("\t\tprivate static bool TryDescendNamespace(ref string current, string tok)");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\tswitch (current)");
		sb.AppendLine("\t\t\t{");
		sb.AppendLine("\t\t\t\tcase \"Root\":");
		foreach (var ch in app.Root.Children)
		{
			var k = CommandNamespacePathKey(AppendSegment(ImmutableArray<string>.Empty, ch.Segment));
			sb.AppendLine(
				$"\t\t\t\t\tif (string.Equals(tok, \"{Escape(ch.Segment)}\", StringComparison.OrdinalIgnoreCase)) {{ current = \"{Escape(k)}\"; return true; }}");
		}

		sb.AppendLine("\t\t\t\t\treturn false;");
		foreach ((var node, var path) in EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty))
		{
			var parentKey = CommandNamespacePathKey(path);
			if (node.Children.Count == 0)
				continue;
			sb.AppendLine($"\t\t\t\tcase \"{Escape(parentKey)}\":");
			foreach (var ch in node.Children)
			{
				var childPath = AppendSegment(path, ch.Segment);
				var k = CommandNamespacePathKey(childPath);
				sb.AppendLine(
					$"\t\t\t\t\tif (string.Equals(tok, \"{Escape(ch.Segment)}\", StringComparison.OrdinalIgnoreCase)) {{ current = \"{Escape(k)}\"; return true; }}");
			}

			sb.AppendLine("\t\t\t\t\treturn false;");
		}

		sb.AppendLine("\t\t\t\tdefault:");
		sb.AppendLine("\t\t\t\t\treturn false;");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t}");
		sb.AppendLine();
	}

	private static void EmitTryMatchCommandMethod(StringBuilder sb, AppEmitModel app)
	{
		sb.AppendLine("\t\tprivate static bool TryMatchCommand(string current, string tok, out string runMethodName)");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\trunMethodName = \"\";");
		sb.AppendLine("\t\t\tswitch (current)");
		sb.AppendLine("\t\t\t{");
		sb.AppendLine("\t\t\t\tcase \"Root\":");
		foreach (var cmd in app.AllCommands.Where(c => !c.IsRootDefault && c.RoutePrefix.IsDefaultOrEmpty))
		{
			sb.AppendLine(
				$"\t\t\t\t\tif (string.Equals(tok, \"{Escape(cmd.CommandName)}\", StringComparison.OrdinalIgnoreCase)) {{ runMethodName = \"{Escape(cmd.RunMethodName)}\"; return true; }}");
		}

		sb.AppendLine("\t\t\t\t\treturn false;");
		foreach ((var node, var path) in EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty))
		{
			var key = CommandNamespacePathKey(path);
			if (node.Commands.Count == 0)
				continue;
			sb.AppendLine($"\t\t\t\tcase \"{Escape(key)}\":");
			foreach (var cmd in node.Commands)
			{
				sb.AppendLine(
					$"\t\t\t\t\tif (string.Equals(tok, \"{Escape(cmd.CommandName)}\", StringComparison.OrdinalIgnoreCase)) {{ runMethodName = \"{Escape(cmd.RunMethodName)}\"; return true; }}");
			}

			sb.AppendLine("\t\t\t\t\treturn false;");
		}

		sb.AppendLine("\t\t\t\tdefault:");
		sb.AppendLine("\t\t\t\t\treturn false;");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t}");
		sb.AppendLine();
	}

	private static void EmitNamespaceConsumeAndDescend(StringBuilder sb, AppEmitModel app)
	{
		sb.AppendLine("\t\tprivate static int ConsumeForNamespace(string current, string[] committed, int pos, int end)");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\tswitch (current)");
		sb.AppendLine("\t\t\t{");
		sb.AppendLine("\t\t\t\tcase \"Root\":");
		sb.AppendLine("\t\t\t\t\treturn pos;");
		foreach ((var node, var path) in EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty))
		{
			var key = CommandNamespacePathKey(path);
			var members = node.CommandNamespaceOptionsModel is { FlattenedMembers.Length: > 0 }
				? node.CommandNamespaceOptionsModel.FlattenedMembers
				: ImmutableArray<ParameterModel>.Empty;
			if (members.IsEmpty)
			{
				sb.AppendLine($"\t\t\t\tcase \"{Escape(key)}\":");
				sb.AppendLine("\t\t\t\t\treturn pos;");
				continue;
			}

			sb.AppendLine($"\t\t\t\tcase \"{Escape(key)}\":");
			sb.AppendLine($"\t\t\t\t\treturn ConsumeForComplete_Namespace_{key}(committed, pos, end);");
		}

		sb.AppendLine("\t\t\t\tdefault:");
		sb.AppendLine("\t\t\t\t\treturn pos;");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t}");
		sb.AppendLine();
		foreach ((var node, var path) in EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty))
		{
			var key = CommandNamespacePathKey(path);
			var members = node.CommandNamespaceOptionsModel is { FlattenedMembers.Length: > 0 }
				? node.CommandNamespaceOptionsModel.FlattenedMembers
				: ImmutableArray<ParameterModel>.Empty;
			if (members.IsEmpty)
				continue;
			EmitOptionsConsumeForComplete(sb, $"ConsumeForComplete_Namespace_{key}", members);
		}
	}

	private static void EmitConsumeForCompleteGlobal(StringBuilder sb, ImmutableArray<ParameterModel> globalMembers)
	{
		if (globalMembers.IsEmpty)
		{
			sb.AppendLine("\t\tprivate static int ConsumeForComplete_Global(string[] committed, int pos, int end) => pos;");
			sb.AppendLine();
			return;
		}

		EmitOptionsConsumeForComplete(sb, "ConsumeForComplete_Global", globalMembers);
	}

	private static void EmitOptionsConsumeForComplete(StringBuilder sb, string methodName, ImmutableArray<ParameterModel> members)
	{
		var syn = SyntheticOptionsCommand(members, methodName);
		sb.AppendLine($"\t\tprivate static int {methodName}(string[] args, int pos, int end)");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\tvar flags = new System.Collections.Generic.Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);");
		sb.AppendLine("\t\t\tvar idx = new int[] { pos };");
		EmitBoolSwitchNames(sb, syn, suppressNoNameHelper: true);
		EmitCanonFlagNameMethod(sb, syn);
		EmitShortFlagMethods(sb, syn, multiFlagsAvailable: false);
		EmitAllowedFlagPredicate(sb, members);
		sb.AppendLine("\t\t\twhile (idx[0] < end && idx[0] < args.Length && args[idx[0]].Length > 0 && args[idx[0]][0] == '-')");
		sb.AppendLine("\t\t\t{");
		sb.AppendLine("\t\t\t\tif (args[idx[0]] == \"--help\" || args[idx[0]] == \"-h\" || args[idx[0]] == \"--version\")");
		sb.AppendLine("\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\tvar a = args[idx[0]];");
		sb.AppendLine("\t\t\t\tif (a.StartsWith(\"--\", StringComparison.Ordinal))");
		sb.AppendLine("\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\tvar eq = a.IndexOf('=');");
		sb.AppendLine("\t\t\t\t\tif (eq >= 0)");
		sb.AppendLine("\t\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\t\tvar flagName = CanonFlagName(a.Substring(2, eq - 2));");
		sb.AppendLine("\t\t\t\t\t\tif (!IsAllowedFlag(flagName))");
		sb.AppendLine("\t\t\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\t\t\tidx[0]++;");
		sb.AppendLine("\t\t\t\t\t}");
		sb.AppendLine("\t\t\t\t\telse");
		sb.AppendLine("\t\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\t\tvar flagName = CanonFlagName(a.Substring(2));");
		sb.AppendLine("\t\t\t\t\t\tif (!IsAllowedFlag(flagName))");
		sb.AppendLine("\t\t\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\t\t\tif (IsBoolSwitchName(flagName))");
		sb.AppendLine("\t\t\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\t\t\tidx[0]++;");
		sb.AppendLine("\t\t\t\t\t\t}");
		sb.AppendLine("\t\t\t\t\t\telse");
		sb.AppendLine("\t\t\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\t\t\tif (idx[0] + 1 >= end || idx[0] + 1 >= args.Length)");
		sb.AppendLine("\t\t\t\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\t\t\t\tidx[0] += 2;");
		sb.AppendLine("\t\t\t\t\t\t}");
		sb.AppendLine("\t\t\t\t\t}");
		sb.AppendLine("\t\t\t\t\tcontinue;");
		sb.AppendLine("\t\t\t\t}");
		sb.AppendLine("\t\t\t\tif (a.Length >= 2 && a[0] == '-' && a[1] != '-')");
		sb.AppendLine("\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\tvar eqs = a.IndexOf('=');");
		sb.AppendLine("\t\t\t\t\tif (eqs >= 0)");
		sb.AppendLine("\t\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\t\tvar shortKey = a.Substring(1, eqs - 1);");
		sb.AppendLine("\t\t\t\t\t\tif (shortKey.Length != 1)");
		sb.AppendLine("\t\t\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\t\t\tif (!TryApplyShortFlag(shortKey[0], a.Substring(eqs + 1)))");
		sb.AppendLine("\t\t\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\t\t\tidx[0]++;");
		sb.AppendLine("\t\t\t\t\t\tcontinue;");
		sb.AppendLine("\t\t\t\t\t}");
		sb.AppendLine("\t\t\t\t\tif (a.Length == 2)");
		sb.AppendLine("\t\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\t\tvar sc = a[1];");
		sb.AppendLine("\t\t\t\t\t\tif (IsShortBoolChar(sc))");
		sb.AppendLine("\t\t\t\t\t\t{");
		sb.AppendLine("\t\t\t\t\t\t\tif (!TryApplyShortFlag(sc, \"true\"))");
		sb.AppendLine("\t\t\t\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\t\t\t\tidx[0]++;");
		sb.AppendLine("\t\t\t\t\t\t\tcontinue;");
		sb.AppendLine("\t\t\t\t\t\t}");
		sb.AppendLine("\t\t\t\t\t\tif (idx[0] + 1 >= end || idx[0] + 1 >= args.Length)");
		sb.AppendLine("\t\t\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\t\t\tif (!TryApplyShortFlag(sc, args[idx[0] + 1]))");
		sb.AppendLine("\t\t\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\t\t\tidx[0] += 2;");
		sb.AppendLine("\t\t\t\t\t\tcontinue;");
		sb.AppendLine("\t\t\t\t\t}");
		sb.AppendLine("\t\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t\t}");
		sb.AppendLine("\t\t\t\tbreak;");
		sb.AppendLine("\t\t\t}");
		sb.AppendLine("\t\t\treturn idx[0];");
		sb.AppendLine("\t\t}");
		sb.AppendLine();
	}

	private static void EmitCompletionStringArray(StringBuilder sb, string name, List<string> values)
	{
		sb.AppendLine($"\t\tprivate static readonly string[] {name} = new string[]");
		sb.AppendLine("\t\t{");
		for (var i = 0; i < values.Count; i++)
		{
			sb.Append("\t\t\t\"").Append(Escape(values[i])).Append('"');
			sb.AppendLine(i < values.Count - 1 ? "," : "");
		}

		sb.AppendLine("\t\t};");
	}

	private static void EmitNextSegmentsArray(StringBuilder sb, string name, RegistryNode node)
	{
		var names = new List<string>();
		foreach (var ch in node.Children)
			names.Add(ch.Segment);
		foreach (var cmd in node.Commands.Where(c => !c.IsRootDefault))
			names.Add(cmd.CommandName);
		names.Sort(StringComparer.OrdinalIgnoreCase);
		EmitCompletionStringArray(sb, name, names);
	}

	private static IEnumerable<string> CollectCompletionFlagStrings(ImmutableArray<ParameterModel> members)
	{
		foreach (var p in members)
		{
			if (p.Kind != ParameterKind.Flag && p.Kind != ParameterKind.OptionsInjected)
				continue;
			foreach (var s in ExpandFlagStrings(p))
				yield return s;
		}
	}

	private static IEnumerable<string> ExpandFlagStrings(ParameterModel p)
	{
		yield return "--" + p.CliLongName;
		foreach (var al in p.Aliases)
		{
			if (string.IsNullOrEmpty(al) || string.Equals(al, p.CliLongName, StringComparison.OrdinalIgnoreCase))
				continue;
			yield return al.StartsWith("-", StringComparison.Ordinal) ? al : "--" + al;
		}

		if (p.ShortOpt is char c)
			yield return "-" + c;
		if (p.Special == BoolSpecialKind.NullableBool)
			yield return "--no-" + p.CliLongName;
	}

	private static List<string> MergeCompletionFlags(
		ImmutableArray<ParameterModel> global,
		ImmutableArray<ParameterModel> ns)
	{
		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var list = new List<string>();
		foreach (var s in CollectCompletionFlagStrings(global))
		{
			if (set.Add(s))
				list.Add(s);
		}

		foreach (var s in CollectCompletionFlagStrings(ns))
		{
			if (set.Add(s))
				list.Add(s);
		}

		list.Sort(StringComparer.OrdinalIgnoreCase);
		return list;
	}
}
