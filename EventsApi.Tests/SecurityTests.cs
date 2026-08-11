using EventsApi.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void Sha256PasswordHasher_HashesAndVerifiesPassword()
    {
        var sut = new Sha256PasswordHasher();

        var hash = sut.Hash("Password123!");

        hash.Should().HaveLength(64);
        hash.Should().NotContain("Password123!");
        sut.Verify("Password123!", hash).Should().BeTrue();
        sut.Verify("WrongPassword!", hash).Should().BeFalse();
    }
}
