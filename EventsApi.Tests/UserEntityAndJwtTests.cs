using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Users.Domain.Entities;
using Users.Domain.Enums;
using Users.Infrastructure.Security;
using Xunit;

namespace EventsApi.Tests;

public sealed class UserEntityAndJwtTests
{
    private const string Secret = "test-secret-with-at-least-thirty-two-bytes";

    [Fact]
    public void User_factory_normalizes_login_and_keeps_role()
    {
        var user = User.Create("  ADMIN  ", "hash", UserRole.Admin);

        user.Id.Should().NotBeEmpty();
        user.Login.Should().Be("admin");
        user.PasswordHash.Should().Be("hash");
        user.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public void User_factory_generates_unique_ids()
    {
        var first = User.Create("first", "hash", UserRole.User);
        var second = User.Create("second", "hash", UserRole.User);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Jwt_contains_identity_and_role_claims()
    {
        var user = User.Create("admin", "hash", UserRole.Admin);
        var token = CreateService().CreateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.Should().Contain(claim =>
            (claim.Type == ClaimTypes.NameIdentifier || claim.Type == "nameid") &&
            claim.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(claim =>
            (claim.Type == ClaimTypes.Name || claim.Type == "unique_name") &&
            claim.Value == "admin");
        jwt.Claims.Should().Contain(claim =>
            (claim.Type == ClaimTypes.Role || claim.Type == "role") &&
            claim.Value == "Admin");
    }

    [Fact]
    public void Jwt_contains_shared_issuer_and_audience()
    {
        var token = CreateService().CreateToken(User.Create("user", "hash", UserRole.User));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Issuer.Should().Be("EventsSystem.Tests");
        jwt.Audiences.Should().ContainSingle()
            .Which.Should().Be("EventsSystem.Clients.Tests");
    }

    [Fact]
    public void Jwt_expiration_uses_configured_lifetime()
    {
        var before = DateTime.UtcNow.AddMinutes(14);
        var token = CreateService(15).CreateToken(User.Create("user", "hash", UserRole.User));
        var after = DateTime.UtcNow.AddMinutes(16);

        var expires = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;

        expires.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Jwt_rejects_short_secret()
    {
        var options = Options.Create(new JwtOptions { Secret = "too-short" });
        var service = new JwtTokenService(options);

        var action = () => service.CreateToken(User.Create("user", "hash", UserRole.User));

        action.Should().Throw<InvalidOperationException>();
    }

    private static JwtTokenService CreateService(int expirationMinutes = 60) =>
        new(Options.Create(new JwtOptions
        {
            Secret = Secret,
            Issuer = "EventsSystem.Tests",
            Audience = "EventsSystem.Clients.Tests",
            ExpirationMinutes = expirationMinutes
        }));
}
