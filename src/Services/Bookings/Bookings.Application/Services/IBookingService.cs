using Bookings.Application.Dtos;

namespace Bookings.Application.Services;

/// <summary>Определяет пользовательские сценарии сервиса бронирований.</summary>
public interface IBookingService
{
    /// <summary>Создаёт новое ожидающее подтверждения бронирование.</summary>
    Task<BookingResponse> CreateAsync(
        CreateBookingRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает бронирование после проверки владельца или роли администратора.</summary>
    Task<BookingResponse> GetByIdAsync(
        Guid bookingId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    /// <summary>Отменяет бронирование после проверки прав доступа.</summary>
    Task CancelAsync(
        Guid bookingId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
