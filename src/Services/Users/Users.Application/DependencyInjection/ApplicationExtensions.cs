using Microsoft.Extensions.DependencyInjection;
using Users.Application.Services;

namespace Users.Application.DependencyInjection;

public static class ApplicationExtensions
{
    public static IServiceCollection AddUsersApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
