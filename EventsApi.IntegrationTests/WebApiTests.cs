using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventsApi.Application.Dtos;
using EventsApi.Domain.Enums;
using EventsApi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventsApi.IntegrationTests;

/// <summary>Сквозные HTTP-тесты авторизации, middleware и контроллеров.</summary>
public sealed class WebApiTests : IntegrationTestBase
{
    private const string Password = "Password123!";
    private static readonly string JwtSecret = string.Concat(Enumerable.Repeat("test-key-", 5));
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public WebApiTests(PostgresDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RegisterAndLogin_ReturnsJwt()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAndLoginAsync(client, "new-user", UserRole.User);

        token.Should().NotBeNullOrWhiteSpace();
        token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public async Task Login_WithUnknownLoginAndWrongPassword_ReturnsSameNotFoundError()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "known-user", UserRole.User);

        var unknownResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            login = "missing-user",
            password = Password
        });
        var wrongPasswordResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            login = "known-user",
            password = "WrongPassword!"
        });

        unknownResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        wrongPasswordResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var unknownProblem = await unknownResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        var wrongProblem = await wrongPasswordResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        unknownProblem!.Detail.Should().Be(wrongProblem!.Detail);
    }

    [Fact]
    public async Task PostEvent_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await PostEventAsync(client, "Unauthorized event");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostEvent_AsUser_ReturnsForbidden()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "regular-user", UserRole.User);

        var response = await PostEventAsync(client, "Forbidden event");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostEvent_WithEmptyTitle_AsAdmin_ReturnsValidationProblem()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "validation-admin", UserRole.Admin);

        var response = await PostEventAsync(client, string.Empty);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "response body: {0}", body);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("Title");
    }

    [Fact]
    public async Task GetEvent_WithUnknownId_ReturnsNotFoundProblem()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/events/{Guid.NewGuid()}");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "response body: {0}", body);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task PostEvent_WithUtcIsoDates_AsAdmin_ReturnsCreated()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "create-admin", UserRole.Admin);

        var response = await PostEventAsync(client, "UTC event");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, "response body: {0}", body);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.Content.ReadFromJsonAsync<EventDto>();
        created!.StartAt.Should().Be(new DateTimeOffset(2030, 12, 1, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Book_StartedEvent_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "past-admin", UserRole.Admin);
        var createResponse = await PostEventAsync(
            client,
            "Past event",
            "2020-01-01T10:00:00Z",
            "2020-01-01T12:00:00Z");
        var ev = await createResponse.Content.ReadFromJsonAsync<EventDto>();
        await AuthenticateAsync(client, "past-user", UserRole.User);

        var response = await client.PostAsync($"/events/{ev!.Id}/book", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancelBooking_OtherUserForbidden_AdminAllowed()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await RegisterAndLoginAsync(client, "cancel-admin", UserRole.Admin);
        SetToken(client, adminToken);
        var createResponse = await PostEventAsync(client, "Cancelable event");
        var ev = await createResponse.Content.ReadFromJsonAsync<EventDto>();

        await AuthenticateAsync(client, "booking-owner", UserRole.User);
        var bookResponse = await client.PostAsync($"/events/{ev!.Id}/book", null);
        var booking = await bookResponse.Content.ReadFromJsonAsync<BookingDto>(JsonOptions);

        await AuthenticateAsync(client, "other-user", UserRole.User);
        var forbiddenResponse = await client.DeleteAsync($"/bookings/{booking!.Id}");

        SetToken(client, adminToken);
        var adminResponse = await client.DeleteAsync($"/bookings/{booking.Id}");

        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        adminResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static Task<HttpResponseMessage> PostEventAsync(
        HttpClient client,
        string title,
        string startAt = "2030-12-01T10:00:00Z",
        string endAt = "2030-12-01T12:00:00Z") =>
        client.PostAsJsonAsync("/events", new
        {
            title,
            startAt,
            endAt,
            totalSeats = 10
        });

    private static async Task AuthenticateAsync(HttpClient client, string login, UserRole role)
    {
        var token = await RegisterAndLoginAsync(client, login, role);
        SetToken(client, token);
    }

    private static async Task<string> RegisterAndLoginAsync(
        HttpClient client,
        string login,
        UserRole role)
    {
        var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            login,
            password = Password,
            role = role.ToString()
        });
        var registerBody = await registerResponse.Content.ReadAsStringAsync();
        registerResponse.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "response body: {0}",
            registerBody);

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            login,
            password = Password
        });
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "response body: {0}", loginBody);
        return (await loginResponse.Content.ReadFromJsonAsync<TokenDto>())!.Token;
    }

    private static void SetToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = Fixture.ConnectionString,
                    ["Jwt:Secret"] = JwtSecret
                });
            });
        });
}
