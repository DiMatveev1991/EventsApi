using System.Reflection;
using Bookings.Presentation.Controllers;
using EventsApi.Presentation.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Users.Presentation.Controllers;
using Xunit;

namespace EventsApi.Tests;

public sealed class AuthorizationContractTests
{
    [Theory]
    [InlineData(nameof(EventsController.Create))]
    [InlineData(nameof(EventsController.Update))]
    [InlineData(nameof(EventsController.Delete))]
    public void Event_mutations_require_admin_role(string methodName)
    {
        var method = typeof(EventsController).GetMethod(methodName);

        method.Should().NotBeNull();
        method!.GetCustomAttributes<AuthorizeAttribute>()
            .Should().ContainSingle(attribute => attribute.Roles == "Admin");
    }

    [Theory]
    [InlineData(nameof(EventsController.GetAll))]
    [InlineData(nameof(EventsController.GetById))]
    [InlineData(nameof(EventsController.GetTop))]
    public void Event_reads_do_not_require_admin_role(string methodName)
    {
        var method = typeof(EventsController).GetMethod(methodName);

        method.Should().NotBeNull();
        method!.GetCustomAttributes<AuthorizeAttribute>().Should().BeEmpty();
    }

    [Fact]
    public void Booking_endpoints_require_authenticated_user()
    {
        typeof(BookingsController).GetCustomAttributes<AuthorizeAttribute>()
            .Should().ContainSingle(attribute => string.IsNullOrEmpty(attribute.Roles));
    }

    [Fact]
    public void Auth_endpoints_are_anonymous()
    {
        typeof(AuthController).GetCustomAttributes<AllowAnonymousAttribute>()
            .Should().ContainSingle();
    }
}
