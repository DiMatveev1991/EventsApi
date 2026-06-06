using EventsApi.Models;

namespace EventsApi.DataAccess
{
	/// <summary>
	/// In-memory реализация <see cref="IEventStore"/>.
	/// Потокобезопасна за счёт внутреннего lock — корректно работает
	/// при одновременных запросах из API, сервиса бронирования и фонового сервиса.
	/// </summary>
	public class InMemoryEventStore : IEventStore
	{
		private readonly Dictionary<Guid, Event> _events = new();
		private readonly object _lock = new();

		public void Add(Event ev)
		{
			ArgumentNullException.ThrowIfNull(ev);

			lock (_lock)
			{
				if (_events.ContainsKey(ev.Id))
					throw new InvalidOperationException(
						$"Событие с ID {ev.Id} уже существует.");

				_events[ev.Id] = ev;
			}
		}

		public Event? GetById(Guid id)
		{
			lock (_lock)
			{
				return _events.TryGetValue(id, out var ev) ? ev : null;
			}
		}

		public IReadOnlyList<Event> GetAll()
		{
			lock (_lock)
			{
				return _events.Values.ToList();
			}
		}

		public void Update(Event ev)
		{
			ArgumentNullException.ThrowIfNull(ev);

			lock (_lock)
			{
				if (!_events.ContainsKey(ev.Id))
					throw new InvalidOperationException(
						$"Событие с ID {ev.Id} не найдено в хранилище.");

				_events[ev.Id] = ev;
			}
		}

		public bool Remove(Guid id)
		{
			lock (_lock)
			{
				return _events.Remove(id);
			}
		}
	}
}