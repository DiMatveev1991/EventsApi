using System.ComponentModel.DataAnnotations;

namespace Bookings.Application.Dtos;

/// <summary>Данные запроса на создание бронирования.</summary>
public sealed class CreateBookingRequest
{
    /// <summary>Идентификатор бронируемого события.</summary>
    [Required]
    public Guid EventId { get; init; }

    /// <summary>Количество бронируемых мест.</summary>
    [Range(1, 50)]
    public int Seats { get; init; } = 1;
}
