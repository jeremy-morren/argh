using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nullean.Argh.Hosting;

namespace Bugs.Hosted;

internal static class Program
{
	public static async Task Main(string[] args)
	{
		var builder = Host.CreateApplicationBuilder(args);
		builder.Services.AddScoped<Bug25Dep>();
		builder.Services.AddHostedService<Bug79Service>();          // bug79: started, never stopped or disposed
		builder.Services.AddArgh(args, b =>
		{
			b.MapScoped<Bug22Commands>();                      // bug22: never seen by the generator
			b.Map<Bug25Commands>(ServiceLifetime.Scoped);     // bug25: resolved from the root provider
			b.Map<Bug78Commands>();                           // bug78: logging suppressed by "--version" as a value
		});
		await builder.Build().RunAsync();
	}
}
