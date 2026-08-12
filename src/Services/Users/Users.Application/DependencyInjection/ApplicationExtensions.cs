using Microsoft.Extensions.DependencyInjection;
using Users.Application.Services;

namespace Users.Application.DependencyInjection;

/// <summary>Содержит регистрации прикладного слоя Users.</summary>
public static class ApplicationExtensions
{
    /// <summary>Регистрирует пользовательские сценарии в DI-контейнере.</summary>
    public static IServiceCollection AddUsersApplication(this IServiceCollection services)
    {
        // UserService Scoped, потому что использует scoped-репозиторий и DbContext
        // одного HTTP-запроса; состояние разных запросов не смешивается.
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
