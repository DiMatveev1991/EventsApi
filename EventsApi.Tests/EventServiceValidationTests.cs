using EventsApi.Exceptions;
using EventsApi.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EventsApi.Tests;

public class EventServiceValidationTests
{
	private readonly EventService _sut = new();

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void Create_WithEmptyOrWhitespaceTitle_ThrowsValidationException(string? title)
	{
		// Arrange
		var dto = TestData.CreateDto(title: title!);

		// Act
		var act = () => _sut.Create(dto);

		// Assert
		act.Should()
			.Throw<ValidationException>()
			.Where(ex => ex.StatusCode == StatusCodes.Status400BadRequest)
			.Which.Errors.Should().ContainKey("Title");
	}

	[Fact]
	public void Create_WithEndAtBeforeStartAt_ThrowsValidationException()
	{
		var dto = TestData.CreateDto(
			startAt: new DateTime(2025, 06, 01, 12, 00, 00),
			endAt: new DateTime(2025, 06, 01, 10, 00, 00));

		var act = () => _sut.Create(dto);

		act.Should()
			.Throw<ValidationException>()
			.Which.Errors.Should().ContainKey("EndAt");
	}

	[Fact]
	public void Create_WithEqualStartAndEnd_ThrowsValidationException()
	{
		var when = new DateTime(2025, 06, 01, 10, 00, 00);
		var dto = TestData.CreateDto(startAt: when, endAt: when);

		var act = () => _sut.Create(dto);

		act.Should().Throw<ValidationException>();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void Create_WithNonPositiveTotalSeats_ThrowsValidationException(int totalSeats)
	{
		// Arrange
		var dto = TestData.CreateDto(totalSeats: totalSeats);

		// Act
		var act = () => _sut.Create(dto);

		// Assert
		act.Should()
			.Throw<ValidationException>()
			.Where(ex => ex.StatusCode == StatusCodes.Status400BadRequest)
			.Which.Errors.Should().ContainKey("TotalSeats");
	}

	[Fact]
	public void Create_WithNullTotalSeats_ThrowsValidationException()
	{
		// Arrange
		var dto = TestData.CreateDto(totalSeats: null);

		// Act
		var act = () => _sut.Create(dto);

		// Assert
		act.Should()
			.Throw<ValidationException>()
			.Where(ex => ex.StatusCode == StatusCodes.Status400BadRequest)
			.Which.Errors.Should().ContainKey("TotalSeats");
	}

	[Fact]
	public void Update_WithEndAtBeforeStartAt_ThrowsValidationException()
	{
		// Arrange
		var existing = _sut.Create(TestData.CreateDto());
		var badUpdate = TestData.UpdateDto(
			startAt: new DateTime(2025, 07, 01, 12, 00, 00),
			endAt: new DateTime(2025, 07, 01, 10, 00, 00));

		// Act
		var act = () => _sut.Update(existing.Id, badUpdate);

		// Assert
		act.Should()
			.Throw<ValidationException>()
			.Which.Errors.Should().ContainKey("EndAt");
	}

	[Fact]
	public void Update_WithEmptyTitle_ThrowsValidationException()
	{
		var existing = _sut.Create(TestData.CreateDto());
		var badUpdate = TestData.UpdateDto(title: "");

		var act = () => _sut.Update(existing.Id, badUpdate);

		act.Should()
			.Throw<ValidationException>()
			.Which.Errors.Should().ContainKey("Title");
	}

	[Fact]
	public void Create_WithNullDto_ThrowsArgumentNullException()
	{
		var act = () => _sut.Create(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void Update_WithNullDto_ThrowsArgumentNullException()
	{
		var existing = _sut.Create(TestData.CreateDto());

		var act = () => _sut.Update(existing.Id, null!);

		act.Should().Throw<ArgumentNullException>();
	}
}