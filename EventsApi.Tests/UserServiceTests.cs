using FluentAssertions;
using Users.Application.Abstractions;
using Users.Application.Dtos;
using Users.Application.Services;
using Users.Domain.Entities;
using Users.Domain.Enums;
using Users.Domain.Exceptions;
using Xunit;

namespace EventsApi.Tests;

public sealed class UserServiceTests
{
    private readonly FakeUserRepository _repository = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly UserService _service;

    public UserServiceTests() =>
        _service = new UserService(_repository, _hasher, new FakeTokenService());

    [Fact]
    public async Task Register_normalizes_login_and_hashes_password()
    {
        await _service.RegisterAsync(new RegisterUserRequest
        {
            Login = "  Dmitry  ",
            Password = "Password123!",
            Role = UserRole.Admin
        });

        var user = await _repository.GetByLoginAsync("dmitry");
        user.Should().NotBeNull();
        user!.PasswordHash.Should().Be("hash:Password123!");
        user.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task Register_persists_user_once()
    {
        await _service.RegisterAsync(new RegisterUserRequest
        {
            Login = "user",
            Password = "Password123!"
        });

        _repository.AddCalls.Should().Be(1);
    }

    [Fact]
    public async Task Register_rejects_duplicate_login_ignoring_case_and_spaces()
    {
        await _service.RegisterAsync(new RegisterUserRequest
        {
            Login = "dmitry",
            Password = "Password123!"
        });

        var action = () => _service.RegisterAsync(new RegisterUserRequest
        {
            Login = "  DMITRY ",
            Password = "AnotherPassword!"
        });

        await action.Should().ThrowAsync<ValidationException>();
        _repository.AddCalls.Should().Be(1);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token()
    {
        await RegisterDefaultUser();

        var result = await _service.LoginAsync(new LoginRequest
        {
            Login = " USER ",
            Password = "Password123!"
        });

        result.Token.Should().StartWith("token:");
    }

    [Theory]
    [InlineData("missing", "Password123!")]
    [InlineData("user", "wrong")]
    public async Task Login_with_invalid_credentials_returns_same_error(
        string login,
        string password)
    {
        await RegisterDefaultUser();

        var action = () => _service.LoginAsync(new LoginRequest
        {
            Login = login,
            Password = password
        });

        await action.Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("Неверный логин или пароль");
    }

    [Fact]
    public async Task Login_passes_normalized_login_to_repository()
    {
        await RegisterDefaultUser();

        await _service.LoginAsync(new LoginRequest
        {
            Login = "  UsEr  ",
            Password = "Password123!"
        });

        _repository.LastLookup.Should().Be("user");
    }

    private Task RegisterDefaultUser() => _service.RegisterAsync(new RegisterUserRequest
    {
        Login = "user",
        Password = "Password123!"
    });

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<string, User> _users = new();

        public int AddCalls { get; private set; }
        public string? LastLookup { get; private set; }

        public Task<User?> GetByLoginAsync(
            string normalizedLogin,
            CancellationToken cancellationToken = default)
        {
            LastLookup = normalizedLogin;
            return Task.FromResult(_users.GetValueOrDefault(normalizedLogin));
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            _users.Add(user.Login, user);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";
        public bool Verify(string password, string passwordHash) => Hash(password) == passwordHash;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string CreateToken(User user) => $"token:{user.Id}";
    }
}
