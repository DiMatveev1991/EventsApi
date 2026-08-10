using System.Net;
using System.Net.Http.Json;
using EventsApi.Application.Dtos;
using EventsApi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventsApi.IntegrationTests;

/// <summary>
/// Сквозные HTTP-тесты Presentation: model binding, middleware и контроллеры.
/// </summary>
public sealed class WebApiTests : IntegrationTestBase
{
    public WebApiTests(PostgresDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PostEvent_WithEmptyTitle_ReturnsValidationProblem()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/events", new
        {
            title = "",
            startAt = "2026-12-01T10:00:00Z",
            endAt = "2026-12-01T12:00:00Z",
            totalSeats = 10
        });

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "response body: {0}",
            body);
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
        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "response body: {0}",
            body);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task PostEvent_WithUtcIsoDates_ReturnsCreated()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/events", new
        {
            title = "UTC event",
            startAt = "2026-12-01T10:00:00Z",
            endAt = "2026-12-01T12:00:00Z",
            totalSeats = 10
        });

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "response body: {0}",
            body);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<EventDto>();
        created!.StartAt.Should().Be(new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero));
        created.EndAt.Should().Be(new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = Fixture.ConnectionString
                });
            });
        });
}
