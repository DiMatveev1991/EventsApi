using System.ComponentModel.DataAnnotations;

namespace Bookings.Application.Dtos;

public sealed class CreateBookingRequest
{
    [Required]
    public Guid EventId { get; init; }

    [Range(1, 50)]
    public int Seats { get; init; } = 1;
}
