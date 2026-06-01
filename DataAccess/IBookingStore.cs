using EventsApi.Models;

namespace EventsApi.DataAccess
{
    /// <summary>
    /// Хранилище бронирований. Абстрагирует слой данных от бизнес-логики.
    /// </summary>
    public interface IBookingStore
    {
        /// <summary>Добавляет новую бронь.</summary>
        void Add(Booking booking);

        /// <summary>Возвращает бронь по идентификатору либо null, если не найдена.</summary>
        Booking? GetById(Guid id);

        /// <summary>Возвращает все бронирования (снимок коллекции).</summary>
        IReadOnlyList<Booking> GetAll();

        /// <summary>Возвращает все бронирования в статусе Pending (снимок коллекции).</summary>
        IReadOnlyList<Booking> GetPending();

        /// <summary>
        /// Сохраняет изменения брони (для in-memory — no-op, т. к. объект мутируется
        /// по ссылке; метод оставлен для совместимости с реальными БД-реализациями).
        /// </summary>
        void Update(Booking booking);
    }
}
