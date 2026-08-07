using FluentAssertions;
using WinAcmeGui.Infrastructure.Operations;

namespace WinAcmeGui.Infrastructure.Tests.Operations;

public sealed class OutputRedactorTests
{
    [Fact]
    public void Redacts_registered_secret_values_from_output()
    {
        var redactor = new OutputRedactor(["super-secret"]);

        redactor.Redact("token=super-secret").Should().Be("token=••••••••");
    }
}
