using EventsApi.DTOs;
using EventsApi.Models;

namespace EventsApi.DataAccess
{
    /// <summary>
    /// In-memory реализация <see cref="IBookingStore"/>.
    /// Потокобезопасна за счёт внутреннего lock — корректно работает
    /// при одновременных запросах из API и фонового сервиса.
    /// </summary>
    public class InMemoryBookingStore : IBookingStore
    {
        private readonly Dictionary<Guid, Booking> _bookings = new();
        private readonly object _lock = new();

        public void Add(Booking booking)
        {
            ArgumentNullException.ThrowIfNull(booking);

            lock (_lock)
            {
                if (_bookings.ContainsKey(booking.Id))
                    throw new InvalidOperationException(
                        $"Бронь с ID {booking.Id} уже существует.");

                _bookings[booking.Id] = booking;
            }
        }

        public Booking? GetById(Guid id)
        {
            lock (_lock)
            {
                return _bookings.TryGetValue(id, out var booking) ? booking : null;
            }
        }

        public IReadOnlyList<Booking> GetAll()
        {
            lock (_lock)
            {
                return _bookings.Values.ToList();
            }
        }

        public IReadOnlyList<Booking> GetPending()
        {
            lock (_lock)
            {
                return _bookings.Values
                    .Where(b => b.Status == BookingStatus.Pending)
                    .ToList();
            }
        }

        public void Update(Booking booking)
        {
            ArgumentNullException.ThrowIfNull(booking);

            lock (_lock)
            {
                if (!_bookings.ContainsKey(booking.Id))
                    throw new InvalidOperationException(
                        $"Бронь с ID {booking.Id} не найдена в хранилище.");

                // Для in-memory обновление по ссылке уже произошло; перезаписываем
                // запись на случай, если пришла другая инстанция с тем же Id.
                _bookings[booking.Id] = booking;
            }
        }
    }
}
