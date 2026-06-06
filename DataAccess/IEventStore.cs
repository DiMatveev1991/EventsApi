using EventsApi.Models;

namespace EventsApi.DataAccess
{
	/// <summary>
	/// Хранилище событий. Абстрагирует слой данных от бизнес-логики
	/// и даёт доступ к доменной сущности для операций с местами.
	/// </summary>
	public interface IEventStore
	{
		/// <summary>Добавляет новое событие.</summary>
		void Add(Event ev);

		/// <summary>Возвращает событие по идентификатору либо null, если не найдено.</summary>
		Event? GetById(Guid id);

		/// <summary>Возвращает все события (снимок коллекции).</summary>
		IReadOnlyList<Event> GetAll();

		/// <summary>
		/// Сохраняет изменения события (для in-memory — перезапись по ссылке;
		/// метод оставлен для совместимости с реальными БД-реализациями).
		/// </summary>
		void Update(Event ev);

		/// <summary>Удаляет событие. Возвращает false, если событие не найдено.</summary>
		bool Remove(Guid id);
	}
}