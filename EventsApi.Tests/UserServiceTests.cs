using EventsApi.Application.Abstractions;
using EventsApi.Application.Dtos;
using EventsApi.Application.Services;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Enums;
using EventsApi.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

public sealed class UserServiceTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly TestPasswordHasher _hasher = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_users, _hasher, new TestTokenService());
    }

    [Fact]
    public async Task RegisterAsync_NormalizesLoginAndHashesPassword()
    {
        await _sut.RegisterAsync(new RegisterUserDto
        {
            Login = "  Dmitry  ",
            Password = "Password123!",
            Role = UserRole.Admin
        });

        var user = await _users.GetByLoginAsync("dmitry");
        user.Should().NotBeNull();
        user!.PasswordHash.Should().Be("hash:Password123!");
        user.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateLoginIgnoringCase_ThrowsBadRequest()
    {
        await _sut.RegisterAsync(new RegisterUserDto
        {
            Login = "dmitry",
            Password = "Password123!"
        });

        var act = async () => await _sut.RegisterAsync(new RegisterUserDto
        {
            Login = "DMITRY",
            Password = "AnotherPassword!"
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        await _sut.RegisterAsync(new RegisterUserDto
        {
            Login = "user",
            Password = "Password123!"
        });

        var result = await _sut.LoginAsync(new LoginDto
        {
            Login = "USER",
            Password = "Password123!"
        });

        result.Token.Should().StartWith("token:");
    }

    [Theory]
    [InlineData("missing", "Password123!")]
    [InlineData("user", "wrong")]
    public async Task LoginAsync_WithInvalidCredentials_ReturnsSameBadRequest(
        string login,
        string password)
    {
        await _sut.RegisterAsync(new RegisterUserDto
        {
            Login = "user",
            Password = "Password123!"
        });

        var act = async () => await _sut.LoginAsync(new LoginDto
        {
            Login = login,
            Password = password
        });

        await act.Should()
            .ThrowAsync<ValidationException>()
            .Where(exception => exception.StatusCode == 400)
            .WithMessage("Неверный логин или пароль");
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly Dictionary<string, User> _users = new();

        public Task<User?> GetByLoginAsync(
            string normalizedLogin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.GetValueOrDefault(normalizedLogin));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            _users.Add(user.Login, user);
            return Task.CompletedTask;
        }
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";
        public bool Verify(string password, string passwordHash) => Hash(password) == passwordHash;
    }

    private sealed class TestTokenService : ITokenService
    {
        public string CreateToken(User user) => $"token:{user.Id}";
    }
}
