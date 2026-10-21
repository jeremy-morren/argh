using System;
using System.Collections.Generic;
using System.Text;

namespace Nullean.Argh;

/// <summary>
/// Writes generated C#: it owns indentation, braces and preprocessor directives, so an emitter states only what it
/// writes. Blocks are scopes that close when disposed, so an emitter's nesting mirrors the generated code's, and the
/// compiler rather than hand-counted tabs keeps the braces balanced.
/// </summary>
internal sealed class CodeWriter
{
	private const char IndentUnit = '\t';

	private readonly StringBuilder _builder = new();
	private int _indent;

	/// <summary>An empty line.</summary>
	public void Line() => _builder.AppendLine();

	/// <summary>
	/// Writes <paramref name="text"/> at the current indentation. Text spanning several lines, such as an expression
	/// from <see cref="ItemList"/>, has every line indented, on top of whatever relative indentation it carries.
	/// </summary>
	public void Line(string text)
	{
		var start = 0;
		while (true)
		{
			var newline = text.IndexOf('\n', start);
			var line = newline < 0 ? text.Substring(start) : text.Substring(start, newline - start);
			WriteIndented(line);
			if (newline < 0)
				return;
			start = newline + 1;
		}
	}

	/// <summary>One item per line, each but the last followed by a comma: the body of an initializer or argument list.</summary>
	public void CommaSeparatedLines(IReadOnlyList<string> items)
	{
		for (var i = 0; i < items.Count; i++)
			Line(i < items.Count - 1 ? items[i] + "," : items[i]);
	}

	/// <summary>Writes each of <paramref name="lines"/> with <see cref="Line(string)"/>.</summary>
	public void Lines(IEnumerable<string> lines)
	{
		foreach (var line in lines)
			Line(line);
	}

	/// <summary>A braced block after <paramref name="header"/>: a type, member, <c>try</c> or <c>switch</c>.</summary>
	public IDisposable Block(string header)
	{
		Line(header);
		return Block();
	}

	/// <summary>A bare braced block.</summary>
	public IDisposable Block()
	{
		Line("{");
		_indent++;
		return new Scope(this, () => Line("}"));
	}

	/// <summary>A braced block whose closing brace is followed by <paramref name="closeSuffix"/>, such as <c>;</c> or <c>,</c>.</summary>
	public IDisposable Block(string header, string closeSuffix)
	{
		Line(header);
		Line("{");
		_indent++;
		return new Scope(this, () => Line("}" + closeSuffix));
	}

	/// <summary>One more level of indentation without braces: a switch section's statements, or a continuation line.</summary>
	public IDisposable Indented()
	{
		_indent++;
		return new Scope(this, null);
	}

	/// <summary><c>if (condition)</c> with a braced block.</summary>
	public IDisposable If(string condition) => Block($"if ({condition})");

	/// <summary><c>if (condition)</c>, then <paramref name="statement"/> on the next line one level in.</summary>
	public void If(string condition, string statement) => Embedded($"if ({condition})", statement);

	/// <summary><c>else if (condition)</c> with a braced block.</summary>
	public IDisposable ElseIf(string condition) => Block($"else if ({condition})");

	/// <summary><c>else</c> with a braced block.</summary>
	public IDisposable Else() => Block("else");

	/// <summary><c>else</c>, then <paramref name="statement"/> on the next line one level in.</summary>
	public void Else(string statement) => Embedded("else", statement);

	/// <summary><c>foreach (iteration)</c> with a braced block; <paramref name="iteration"/> is the <c>var x in source</c> part.</summary>
	public IDisposable Foreach(string iteration) => Block($"foreach ({iteration})");

	/// <summary><c>foreach (iteration)</c>, then <paramref name="statement"/> on the next line one level in.</summary>
	public void Foreach(string iteration, string statement) => Embedded($"foreach ({iteration})", statement);

	/// <summary><c>for (header)</c> with a braced block; <paramref name="header"/> is the part between the parentheses.</summary>
	public IDisposable For(string header) => Block($"for ({header})");

	/// <summary><c>while (condition)</c> with a braced block.</summary>
	public IDisposable While(string condition) => Block($"while ({condition})");

	/// <summary><c>switch (expression)</c> with a braced block, for <see cref="Case"/> sections.</summary>
	public IDisposable Switch(string expression) => Block($"switch ({expression})");

	/// <summary>A switch section: <c>case label:</c> (or <c>default:</c>), then its statements one level in.</summary>
	public IDisposable Case(string label)
	{
		Line(label == "default" ? "default:" : $"case {label}:");
		return Indented();
	}

