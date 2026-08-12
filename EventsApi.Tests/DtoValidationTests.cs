using System.ComponentModel.DataAnnotations;
using Bookings.Application.Dtos;
using FluentAssertions;
using Users.Application.Dtos;
using Users.Domain.Enums;
using Xunit;

namespace EventsApi.Tests;

public sealed class DtoValidationTests
{
    [Theory]
    [InlineData("ab", "Password123!", false)]
    [InlineData("valid-user", "short", false)]
    [InlineData("valid-user", "Password123!", true)]
    public void Registration_contract_validates_login_and_password(
        string login,
        string password,
        bool expectedValid)
    {
        var request = new RegisterUserRequest
        {
            Login = login,
            Password = password,
            Role = UserRole.User
        };

        IsValid(request).Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void Booking_contract_validates_seat_range(int seats, bool expectedValid)
    {
        var request = new CreateBookingRequest
        {
            EventId = Guid.NewGuid(),
            Seats = seats
        };

        IsValid(request).Should().Be(expectedValid);
    }

    private static bool IsValid(object value)
    {
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(
            value,
            new ValidationContext(value),
            results,
            validateAllProperties: true);
    }
}
