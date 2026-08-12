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

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-hash")]
    [InlineData("100000.not-base64.still-not-base64")]
    [InlineData("abc.c2FsdA==.aGFzaA==")]
    public void Pbkdf2_rejects_malformed_hash(string hash)
    {
        var hasher = new Pbkdf2PasswordHasher();

        hasher.Verify("Password123!", hash).Should().BeFalse();
    }

    [Fact]
    public void Pbkdf2_hash_contains_expected_format_and_iteration_count()
    {
        var hash = new Pbkdf2PasswordHasher().Hash("Password123!");

        var parts = hash.Split('.');
        parts.Should().HaveCount(3);
        parts[0].Should().Be("100000");
        Convert.FromBase64String(parts[1]).Should().HaveCount(16);
        Convert.FromBase64String(parts[2]).Should().HaveCount(32);
    }
}
