using System;
using System.Threading.Tasks;
using Nullean.Argh;

namespace System.Runtime.CompilerServices
{
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	internal sealed class ModuleInitializerAttribute : Attribute
	{
	}
}

namespace System.Diagnostics.CodeAnalysis
{
	[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
	internal sealed class NotNullWhenAttribute : Attribute
	{
		public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

		public bool ReturnValue { get; }
	}
}

namespace Nullean.Argh.Tests.NetstandardPolyfilled
{
	/// <summary>Registers one command and one [AsParameters] DTO, so the generated code uses both attributes.</summary>
	public static class PolyfilledCli
	{
		/// <summary>Runs the command.</summary>
		public static Task<int> RunAsync(string[] args)
		{
			var app = new ArghApp();
			app.Map("greet", PolyfilledHandlers.Greet);
			return app.RunAsync(args);
		}
	}

	/// <summary>Options bound with [AsParameters].</summary>
	public sealed class GreetArgs
	{
		/// <summary>Who to greet.</summary>
		public string Name { get; set; } = "";
	}

	/// <summary>Commands.</summary>
	public static class PolyfilledHandlers
	{
		/// <summary>Greets someone.</summary>
		/// <param name="args">The options.</param>
		public static void Greet([AsParameters] GreetArgs args) => Console.Out.WriteLine("greet:" + args.Name);
	}
}
