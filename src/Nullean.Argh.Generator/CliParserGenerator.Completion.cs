using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Nullean.Argh;

public sealed partial class CliParserGenerator
{
	private static void EmitRootCompleteBlock(CodeWriter w)
	{
		using (w.If("CompletionProtocol.IsCompleteInvocation(args)"))
		{
			using (w.If("!CompletionProtocol.TryParseCompleteInvocation(args, out var __shell, out var __words)"))
			{
				w.Line("Console.Error.WriteLine(\"Error: expected '__complete <bash|zsh|fish|powershell> -- [words...]'\");");
				w.Return("2");
			}

			w.Line("Complete(__shell, __words);");
			w.Return("0");
		}

		w.Line();
	}


	private static void EmitCompletionForApp(CodeWriter w, AppEmitModel app)
	{
		EmitCompletionHierarchical(w, app);
	}


	private static void EmitCompletionHierarchical(CodeWriter w, AppEmitModel app)
	{
		var globalMembers = app.GlobalOptionsModel is { FlattenedMembers.Length: > 0 }
			? app.GlobalOptionsModel.FlattenedMembers
			: ImmutableArray<ParameterModel>.Empty;

		w.Line("private static readonly string[] __c_meta_root = new string[] { \"--help\", \"-h\", \"--version\" };");
		EmitCompletionStringArray(w, "__c_flags_global", CollectCompletionFlagStrings(globalMembers).ToList());
		EmitNextSegmentsArray(w, "__c_next_Root", app.Root);

		foreach ((var node, var path) in EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty))
		{
			var key = CommandNamespacePathKey(path);
			EmitNextSegmentsArray(w, $"__c_next_{key}", node);
			var mergedNs = MergeCompletionFlags(globalMembers, NamespaceOptionMembers(node));
			mergedNs.AddRange(s_helpFlags);
			mergedNs.Sort(StringComparer.OrdinalIgnoreCase);
			EmitCompletionStringArray(w, $"__c_flags_ns_{key}", mergedNs);
		}

		foreach (var cmd in app.AllCommands.Where(c => !c.IsRootDefault))
		{
			var chain = BuildOptionsInjectionChain(app, cmd);
			var list = new List<string>(s_helpFlags);
			list.AddRange(CollectCompletionFlagStrings(globalMembers));
			foreach (var (_, _, _, _, _, flat, _) in chain)
				list.AddRange(CollectCompletionFlagStrings(flat));
			list.AddRange(CollectCompletionFlagStrings(cmd.Parameters));
			var distinct = list.Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
			EmitCompletionStringArray(w, $"__c_flags_cmd_{cmd.RunMethodName}", distinct);
		}

