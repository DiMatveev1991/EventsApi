using EventsApi.Application.Abstractions;
using EventsApi.Application.Dtos;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;

namespace EventsApi.Application.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public UserService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var normalizedLogin = NormalizeLogin(dto.Login);
        if (string.IsNullOrWhiteSpace(normalizedLogin) || string.IsNullOrWhiteSpace(dto.Password))
            throw InvalidCredentialsData();

        if (await _userRepository.GetByLoginAsync(normalizedLogin, cancellationToken) is not null)
        {
            throw new ValidationException(
                "Пользователь с таким логином уже существует",
                new Dictionary<string, string[]>
                {
                    [nameof(dto.Login)] = new[] { "Логин уже занят" }
                });
        }

        var user = User.Create(normalizedLogin, _passwordHasher.Hash(dto.Password), dto.Role);
        await _userRepository.AddAsync(user, cancellationToken);
    }

    public async Task<TokenDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var user = await _userRepository.GetByLoginAsync(NormalizeLogin(dto.Login), cancellationToken);
        if (user is null || !_passwordHasher.Verify(dto.Password ?? string.Empty, user.PasswordHash))
            throw new ValidationException("Неверный логин или пароль");

        return new TokenDto { Token = _tokenService.CreateToken(user) };
    }

    private static string NormalizeLogin(string? login) =>
        (login ?? string.Empty).Trim().ToLowerInvariant();

    private static ValidationException InvalidCredentialsData() => new(
        "Логин и пароль обязательны",
        new Dictionary<string, string[]>
        {
            [nameof(LoginDto.Login)] = new[] { "Login не может быть пустым" },
            [nameof(LoginDto.Password)] = new[] { "Password не может быть пустым" }
        });
}
