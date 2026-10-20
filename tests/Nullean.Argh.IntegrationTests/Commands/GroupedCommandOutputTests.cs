using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Commands;

public class GroupedCommandOutputTests
{
	[Fact]
	public void Storage_list_prints_marker()
	{
		var result = CliHostRunner.Run("storage", "list");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("storage-list");
	}

	[Fact]
	public void Storage_blob_upload_prints_marker()
	{
		var result = CliHostRunner.Run("storage", "blob", "upload");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("blob-upload");
	}

	// Arguments reach the process as UTF-16 on Windows, so routing does not depend on the console code page.
	[Theory]
	[InlineData("café", "marker:unicode-cafe")]
	[InlineData("CAFÉ", "marker:unicode-cafe")]
	[InlineData("日本語", "marker:unicode-nihongo")]
	public void Unicode_command_name_routes(string command, string marker)
	{
		var result = CliHostRunner.Run("unicode", command);
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be(marker);
	}
}