		EmitCompleteHierarchicalBody(w, app, globalMembers);
		EmitFlagValueCompletion(w, app, globalMembers);
	}

	private static readonly string[] s_helpFlags = { "--help", "-h" };

	/// <summary>A namespace's flattened options members, or none.</summary>
	private static ImmutableArray<ParameterModel> NamespaceOptionMembers(RegistryNode node) =>
		node.CommandNamespaceOptionsModel is { FlattenedMembers.Length: > 0 }
			? node.CommandNamespaceOptionsModel.FlattenedMembers
			: ImmutableArray<ParameterModel>.Empty;

	/// <summary>
	/// Emits value tables for flags with a closed set of values (enums, enum collections, <c>[AllowedValues]</c>) and
	/// <c>TryWriteFlagValues</c>, which completes them after <c>--flag </c>, <c>--flag=</c> (one word) or <c>--flag = </c> (bash splits at <c>=</c>).
	/// </summary>
	private static void EmitFlagValueCompletion(CodeWriter w, AppEmitModel app, ImmutableArray<ParameterModel> globalMembers)
	{
		var tables = new Dictionary<string, string>(StringComparer.Ordinal);
		var scopes = new List<(string Scope, List<(List<string> Spellings, string Table)> Flags)>();

		void AddScope(string scope, IEnumerable<ParameterModel> members)
		{
			var flags = new List<(List<string>, string)>();
			var seen = new HashSet<string>(StringComparer.Ordinal);
			foreach (var p in members)
			{
				var values = CompletionValuesFor(p);
				if (values is null)
					continue;
				var spellings = ExpandFlagStrings(p).Where(s => !s.StartsWith("--no-", StringComparison.Ordinal) && seen.Add(s)).ToList();
				if (spellings.Count == 0)
					continue;
				var key = string.Join("\n", values.Select(v => v.CultureType + "|" + v.Literal + "|" + v.Text));
				if (!tables.TryGetValue(key, out var table))
				{
					table = EmitCompletionValueTable(w, $"__c_values_{tables.Count}", values);
					tables.Add(key, table);
				}

				flags.Add((spellings, table));
			}

			if (flags.Count > 0)
				scopes.Add((scope, flags));
		}

		// Innermost members first so they win over a global or namespace flag spelled the same way.
		AddScope("Root", globalMembers);
		foreach ((var node, var path) in EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty))
			AddScope(CommandNamespacePathKey(path), NamespaceOptionMembers(node).Concat(globalMembers));

		foreach (var cmd in app.AllCommands.Where(c => !c.IsRootDefault))
		{
			var members = new List<ParameterModel>(cmd.Parameters);
			foreach (var (_, _, _, _, _, flat, _) in BuildOptionsInjectionChain(app, cmd).Reverse())
				members.AddRange(flat);
			members.AddRange(globalMembers);
			AddScope("cmd:" + cmd.RunMethodName, members);
		}

		using (w.Block("private static bool TryWriteFlagValues(string scope, string[] committed, int start, string partial)"))
		{
			using (w.If("partial.Length > 2 && partial[0] == '-' && partial[1] == '-'"))
			{
				w.Line("var eq = partial.IndexOf('=');");
				w.Return("eq > 2 && WriteFlagValues(scope, partial.Substring(0, eq), partial.Substring(eq + 1), partial.Substring(0, eq + 1))");
			}

			w.If("partial.Length > 0 && partial[0] == '-'", "return false;");
			w.Line("var n = committed.Length;");
			w.If("n - start >= 1 && committed[n - 1].Length > 1 && committed[n - 1][0] == '-'", "return WriteFlagValues(scope, committed[n - 1], partial, \"\");");
			w.If("n - start >= 2 && committed[n - 1] == \"=\" && committed[n - 2].Length > 1 && committed[n - 2][0] == '-'", "return WriteFlagValues(scope, committed[n - 2], partial, \"\");");
			w.Return("false");
		}

		w.Line();

		using (w.Block("private static bool WriteFlagValues(string scope, string flag, string partial, string insertPrefix)"))
		{
			if (scopes.Count == 0)
			{
				w.Line("_ = scope; _ = flag; _ = partial; _ = insertPrefix;");
				w.Return("false");
			}
			else
			{
				using (w.Switch("scope"))
				{
					foreach (var (scope, flags) in scopes)
					{
						using (w.Case(Quote(scope)))
						{
							foreach (var (spellings, table) in flags)
							{
								var conditions = spellings.Select(s => $"ArghNames.IsFlag(flag, {Quote(s)})");
								using (w.If(string.Join(" || ", conditions)))
								{
									w.Line($"CompletionWriter.WriteFiltered({table}, partial, insertPrefix);");
									w.Return("true");
								}
							}

							w.Return("false");
						}
					}

					w.Case("default", "return false;");
				}
			}
		}

		w.Line();
	}

	/// <summary>
	/// A value completion offers: its invariant <paramref name="Text"/>, or, for a numeric constant, the C#
	/// <paramref name="Literal"/> that the generated code formats with the parse culture (<paramref name="CultureType"/> is
	/// its keyword), so the completed text parses back to the same value in the culture users type in.
	/// </summary>
	private readonly record struct CompletionValue(string Text, string CultureType, string Literal);

	/// <summary>The closed set of values a flag accepts, or <see langword="null"/> when its values are open-ended.</summary>
	private static List<CompletionValue>? CompletionValuesFor(ParameterModel p)
	{
		if (p.Kind != ParameterKind.Flag && p.Kind != ParameterKind.OptionsInjected)
			return null;
		if (p.ScalarKind == CliScalarKind.Enum && !p.EnumMemberNames.IsDefaultOrEmpty)
			return p.EnumMemberNames.Select((m, i) => new CompletionValue(ResolveEnumMemberCliName(p.EnumMemberCliNames, i, m), "", "")).ToList();
		if (p.IsCollection && p.ElementScalarKind == CliScalarKind.Enum && !p.ElementEnumMemberNames.IsDefaultOrEmpty)
			return p.ElementEnumMemberNames.Select((m, i) => new CompletionValue(ResolveEnumMemberCliName(p.ElementEnumMemberCliNames, i, m), "", "")).ToList();
		if (!p.Validations.IsDefaultOrEmpty)
		{
			foreach (var c in p.Validations)
			{
				if (c is AllowedValuesConstraint av)
					return av.Values.Select((literal, i) => new CompletionValue(av.DisplayValues[i], av.CultureTypes[i], literal))
						.Where(v => v.Literal != "null")
						.ToList();
			}
		}

		return null;
	}

	/// <summary>
	/// Emits a value table and returns the expression that reads it. A table with a numeric constant is a method that
	/// formats it with the parse culture at completion time; any other table is a static array.
	/// </summary>
	private static string EmitCompletionValueTable(CodeWriter w, string name, List<CompletionValue> values)
	{
		if (values.All(v => v.CultureType.Length == 0))
		{
			EmitCompletionStringArray(w, name, values.Select(v => v.Text).ToList());
			return name;
		}

		using (w.Block($"private static string[] {name}() => new string[]", ";"))
		{
			w.CommaSeparatedLines(values
				.Select(v => v.CultureType.Length > 0 ? $"(({v.CultureType})({v.Literal})).ToString({ParseCulture})" : Quote(v.Text))
				.ToList());
		}

		return name + "()";
	}

	private static void EmitCompleteHierarchicalBody(CodeWriter w, AppEmitModel app, ImmutableArray<ParameterModel> globalMembers)
	{
		using (w.Block("private static void Complete(CompletionShell shell, ReadOnlySpan<string> words)"))
		{
			w.Line("_ = shell;");
			using (w.If("words.Length == 0"))
			{
				w.Line("CompletionWriter.WriteCommands(__c_next_Root, \"\");");
				w.Line("return;");
			}

			// Index and Range need System.Index/System.Range (.NET Core 3.0, .NET Standard 2.1); Slice works everywhere.
			w.PreprocessorIfElse("NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER",
				() =>
				{
					w.Line("var committed = words.Length > 1 ? words[..^1].ToArray() : Array.Empty<string>();");
					w.Line("var partial = words[^1];");
				},
				() =>
				{
					w.Line("var committed = words.Length > 1 ? words.Slice(0, words.Length - 1).ToArray() : Array.Empty<string>();");
					w.Line("var partial = words[words.Length - 1];");
				});
			w.Line("var pos = 0;");
			if (globalMembers.Length > 0)
				w.Line("pos = ConsumeForComplete_Global(committed, pos, committed.Length);");

			using (w.If("pos == committed.Length"))
			{
				w.If("TryWriteFlagValues(\"Root\", committed, 0, partial)", "return;");
				w.If("partial.Length > 0 && partial[0] == '-'", "CompletionWriter.WriteFlags(MergeSpans(__c_flags_global, __c_meta_root), partial);");
				w.Else("CompletionWriter.WriteCommands(__c_next_Root, partial);");
				w.Line("return;");
			}

			w.Line("var current = \"Root\";");
			using (w.While("pos < committed.Length"))
			{
				w.Line("var tok = committed[pos];");
				using (w.If("tok.Length > 0 && tok[0] == '-'"))
				{
					w.Line("TryWriteFlagValues(current, committed, pos, partial);");
					w.Line("return;");
				}

				using (w.If("TryDescendNamespace(ref current, tok)"))
				{
					w.Line("pos++;");
					w.Line("var nsStart = pos;");
					w.Line("pos = ConsumeForNamespace(current, committed, pos, committed.Length);");
					using (w.If("pos == committed.Length"))
					{
						w.If("!TryWriteFlagValues(current, committed, nsStart, partial)", "WriteAtNamespace(current, partial);");
						w.Line("return;");
					}

					w.Line("continue;");
				}

				using (w.If("TryMatchCommand(current, tok, out var runName)"))
				{
					w.Line("pos++;");
					w.If("TryWriteFlagValues(\"cmd:\" + runName, committed, pos, partial)", "return;");
					w.Line("pos = ConsumeCommandTail(runName, committed, pos, committed.Length);");
					w.If("partial.Length > 0 && partial[0] == '-'", "CompletionWriter.WriteFlags(FlagsForCommand(runName), partial);");
					w.Line("return;");
				}

				w.Line("return;");
			}
		}

		w.Line();

		EmitConsumeForCompleteGlobal(w, globalMembers);
		EmitMergeSpansHelper(w);
		EmitFlagsForCommandSwitch(w, app);
		EmitConsumeCommandTailSwitch(w, app);
		EmitNamespaceConsumeAndDescend(w, app);
		EmitTryDescendNamespace(w, app);
		EmitTryMatchCommandMethod(w, app);
		EmitWriteAtNamespace(w, app);
	}

	private static void EmitMergeSpansHelper(CodeWriter w)
	{
		using (w.Block("private static ReadOnlySpan<string> MergeSpans(ReadOnlySpan<string> a, ReadOnlySpan<string> b)"))
		{
			w.Line("if (a.Length == 0) return b;");
			w.Line("if (b.Length == 0) return a;");
			w.Line("var r = new string[a.Length + b.Length];");
			w.Line("a.CopyTo(r.AsSpan(0, a.Length));");
			w.Line("b.CopyTo(r.AsSpan(a.Length));");
			w.Return("r");
		}

		w.Line();
	}

	/// <summary>A method that switches over command run-method names, one <c>case "Run…": return …;</c> line per command.</summary>
	private static void EmitRunMethodSwitch(CodeWriter w, string signature, AppEmitModel app, Func<CommandModel, string> result, string defaultResult)
	{
		using (w.Block(signature))
		{
			using (w.Switch("runMethodName"))
			{
				foreach (var cmd in app.AllCommands.Where(c => !c.IsRootDefault))
					w.Line($"case {Quote(cmd.RunMethodName)}: return {result(cmd)};");

				w.Line($"default: return {defaultResult};");
			}
		}

		w.Line();
	}

	private static void EmitFlagsForCommandSwitch(CodeWriter w, AppEmitModel app) =>
		EmitRunMethodSwitch(w, "private static ReadOnlySpan<string> FlagsForCommand(string runMethodName)", app,
			static cmd => $"__c_flags_cmd_{cmd.RunMethodName}", "default");

	private static void EmitConsumeCommandTailSwitch(CodeWriter w, AppEmitModel app)
	{
		EmitRunMethodSwitch(w, "private static int ConsumeCommandTail(string runMethodName, string[] committed, int pos, int end)", app,
			static cmd => $"ConsumeForComplete_Command_{cmd.RunMethodName}(committed, pos, end)", "pos");
		foreach (var cmd in app.AllCommands.Where(c => !c.IsRootDefault))
			EmitOptionsConsumeForComplete(w, $"ConsumeForComplete_Command_{cmd.RunMethodName}", cmd.Parameters);
	}

	private static void EmitWriteAtNamespace(CodeWriter w, AppEmitModel app)
	{
		var keys = EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty)
			.Select(static n => CommandNamespacePathKey(n.path))
			.ToList();

		// One case per namespace, writing the completions in that namespace's table.
		void WriteForNamespace(string write, string rootTable, string tablePrefix)
		{
			using (w.Switch("current"))
			{
				using (w.Case("\"Root\""))
				{
					w.Line($"CompletionWriter.{write}({rootTable}, partial);");
					w.Line("break;");
				}

				foreach (var key in keys)
				{
					using (w.Case(Quote(key)))
					{
						w.Line($"CompletionWriter.{write}({tablePrefix}{key}, partial);");
						w.Line("break;");
					}
				}

				w.Case("default", "break;");
			}
		}

		using (w.Block("private static void WriteAtNamespace(string current, string partial)"))
		{
			using (w.If("partial.Length > 0 && partial[0] == '-'"))
				WriteForNamespace("WriteFlags", "MergeSpans(__c_flags_global, __c_meta_root)", "__c_flags_ns_");
			using (w.Else())
				WriteForNamespace("WriteCommands", "__c_next_Root", "__c_next_");
		}

		w.Line();
	}

	/// <summary>
	/// A <c>switch (current)</c> over namespace keys: per namespace, a <c>switch</c> over the registered name <c>tok</c>
	/// matches (see <see cref="EmitCommandNameSwitch"/>), with a case per command, or per child namespace, then
	/// <c>return false;</c>. Namespaces with none are left to <c>default</c>.
	/// </summary>
	private static void EmitNamespaceTokenSwitch(CodeWriter w, AppEmitModel app, Action<CommandModel>? command,
		Action<ImmutableArray<string>>? childNamespace)
	{
		using (w.Switch("current"))
		{
			var scopes = new List<(RegistryNode Node, ImmutableArray<string> Path)> { (app.Root, ImmutableArray<string>.Empty) };
			scopes.AddRange(EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty));
			foreach ((var node, var path) in scopes)
			{
				if (command is not null ? !node.Commands.Any(static c => !c.IsRootDefault) : node.Children.Count == 0)
					continue;
				using (w.Case(Quote(CommandNamespacePathKey(path))))
				{
					EmitCommandNameSwitch(w, node, path, "tok", command, childNamespace);
					w.Return("false");
				}
			}

			w.Case("default", "return false;");
		}
	}

	private static void EmitTryDescendNamespace(CodeWriter w, AppEmitModel app)
	{
		using (w.Block("private static bool TryDescendNamespace(ref string current, string tok)"))
		{
			EmitNamespaceTokenSwitch(w, app, command: null, child =>
			{
				w.Line($"current = {Quote(CommandNamespacePathKey(child))};");
				w.Return("true");
			});
		}

		w.Line();
	}

	private static void EmitTryMatchCommandMethod(CodeWriter w, AppEmitModel app)
	{
		using (w.Block("private static bool TryMatchCommand(string current, string tok, out string runMethodName)"))
		{
			w.Line("runMethodName = \"\";");
			EmitNamespaceTokenSwitch(w, app, cmd =>
			{
				w.Line($"runMethodName = {Quote(cmd.RunMethodName)};");
				w.Return("true");
			}, childNamespace: null);
		}

		w.Line();
	}

	private static void EmitNamespaceConsumeAndDescend(CodeWriter w, AppEmitModel app)
	{
		var namespaces = EnumerateCommandNamespaceNodesWithPath(app.Root, ImmutableArray<string>.Empty)
			.Select(static n => (Key: CommandNamespacePathKey(n.path), Members: NamespaceOptionMembers(n.node)))
			.ToList();
		using (w.Block("private static int ConsumeForNamespace(string current, string[] committed, int pos, int end)"))
		{
			using (w.Switch("current"))
			{
				w.Case("\"Root\"", "return pos;");
				foreach ((var key, var members) in namespaces)
					w.Case(Quote(key), members.IsEmpty ? "return pos;" : $"return ConsumeForComplete_Namespace_{key}(committed, pos, end);");
				w.Case("default", "return pos;");
			}
		}

		w.Line();
		foreach ((var key, var members) in namespaces)
		{
			if (!members.IsEmpty)
				EmitOptionsConsumeForComplete(w, $"ConsumeForComplete_Namespace_{key}", members);
		}
	}

	private static void EmitConsumeForCompleteGlobal(CodeWriter w, ImmutableArray<ParameterModel> globalMembers)
	{
		if (globalMembers.IsEmpty)
		{
			w.Line("private static int ConsumeForComplete_Global(string[] committed, int pos, int end) => pos;");
			w.Line();
			return;
		}

		EmitOptionsConsumeForComplete(w, "ConsumeForComplete_Global", globalMembers);
	}

	/// <summary>
	/// <c>int methodName(args, pos, end)</c>: the index after the options at <c>args[pos]</c>, read as the command line reads
	/// them, up to the first that isn't one of <paramref name="members"/>' flags or has no value yet.
	/// </summary>
	private static void EmitOptionsConsumeForComplete(CodeWriter w, string methodName, ImmutableArray<ParameterModel> members)
	{
		var flagSet = FlagSetFieldName(methodName);
		EmitFlagSetField(w, flagSet, members);
		w.Arrow($"private static int {methodName}(string[] args, int pos, int end)",
			$"{ArghTokenizerFq}.ReadOptions(args, pos, {flagSet}, end: end, complete: true).Next");
		w.Line();
	}

	private static void EmitCompletionStringArray(CodeWriter w, string name, List<string> values)
	{
		using (w.Block($"private static readonly string[] {name} = new string[]", ";"))
			w.CommaSeparatedLines(values.Select(Quote).ToList());
	}

	private static void EmitNextSegmentsArray(CodeWriter w, string name, RegistryNode node)
	{
		var names = new List<string>();
		foreach (var ch in node.Children)
			names.Add(ch.Segment);
		foreach (var cmd in node.Commands.Where(c => !c.IsRootDefault))
			names.Add(cmd.CommandName);
		names.Sort(StringComparer.OrdinalIgnoreCase);
		EmitCompletionStringArray(w, name, names);
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
			if (string.IsNullOrEmpty(al) || string.Equals(al, p.CliLongName, StringComparison.Ordinal))
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
		var set = new HashSet<string>(StringComparer.Ordinal);
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