	/// <summary>A switch section whose statements sit in a braced block: <c>case label:</c>, then <c>{</c> … <c>}</c>.</summary>
	public IDisposable CaseBlock(string label)
	{
		Line(label == "default" ? "default:" : $"case {label}:");
		return Block();
	}

	/// <summary>A switch section holding a single statement.</summary>
	public void Case(string label, string statement)
	{
		using (Case(label))
			Line(statement);
	}

	/// <summary><c>using (resource)</c> with a braced block.</summary>
	public IDisposable Using(string resource) => Block($"using ({resource})");

	/// <summary><c>using (resource)</c>, then <paramref name="statement"/> on the next line one level in.</summary>
	public void Using(string resource, string statement) => Embedded($"using ({resource})", statement);

	/// <summary><c>try</c> with a braced block; follow it with <see cref="Catch"/> or <see cref="Finally"/>.</summary>
	public IDisposable Try() => Block("try");

	/// <summary><c>catch (declaration)</c> with a braced block.</summary>
	public IDisposable Catch(string declaration) => Block($"catch ({declaration})");

	/// <summary><c>finally</c> with a braced block.</summary>
	public IDisposable Finally() => Block("finally");

	/// <summary><c>return expression;</c>.</summary>
	public void Return(string expression) => Line($"return {expression};");

	/// <summary>An expression-bodied member: <paramref name="signature"/> <c>=&gt;</c>, then the body on the next line one level in.</summary>
	public void Arrow(string signature, string body) => Embedded($"{signature} =>", body + ";");

	/// <summary>
	/// <c>#if condition</c> … <c>#endif</c> around what <paramref name="body"/> writes. Directives start at column zero,
	/// as the compiler and readers expect; the body keeps the current indentation.
	/// </summary>
	public void PreprocessorIf(string condition, Action body)
	{
		Directive($"#if {condition}");
		body();
		Directive("#endif");
	}

	/// <summary><c>#if condition</c> … <c>#else</c> … <c>#endif</c>: <paramref name="ifBody"/> where the symbol is defined, <paramref name="elseBody"/> elsewhere.</summary>
	public void PreprocessorIfElse(string condition, Action ifBody, Action elseBody)
	{
		Directive($"#if {condition}");
		ifBody();
		Directive("#else");
		elseBody();
		Directive("#endif");
	}

	/// <summary>
	/// <paramref name="open"/>, then one item per line indented a further level, each followed by <paramref name="separator"/>
	/// (the last by <paramref name="lastSeparator"/>), then <paramref name="close"/>. The result carries relative
	/// indentation only, so it works as a whole statement and as an item of an enclosing list alike.
	/// </summary>
	public static string ItemList(string open, IReadOnlyList<string> items, string separator, string lastSeparator, string close)
	{
		var text = new StringBuilder(open);
		for (var i = 0; i < items.Count; i++)
			text.Append('\n').Append(IndentLines(items[i])).Append(i == items.Count - 1 ? lastSeparator : separator);
		if (close.Length > 0)
			text.Append('\n').Append(close);
		return text.ToString();
	}

	/// <summary><c>new T[] { … }</c> with one item per line, each followed by a comma, or <c>Array.Empty&lt;T&gt;()</c> when there are none.</summary>
	public static string NewArray(string elementType, IReadOnlyList<string> items) =>
		items.Count == 0 ? $"Array.Empty<{elementType}>()" : ItemList($"new {elementType}[]\n{{", items, ",", ",", "}");

	/// <summary><c>new T(</c> with one argument per line, then <c>)</c> on a line of its own.</summary>
	public static string New(string type, IReadOnlyList<string> arguments) => ItemList($"new {type}(", arguments, ",", "", ")");

	/// <summary>One level in on every line of <paramref name="text"/>.</summary>
	public static string IndentLines(string text) => IndentUnit + text.Replace("\n", "\n" + IndentUnit);

	public override string ToString() => _builder.ToString();

	private void Embedded(string header, string statement)
	{
		Line(header);
		_indent++;
		Line(statement);
		_indent--;
	}

	private void Directive(string directive) => _builder.AppendLine(directive);

	private void WriteIndented(string line)
	{
		if (line.Length > 0)
			_builder.Append(IndentUnit, _indent);
		_builder.AppendLine(line);
	}

	private sealed class Scope(CodeWriter writer, Action? close) : IDisposable
	{
		public void Dispose()
		{
			writer._indent--;
			close?.Invoke();
		}
	}
}
