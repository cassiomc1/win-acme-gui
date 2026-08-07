using FluentAssertions;
using WinAcmeGui.Infrastructure.Discovery;

namespace WinAcmeGui.Infrastructure.Tests.Discovery;

public sealed class ScheduledTaskCandidateParserTests
{
    [Fact]
    public void Extracts_wacs_path_from_task_command_line()
    {
        var output = "TaskName: \\win-acme renew\nTask To Run: C:\\Tools\\wacs.exe --renew\n";

        ScheduledTaskCandidateParser.Parse(output).Should().ContainSingle().Which.Should().Be(@"C:\Tools\wacs.exe");
    }
}
