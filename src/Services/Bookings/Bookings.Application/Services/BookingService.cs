using Bookings.Application.Abstractions;
using Bookings.Application.Dtos;
using Bookings.Domain.Entities;
using Bookings.Domain.Exceptions;

namespace Bookings.Application.Services;

/// <summary>Реализует создание, просмотр и отмену бронирований.</summary>
public sealed class BookingService(IBookingRepository bookings) : IBookingService
{
    /// <summary>Максимальное количество активных бронирований одного пользователя.</summary>
    public const int MaxActiveBookingsPerUser = 10;

    /// <summary>Создаёт бронирование с проверкой пользовательского лимита.</summary>
    public async Task<BookingResponse> CreateAsync(
        CreateBookingRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var activeCount = await bookings.CountActiveByUserIdAsync(userId, cancellationToken);
        if (activeCount >= MaxActiveBookingsPerUser)
            throw new ActiveBookingLimitExceededException(MaxActiveBookingsPerUser);

        var booking = Booking.CreatePending(request.EventId, userId, request.Seats);
        await bookings.AddAsync(booking, cancellationToken);
        return Map(booking);
    }

    /// <summary>Возвращает бронирование владельцу или администратору.</summary>
    public async Task<BookingResponse> GetByIdAsync(
        Guid bookingId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var booking = await FindAsync(bookingId, cancellationToken);
        EnsureOwnerOrAdmin(booking, currentUserId, isAdmin);
        return Map(booking);
    }

    /// <summary>Отменяет бронирование владельца или администратора.</summary>
    public async Task CancelAsync(
        Guid bookingId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var booking = await FindAsync(bookingId, cancellationToken);
        EnsureOwnerOrAdmin(booking, currentUserId, isAdmin);
        booking.Cancel(DateTimeOffset.UtcNow);
        await bookings.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Находит бронирование либо формирует ожидаемую ошибку 404.</summary>
    private async Task<Booking> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await bookings.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Booking {id} was not found.");

    /// <summary>Проверяет, что запрос выполняет владелец бронирования или администратор.</summary>
    private static void EnsureOwnerOrAdmin(Booking booking, Guid currentUserId, bool isAdmin)
    {
        if (!isAdmin && booking.UserId != currentUserId)
            throw new ForbiddenException("You can access only your own bookings.");
    }

    /// <summary>Преобразует доменную сущность бронирования в DTO ответа.</summary>
    internal static BookingResponse Map(Booking booking) => new(
        booking.Id,
        booking.EventId,
        booking.UserId,
        booking.Seats,
        booking.Status,
        booking.CreatedAt,
        booking.ProcessedAt);
}
