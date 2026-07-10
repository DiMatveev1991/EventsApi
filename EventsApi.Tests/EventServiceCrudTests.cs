using EventsApi.Exceptions;
using EventsApi.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.Tests;

public class EventServiceCrudTests : IDisposable
{
	private readonly ServiceProvider _sp;
	private readonly IEventService _sut;

	public EventServiceCrudTests()
	{
		_sp = TestHost.Build();
		_sut = _sp.GetRequiredService<IEventService>();
	}

	public void Dispose() => _sp.Dispose();

	[Fact]
	public async Task Create_WithValidData_ReturnsEventWithGeneratedId()
	{
		// Arrange
		var dto = TestData.CreateDto(title: "Конференция");

		// Act
		var created = await _sut.CreateAsync(dto);

		// Assert
		created.Should().NotBeNull();
		created.Id.Should().NotBe(Guid.Empty);
		created.Title.Should().Be("Конференция");
		created.Description.Should().Be(dto.Description);
		created.StartAt.Should().Be(dto.StartAt);
		created.EndAt.Should().Be(dto.EndAt);
	}

	[Fact]
	public async Task Create_SetsAvailableSeatsEqualToTotalSeats()
	{
		// Arrange
		var dto = TestData.CreateDto(totalSeats: 7);

		// Act
		var created = await _sut.CreateAsync(dto);

		// Assert
		created.TotalSeats.Should().Be(7);
		created.AvailableSeats.Should().Be(7);
	}

	[Fact]
	public async Task Create_TrimsTitle()
	{
		// Arrange
		var dto = TestData.CreateDto(title: "   Тест   ");

		// Act
		var created = await _sut.CreateAsync(dto);

		// Assert
		created.Title.Should().Be("Тест");
	}

	[Fact]
	public async Task GetAll_WhenEmpty_ReturnsZeroCount()
	{
		// Act
		var result = await _sut.GetAllAsync(TestData.Query());

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(0);
		result.Items.Should().BeEmpty();
		result.Page.Should().Be(1);
		result.PageSize.Should().Be(10);
	}

	[Fact]
	public async Task GetAll_WithoutFilters_ReturnsAllEvents()
	{
		// Arrange
		await _sut.CreateAsync(TestData.CreateDto(title: "Event A"));
		await _sut.CreateAsync(TestData.CreateDto(title: "Event B"));
		await _sut.CreateAsync(TestData.CreateDto(title: "Event C"));

		// Act
		var result = await _sut.GetAllAsync(TestData.Query());

		// Assert
		result.TotalCount.Should().Be(3);
		result.Items.Should().HaveCount(3);
	}

	[Fact]
	public async Task GetById_ExistingId_ReturnsEvent()
	{
		// Arrange
		var created = await _sut.CreateAsync(TestData.CreateDto(title: "Нужное"));

		// Act
		var found = await _sut.GetByIdAsync(created.Id);

		// Assert
		found.Should().NotBeNull();
		found.Id.Should().Be(created.Id);
		found.Title.Should().Be("Нужное");
	}

	[Fact]
	public async Task GetById_NonExistingId_ThrowsNotFoundException()
	{
		// Arrange
		var unknownId = Guid.NewGuid();

		// Act
		var act = async () => await _sut.GetByIdAsync(unknownId);

		// Assert
		await act.Should()
			.ThrowAsync<NotFoundException>()
			.Where(ex => ex.StatusCode == StatusCodes.Status404NotFound)
			.WithMessage($"*{unknownId}*");
	}

	[Fact]
	public async Task Update_ExistingId_ReturnsUpdatedEvent()
	{
		// Arrange
		var created = await _sut.CreateAsync(TestData.CreateDto(title: "Старое"));
		var updateDto = TestData.UpdateDto(title: "Новое", description: "Новое описание");

		// Act
		var updated = await _sut.UpdateAsync(created.Id, updateDto);

		// Assert
		updated.Id.Should().Be(created.Id);
		updated.Title.Should().Be("Новое");
		updated.Description.Should().Be("Новое описание");
		updated.StartAt.Should().Be(updateDto.StartAt);
		updated.EndAt.Should().Be(updateDto.EndAt);

		// и повторное чтение тоже видит новые данные
		(await _sut.GetByIdAsync(created.Id)).Title.Should().Be("Новое");
	}

	[Fact]
	public async Task Update_NonExistingId_ThrowsNotFoundException()
	{
		// Arrange
		var unknownId = Guid.NewGuid();

		// Act
		var act = async () => await _sut.UpdateAsync(unknownId, TestData.UpdateDto());

		// Assert
		await act.Should().ThrowAsync<NotFoundException>();
	}

	[Fact]
	public async Task Delete_ExistingId_RemovesEvent()
	{
		// Arrange
		var created = await _sut.CreateAsync(TestData.CreateDto());

		// Act
		await _sut.DeleteAsync(created.Id);

		// Assert
		var act = async () => await _sut.GetByIdAsync(created.Id);
		await act.Should().ThrowAsync<NotFoundException>();

		(await _sut.GetAllAsync(TestData.Query())).TotalCount.Should().Be(0);
	}

	[Fact]
	public async Task Delete_NonExistingId_ThrowsNotFoundException()
	{
		// Act
		var act = async () => await _sut.DeleteAsync(Guid.NewGuid());

		// Assert
		await act.Should().ThrowAsync<NotFoundException>();
	}
}