using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EventsApi.Tests;

public class EventServiceCrudTests
{
    private readonly EventService _sut = new();

    [Fact]
    public void Create_WithValidData_ReturnsEventWithGeneratedId()
    {
        // Arrange
        var dto = TestData.CreateDto(title: "Конференция");

        // Act
        var created = _sut.Create(dto);

        // Assert
        created.Should().NotBeNull();
        created.Id.Should().NotBe(Guid.Empty);
        created.Title.Should().Be("Конференция");
        created.Description.Should().Be(dto.Description);
        created.StartAt.Should().Be(dto.StartAt);
        created.EndAt.Should().Be(dto.EndAt);
    }

    [Fact]
    public void Create_TrimsTitle()
    {
        // Arrange
        var dto = TestData.CreateDto(title: "   Тест   ");

        // Act
        var created = _sut.Create(dto);

        // Assert
        created.Title.Should().Be("Тест");
    }

    [Fact]
    public void GetAll_WhenEmpty_ReturnsZeroCount()
    {
        // Act
        var result = _sut.GetAll(TestData.Query());

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public void GetAll_WithoutFilters_ReturnsAllEvents()
    {
        // Arrange
        _sut.Create(TestData.CreateDto(title: "Event A"));
        _sut.Create(TestData.CreateDto(title: "Event B"));
        _sut.Create(TestData.CreateDto(title: "Event C"));

        // Act
        var result = _sut.GetAll(TestData.Query());

        // Assert
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public void GetById_ExistingId_ReturnsEvent()
    {
        // Arrange
        var created = _sut.Create(TestData.CreateDto(title: "Нужное"));

        // Act
        var found = _sut.GetById(created.Id);

        // Assert
        found.Should().NotBeNull();
        found.Id.Should().Be(created.Id);
        found.Title.Should().Be("Нужное");
    }

    [Fact]
    public void GetById_NonExistingId_ThrowsNotFoundException()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var act = () => _sut.GetById(unknownId);

        // Assert
        act.Should()
            .Throw<NotFoundException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status404NotFound)
            .WithMessage($"*{unknownId}*");
    }

    [Fact]
    public void Update_ExistingId_ReturnsUpdatedEvent()
    {
        // Arrange
        var created = _sut.Create(TestData.CreateDto(title: "Старое"));
        var updateDto = TestData.UpdateDto(title: "Новое", description: "Новое описание");

        // Act
        var updated = _sut.Update(created.Id, updateDto);

        // Assert
        updated.Id.Should().Be(created.Id);
        updated.Title.Should().Be("Новое");
        updated.Description.Should().Be("Новое описание");
        updated.StartAt.Should().Be(updateDto.StartAt);
        updated.EndAt.Should().Be(updateDto.EndAt);

        // и повторное чтение тоже видит новые данные
        _sut.GetById(created.Id).Title.Should().Be("Новое");
    }

    [Fact]
    public void Update_NonExistingId_ThrowsNotFoundException()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var act = () => _sut.Update(unknownId, TestData.UpdateDto());

        // Assert
        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void Delete_ExistingId_RemovesEvent()
    {
        // Arrange
        var created = _sut.Create(TestData.CreateDto());

        // Act
        _sut.Delete(created.Id);

        // Assert
        var act = () => _sut.GetById(created.Id);
        act.Should().Throw<NotFoundException>();

        _sut.GetAll(TestData.Query()).TotalCount.Should().Be(0);
    }

    [Fact]
    public void Delete_NonExistingId_ThrowsNotFoundException()
    {
        // Act
        var act = () => _sut.Delete(Guid.NewGuid());

        // Assert
        act.Should().Throw<NotFoundException>();
    }
}
