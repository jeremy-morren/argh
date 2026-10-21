using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Nullean.Argh;
using Nullean.Argh.Tests.Netstandard;

var fixture = typeof(NetstandardCli).Assembly;

// Which runtime this is, and which builds of the fixture and of Nullean.Argh.Core it loaded, so tests can check that
// each host exercises the intended assets.
if (args.Length == 1 && args[0] == "--framework-info")
{
	Console.Out.WriteLine("runtime: " + RuntimeInformation.FrameworkDescription);
	Console.Out.WriteLine("fixture: " + fixture.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName);
	Console.Out.WriteLine("core: " + typeof(ArghApp).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName);
	return 0;
}

// Lists the polyfill attributes the generator emitted into the fixture assembly, so tests can check the detection.
if (args.Length == 1 && args[0] == "--list-polyfills")
{
	foreach (var name in new[]
	         {
		         "System.Runtime.CompilerServices.ModuleInitializerAttribute",
		         "System.Diagnostics.CodeAnalysis.NotNullWhenAttribute",
	         })
	{
		if (fixture.GetType(name, throwOnError: false) is not null)
			Console.Out.WriteLine(name);
	}

	return 0;
}

return await NetstandardCli.RunAsync(args);
