using FluentAssertions;
using Users.Infrastructure.Security;
using Xunit;

namespace EventsApi.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void Pbkdf2_hash_is_salted_and_verifiable()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var first = hasher.Hash("Password123!");
        var second = hasher.Hash("Password123!");

        first.Should().NotBe(second);
        hasher.Verify("Password123!", first).Should().BeTrue();
        hasher.Verify("wrong", first).Should().BeFalse();
    }
}
