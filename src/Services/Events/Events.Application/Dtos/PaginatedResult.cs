namespace EventsApi.Application.Dtos
{
    /// <summary>
    /// Ответ эндпоинта со страничной выборкой.
    /// </summary>
    /// <typeparam name="T">Тип элементов.</typeparam>
    public class PaginatedResult<T>
    {
        /// <summary>Общее количество элементов, удовлетворяющих фильтру (до пагинации).</summary>
        public int TotalCount { get; set; }

        /// <summary>Номер текущей страницы (от 1).</summary>
        public int Page { get; set; }

        /// <summary>Количество элементов на текущей странице (фактически возвращённых).</summary>
        public int PageSize { get; set; }

        /// <summary>Элементы текущей страницы.</summary>
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

        /// <summary>Создаёт пустой результат для сериализации и привязки модели.</summary>
        public PaginatedResult() { }

        /// <summary>Создаёт заполненный результат страничной выборки.</summary>
        public PaginatedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
        }
    }
}
