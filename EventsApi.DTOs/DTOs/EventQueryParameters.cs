using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace EventsApi.DTOs
{
    /// <summary>
    /// Параметры запроса для GET /events: фильтрация + пагинация.
    /// Все поля опциональные. Привязываются из query-строки.
    /// </summary>
    public class EventQueryParameters
    {
        /// <summary>Поиск по названию (регистронезависимый, частичное совпадение).</summary>
        [FromQuery(Name = "title")]
        public string? Title { get; set; }

        /// <summary>Только события, начинающиеся не раньше этой даты.</summary>
        [FromQuery(Name = "from")]
        public DateTime? From { get; set; }

        /// <summary>Только события, заканчивающиеся не позже этой даты.</summary>
        [FromQuery(Name = "to")]
        public DateTime? To { get; set; }

        /// <summary>Номер страницы, начинается с 1.</summary>
        [FromQuery(Name = "page")]
        [DefaultValue(1)]
        public int Page { get; set; } = 1;

        /// <summary>Количество элементов на странице.</summary>
        [FromQuery(Name = "pageSize")]
        [DefaultValue(10)]
        public int PageSize { get; set; } = 10;
    }
}