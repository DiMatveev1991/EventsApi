using System.ComponentModel;

namespace EventsApi.Application.Dtos
{
    /// <summary>
    /// Параметры запроса для GET /events: фильтрация + пагинация.
    /// Все поля опциональные. В Presentation привязываются из query-строки
    /// по именам свойств (регистронезависимо), поэтому слой Application не зависит
    /// от атрибутов привязки ASP.NET Core.
    /// </summary>
    public class EventQueryParameters
    {
        /// <summary>Поиск по названию (регистронезависимый, частичное совпадение).</summary>
        public string? Title { get; set; }

        /// <summary>Только события, начинающиеся не раньше этой даты.</summary>
        public DateTimeOffset? From { get; set; }

        /// <summary>Только события, заканчивающиеся не позже этой даты.</summary>
        public DateTimeOffset? To { get; set; }

        /// <summary>Номер страницы, начинается с 1.</summary>
        [DefaultValue(1)]
        public int Page { get; set; } = 1;

        /// <summary>Количество элементов на странице.</summary>
        [DefaultValue(10)]
        public int PageSize { get; set; } = 10;
    }
}
