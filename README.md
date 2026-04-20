# EventsApi

REST API для управления мероприятиями, построенный на ASP.NET Core Web API (.NET 9).

## Быстрый старт

### Требования

- .NET 9 SDK (работает и на .NET 8, достаточно сменить `TargetFramework`)

### Запуск

```bash
# Клонировать репозиторий
git clone <URL репозитория>
cd EventsApi

# Собрать проект
dotnet build

# Запустить API
dotnet run --project EventsApi.csproj
```

После запуска API будет доступен по адресу:

- HTTP: `http://localhost:5134`
- HTTPS: `https://localhost:7201`

### Swagger UI

Откройте в браузере: `http://localhost:5134/swagger`

### Запуск тестов

```bash
# Из корня репозитория
dotnet test
```

При необходимости можно запустить только тестовый проект:

```bash
dotnet test EventsApi.Tests/EventsApi.Tests.csproj
```

## Структура проекта

```
EventsApi/
├── Controllers/
│   └── EventsController.cs            # Эндпоинты API
├── EventsApi.DTOs/
│   ├── DTOs/CreateEventDto.cs         # DTO создания
│   ├── DTOs/UpdateEventDto.cs         # DTO обновления
│   ├── DTOs/EventDto.cs               # DTO ответа
│   ├── DTOs/EventQueryParameters.cs   # Параметры фильтрации и пагинации
│   └── DTOs/PaginatedResult.cs        # Обёртка для страничных ответов
├── Exceptions/
│   └── AppException.cs                # NotFoundException, ValidationException
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs # Глобальный обработчик ошибок
├── Models/
│   └── Event.cs                       # Доменная модель
├── Services/
│   ├── IEventService.cs
│   └── EventService.cs                # LINQ-фильтрация и пагинация
├── EventsApi.Tests/                   # xUnit + FluentAssertions
│   ├── EventServiceCrudTests.cs
│   ├── EventServiceFilteringTests.cs
│   ├── EventServicePaginationTests.cs
│   └── EventServiceValidationTests.cs
├── Program.cs
└── EventsApi.csproj
```

## Документация API

### Модель мероприятия

| Поле          | Тип        | Обязательное | Описание                  |
|---------------|------------|:------------:|---------------------------|
| `id`          | `guid`     | —            | Уникальный идентификатор  |
| `title`       | `string`   | да           | Название мероприятия      |
| `description` | `string?`  | нет          | Описание (может быть null)|
| `startAt`     | `datetime` | да           | Дата и время начала       |
| `endAt`       | `datetime` | нет          | Дата и время окончания    |

### Эндпоинты

#### `GET /events` — список мероприятий с фильтрацией и пагинацией

**Query-параметры:**

| Параметр   | Тип        | По умолчанию | Описание                                                   |
|------------|------------|:------------:|------------------------------------------------------------|
| `title`    | `string?`  | —            | Частичное совпадение, регистронезависимо                   |
| `from`     | `datetime?`| —            | События, начинающиеся не раньше указанной даты             |
| `to`       | `datetime?`| —            | События, заканчивающиеся не позже указанной даты           |
| `page`     | `int`      | `1`          | Номер страницы (нумерация с 1)                             |
| `pageSize` | `int`      | `10`         | Количество элементов на странице                           |

Все фильтры комбинируются логическим И.

**Пример запроса:**

```
GET /events?title=митап&from=2025-07-01&to=2025-08-31&page=1&pageSize=5
```

**Ответ `200 OK`:**

```json
{
  "totalCount": 2,
  "page": 1,
  "pageSize": 5,
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "title": "C# Митап",
      "description": "Обсуждаем новинки .NET",
      "startAt": "2025-07-15T18:00:00",
      "endAt":   "2025-07-15T20:00:00"
    },
    {
      "id": "4b2e1c88-9f6a-4c12-aab4-1a2b3c4d5e6f",
      "title": "JS Митап",
      "description": null,
      "startAt": "2025-08-20T18:00:00",
      "endAt":   "2025-08-20T20:00:00"
    }
  ]
}
```

#### `GET /events/{id}` — мероприятие по ID

- `200 OK` — найдено
- `404 Not Found` — не найдено (см. формат ошибки ниже)

#### `POST /events` — создание

Тело запроса:

```json
{
  "title": "Митап по C#",
  "description": "Обсуждаем новинки .NET 9",
  "startAt": "2025-09-15T18:00:00",
  "endAt":   "2025-09-15T20:00:00"
}
```

- `201 Created` — создано, в теле созданный объект
- `400 Bad Request` — ошибки валидации

Правила валидации:

- `title` — обязательное, не пустое
- `startAt`, `endAt` — обязательные
- `endAt` должно быть строго позже `startAt`

#### `PUT /events/{id}` — полное обновление

- `200 OK` — обновлено
- `400 Bad Request` — ошибки валидации
- `404 Not Found` — не найдено

#### `DELETE /events/{id}`

- `204 No Content` — удалено
- `404 Not Found` — не найдено

## Формат ошибок

Все ошибки возвращаются в едином формате **Problem Details (RFC 7807)** с `Content-Type: application/problem+json`.

**Пример `400 Bad Request` (валидация):**

```json
{
  "status": 400,
  "title": "Ошибка валидации",
  "detail": "Один или несколько параметров запроса некорректны",
  "instance": "/events",
  "errors": {
    "Title": ["Title не может быть пустым"],
    "EndAt": ["EndAt должен быть позже StartAt"]
  }
}
```

**Пример `404 Not Found`:**

```json
{
  "status": 404,
  "title": "Ресурс не найден",
  "detail": "Мероприятие с ID 3fa85f64-5717-4562-b3fc-2c963f66afa6 не найдено",
  "instance": "/events/3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Пример `500 Internal Server Error`:**

```json
{
  "status": 500,
  "title": "Внутренняя ошибка сервера",
  "detail": "Произошла непредвиденная ошибка. Повторите попытку позже.",
  "instance": "/events"
}
```

В режиме `Development` для 500-ответов дополнительно добавляется поле `trace` с трассировкой стека.

## Тестирование

- Фреймворк — **xUnit**
- Библиотека утверждений — **FluentAssertions**
- Тесты следуют паттерну **Arrange–Act–Assert**

Покрыто:

- Успешные сценарии CRUD (`Create`, `GetAll`, `GetById`, `Update`, `Delete`)
- Фильтрация по `title` (регистронезависимая), `from`, `to`, их комбинации
- Пагинация (граничные страницы, нормализация отрицательных значений, пустые страницы)
- Неуспешные сценарии: несуществующий ID, `EndAt ≤ StartAt`, пустой `Title`, `null` DTO
- Граничные случаи для дат фильтра (инклюзивные границы `from`/`to`)

## Примечания

- Данные хранятся в памяти (singleton) и сбрасываются при перезапуске.
- Все даты принимаются и возвращаются в формате ISO 8601 (`2025-09-15T18:00:00`).
- Логирование ошибок выполняется встроенным `ILogger`: клиентские ошибки (4xx) — `Warning`, серверные (5xx) — `Error`.
