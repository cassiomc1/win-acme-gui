using FluentAssertions;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.Application.Tests.Certificates;

public sealed class ManageCertificateTests
{
    [Fact]
    public async Task Certificate_operation_runs_the_validated_command()
    {
        var runner = new StubRunner();
        var service = new ManageCertificate(runner, new CertificateDraftValidator(), new WinAcmeCommandFactory());

        var result = await service.CreateAsync(
            @"C:\wacs.exe",
            new CertificateDraft("manual", ["example.com"], "http-01", "rsa", "certificatestore", AcceptTerms: true),
            true,
            null,
            CancellationToken.None
        );

        result.Status.Should().Be(OperationStatus.Succeeded);
        runner.Commands.Should().ContainSingle();
        runner.Commands[0].DisplayText.Should().Contain("--validation selfhosting");
    }

    private sealed class StubRunner : IWinAcmeRunner
    {
        public List<WinAcmeCommand> Commands { get; } = [];

        public Task<OperationResult> RunAsync(WinAcmeCommand command, IProgress<string>? output, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(new OperationResult(OperationStatus.Succeeded, 0, TimeSpan.Zero, [], null));
        }
    }
}
