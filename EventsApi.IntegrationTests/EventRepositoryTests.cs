using EventsApi.Domain.Entities;
using EventsApi.Infrastructure.Repositories;
using EventsApi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace EventsApi.IntegrationTests
{
    /// <summary>
    /// Интеграционные тесты <see cref="EventRepository"/> на реальной PostgreSQL:
    /// проверяют все методы репозитория, включая все вариации фильтров и пагинацию.
    /// </summary>
    public sealed class EventRepositoryTests : IntegrationTestBase
    {
        public EventRepositoryTests(PostgresDatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public async Task AddAsync_persists_event_and_sets_available_seats()
        {
            // Arrange
            var ev = TestData.Event(title: "Concert", totalSeats: 50);

            // Act
            await using (var ctx = CreateContext())
            {
                await new EventRepository(ctx).AddAsync(ev);
            }

            // Assert
            await using (var ctx = CreateContext())
            {
                var stored = await new EventRepository(ctx).GetByIdAsync(ev.Id);
                stored.Should().NotBeNull();
                stored!.Title.Should().Be("Concert");
                stored.TotalSeats.Should().Be(50);
                stored.AvailableSeats.Should().Be(50);
            }
        }

        [Fact]
        public async Task GetByIdAsync_returns_null_when_event_missing()
        {
            // Arrange
            await using var ctx = CreateContext();
            var repository = new EventRepository(ctx);

            // Act
            var result = await repository.GetByIdAsync(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdateAsync_persists_changed_fields()
        {
            // Arrange
            var ev = TestData.Event(title: "Before");
            await using (var ctx = CreateContext())
            {
                await new EventRepository(ctx).AddAsync(ev);
            }

            // Act
            await using (var ctx = CreateContext())
            {
                var repository = new EventRepository(ctx);
                var stored = await repository.GetByIdAsync(ev.Id);
                stored!.Title = "After";
                stored.Description = "Updated";
                stored.StartAt = new DateTimeOffset(2030, 5, 5, 9, 0, 0, TimeSpan.Zero);
                stored.EndAt = stored.StartAt.AddHours(3);
                await repository.UpdateAsync(stored);
            }

            // Assert
            await using (var ctx = CreateContext())
            {
                var stored = await new EventRepository(ctx).GetByIdAsync(ev.Id);
                stored!.Title.Should().Be("After");
                stored.Description.Should().Be("Updated");
                stored.StartAt.Should().Be(new DateTimeOffset(2030, 5, 5, 9, 0, 0, TimeSpan.Zero));
            }
        }

        [Fact]
        public async Task DeleteAsync_removes_event_and_cascades_to_bookings()
        {
            // Arrange
            var ev = TestData.Event(totalSeats: 5);
            var booking = Booking.CreatePending(ev.Id, Guid.Empty);
            await using (var ctx = CreateContext())
            {
                ctx.Events.Add(ev);
                ctx.Bookings.Add(booking);
                await ctx.SaveChangesAsync();
            }

            // Act
            await using (var ctx = CreateContext())
            {
                var repository = new EventRepository(ctx);
                var stored = await repository.GetByIdAsync(ev.Id);
                await repository.DeleteAsync(stored!);
            }

            // Assert — событие удалено, и связанная бронь удалена каскадом (FK ON DELETE CASCADE)
            await using (var ctx = CreateContext())
            {
                (await new EventRepository(ctx).GetByIdAsync(ev.Id)).Should().BeNull();
                (await new BookingRepository(ctx).GetByIdAsync(booking.Id)).Should().BeNull();
            }
        }

        [Fact]
        public async Task GetPagedAsync_without_filters_returns_all_ordered_by_start_then_id()
        {
            // Arrange
            var early = TestData.Event(title: "Early", startAt: new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero));
            var middle = TestData.Event(title: "Middle", startAt: new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero));
            var late = TestData.Event(title: "Late", startAt: new DateTimeOffset(2025, 12, 1, 10, 0, 0, TimeSpan.Zero));
            await SeedAsync(late, early, middle);

            // Act
            await using var ctx = CreateContext();
            var (items, total) = await new EventRepository(ctx).GetPagedAsync(null, null, null, 0, 10);

            // Assert
            total.Should().Be(3);
            items.Select(e => e.Title).Should().ContainInOrder("Early", "Middle", "Late");
        }

        [Fact]
        public async Task GetPagedAsync_filters_by_title_case_insensitive_partial()
        {
            // Arrange
            await SeedAsync(
                TestData.Event(title: "DotNet Conf"),
                TestData.Event(title: "Java Meetup"),
                TestData.Event(title: "dotnet workshop"));

            // Act
            await using var ctx = CreateContext();
            var (items, total) = await new EventRepository(ctx).GetPagedAsync("dotnet", null, null, 0, 10);

            // Assert
            total.Should().Be(2);
            items.Select(e => e.Title).Should().BeEquivalentTo(new[] { "DotNet Conf", "dotnet workshop" });
        }

        [Fact]
        public async Task GetPagedAsync_filters_by_from_date()
        {
            // Arrange
            await SeedAsync(
                TestData.Event(title: "Past", startAt: new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero)),
                TestData.Event(title: "Future", startAt: new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero)));
            var from = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

            // Act
            await using var ctx = CreateContext();
            var (items, total) = await new EventRepository(ctx).GetPagedAsync(null, from, null, 0, 10);

            // Assert
            total.Should().Be(1);
            items.Single().Title.Should().Be("Future");
        }

        [Fact]
        public async Task GetPagedAsync_filters_by_to_date()
        {
            // Arrange
            var early = TestData.Event(
                title: "Early",
                startAt: new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero),
                endAt: new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
            var late = TestData.Event(
                title: "Late",
                startAt: new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
                endAt: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
            await SeedAsync(early, late);
            var to = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

            // Act
            await using var ctx = CreateContext();
            var (items, total) = await new EventRepository(ctx).GetPagedAsync(null, null, to, 0, 10);

            // Assert
            total.Should().Be(1);
            items.Single().Title.Should().Be("Early");
        }

        [Fact]
        public async Task GetPagedAsync_combines_title_and_date_filters()
        {
            // Arrange
            await SeedAsync(
                TestData.Event(title: "DotNet Early", startAt: new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero)),
                TestData.Event(title: "DotNet Late", startAt: new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero)),
                TestData.Event(title: "Java Late", startAt: new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero)));
            var from = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

            // Act
            await using var ctx = CreateContext();
            var (items, total) = await new EventRepository(ctx).GetPagedAsync("dotnet", from, null, 0, 10);

            // Assert
            total.Should().Be(1);
            items.Single().Title.Should().Be("DotNet Late");
        }

        [Fact]
        public async Task GetPagedAsync_applies_pagination_via_skip_and_take()
        {
            // Arrange — 5 событий с возрастающей датой начала
            var events = Enumerable.Range(1, 5)
                .Select(i => TestData.Event(
                    title: $"Event {i}",
                    startAt: new DateTimeOffset(2025, i, 1, 10, 0, 0, TimeSpan.Zero)))
                .ToArray();
            await SeedAsync(events);

            // Act — вторая страница по 2 элемента (skip 2, take 2)
            await using var ctx = CreateContext();
            var (items, total) = await new EventRepository(ctx).GetPagedAsync(null, null, null, 2, 2);

            // Assert
            total.Should().Be(5); // total не зависит от пагинации
            items.Should().HaveCount(2);
            items.Select(e => e.Title).Should().ContainInOrder("Event 3", "Event 4");
        }

        [Fact]
        public async Task GetPagedAsync_returns_empty_page_beyond_last()
        {
            // Arrange
            await SeedAsync(TestData.Event(title: "Only"));

            // Act — запрашиваем страницу за пределами данных
            await using var ctx = CreateContext();
            var (items, total) = await new EventRepository(ctx).GetPagedAsync(null, null, null, 10, 10);

            // Assert
            total.Should().Be(1);
            items.Should().BeEmpty();
        }

        private async Task SeedAsync(params Event[] events)
        {
            await using var ctx = CreateContext();
            ctx.Events.AddRange(events);
            await ctx.SaveChangesAsync();
        }
    }
}
