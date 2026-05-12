# EventsApi

REST API для управления мероприятиями, построенный на ASP.NET Core Web API (.NET 9).

## Быстрый старт

### Требования

- .NET 9 SDK (работает и на .NET 8, достаточно сменить `TargetFramework`)

### Запуск

```bash
git clone <URL репозитория>
cd EventsApi

dotnet build
dotnet run --project EventsApi.csproj
```

После запуска API будет доступен по адресу:

- HTTP: `http://localhost:5134`
- HTTPS: `https://localhost:7201`

### Swagger UI

Откройте в браузере: `http://localhost:5134/swagger`

### Запуск тестов

```bash
dotnet test
```

При необходимости — только тестовый проект:

```bash
dotnet test EventsApi.Tests/EventsApi.Tests.csproj
```

## Структура проекта

```
EventsApi/
├── Controllers/
│   ├── EventsController.cs            # Эндпоинты по событиям + POST /events/{id}/book
│   └── BookingsController.cs          # Эндпоинты по бронированиям
├── BackgroundServices/
│   └── BookingProcessor.cs            # Фоновая обработка Pending-броней
├── DataAccess/
│   ├── IBookingStore.cs
│   └── InMemoryBookingStore.cs        # In-memory хранилище бронирований
├── EventsApi.DTOs/
│   ├── DTOs/CreateEventDto.cs
│   ├── DTOs/UpdateEventDto.cs
│   ├── DTOs/EventDto.cs
│   ├── DTOs/EventQueryParameters.cs
│   ├── DTOs/PaginatedResult.cs
│   ├── DTOs/BookingDto.cs
│   └── DTOs/BookingStatus.cs
├── Exceptions/
│   └── AppException.cs                # NotFoundException, ValidationException
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs # Глобальный обработчик ошибок
├── Models/
│   ├── Event.cs
│   └── Booking.cs
├── Services/
│   ├── IEventService.cs
│   ├── EventService.cs
│   ├── IBookingService.cs
│   └── BookingService.cs
├── EventsApi.Tests/                   # xUnit + FluentAssertions
│   ├── EventServiceCrudTests.cs
│   ├── EventServiceFilteringTests.cs
│   ├── EventServicePaginationTests.cs
│   ├── EventServiceValidationTests.cs
│   ├── BookingEntityTests.cs
│   ├── InMemoryBookingStoreTests.cs
│   ├── BookingServiceTests.cs
│   └── BookingProcessorTests.cs
├── Program.cs
└── EventsApi.csproj
```

## Документация API

### Модель мероприятия

| Поле          | Тип        | Обязательное | Описание                  |
|---------------|------------|:------------:|---------------------------|
| `id`          | `guid`     | —            | Уникальный идентификатор  |
| `title`       | `string`   | да           | Название мероприятия      |
| `description` | `string?`  | нет          | Описание                  |
| `startAt`     | `datetime` | да           | Дата и время начала       |
| `endAt`       | `datetime` | да           | Дата и время окончания    |

### Модель брони

| Поле          | Тип             | Обязательное | Описание                                                |
|---------------|-----------------|:------------:|---------------------------------------------------------|
| `id`          | `guid`          | —            | Уникальный идентификатор брони                          |
| `eventId`     | `guid`          | да           | Идентификатор события                                   |
| `status`      | `BookingStatus` | да           | Текущий статус (`Pending`/`Confirmed`/`Rejected`)       |
| `createdAt`   | `datetime`      | да           | Время создания брони (UTC)                              |
| `processedAt` | `datetime?`     | нет          | Время обработки фоновым сервисом (UTC)                  |

**Статусы (`BookingStatus`):**

| Значение     | Описание                                                |
|--------------|---------------------------------------------------------|
| `Pending`    | Бронь создана, ожидает обработки фоновым сервисом       |
| `Confirmed`  | Бронь подтверждена                                      |
| `Rejected`   | Бронь отклонена (например, событие было удалено)        |

### Эндпоинты — события

#### `GET /events` — список с фильтрацией и пагинацией

**Query-параметры:**

| Параметр   | Тип        | По умолчанию | Описание                                                   |
|------------|------------|:------------:|------------------------------------------------------------|
| `title`    | `string?`  | —            | Частичное совпадение, регистронезависимо                   |
| `from`     | `datetime?`| —            | События, начинающиеся не раньше указанной даты             |
| `to`       | `datetime?`| —            | События, заканчивающиеся не позже указанной даты           |
| `page`     | `int`      | `1`          | Номер страницы (нумерация с 1)                             |
| `pageSize` | `int`      | `10`         | Количество элементов на странице                           |

#### `GET /events/{id}` — мероприятие по ID

- `200 OK` / `404 Not Found`

#### `POST /events` — создание

- `201 Created` / `400 Bad Request`

#### `PUT /events/{id}` — полное обновление

- `200 OK` / `400 Bad Request` / `404 Not Found`

#### `DELETE /events/{id}`

- `204 No Content` / `404 Not Found`

### Эндпоинты — бронирования

#### `POST /events/{id}/book` — создать бронь

Создаёт бронь для указанного события. Реализует паттерн «**быстрый ответ + отложенная обработка**»: эндпоинт мгновенно возвращает созданную бронь в статусе `Pending`, а её обработка выполняется фоновым сервисом.

**Ответ `202 Accepted`:**

```http
HTTP/1.1 202 Accepted
Location: /bookings/3fa85f64-5717-4562-b3fc-2c963f66afa6
Content-Type: application/json

{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventId": "11111111-2222-3333-4444-555555555555",
  "status": 0,
  "createdAt": "2025-09-15T12:00:00Z",
  "processedAt": null
}
```

- `202 Accepted` — бронь принята в обработку, заголовок `Location` указывает на ресурс брони
- `404 Not Found` — событие с указанным `id` не существует

#### `GET /bookings/{id}` — получить текущее состояние брони

- `200 OK` — возвращает актуальную информацию о брони (включая текущий `status` и `processedAt`)
- `404 Not Found` — бронь не найдена

**Пример ответа после обработки:**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventId": "11111111-2222-3333-4444-555555555555",
  "status": 1,
  "createdAt": "2025-09-15T12:00:00Z",
  "processedAt": "2025-09-15T12:00:02Z"
}
```

## Фоновая обработка бронирований

За обработку бронирований отвечает класс `BookingProcessor` — `BackgroundService`, регистрируемый через `AddHostedService`.

**Алгоритм работы:**

1. Каждые `500 мс` сервис опрашивает `IBookingStore` и забирает брони в статусе `Pending`.
2. Для каждой такой брони выполняется `Task.Delay(2 сек)` — имитация обращения к внешней системе.
3. После «обработки» проверяется, существует ли ещё событие, к которому относится бронь:
   - событие найдено → бронь переводится в `Confirmed`;
   - событие удалено → бронь переводится в `Rejected`.
4. Заполняется поле `ProcessedAt` (UTC), бронь сохраняется в хранилище.

Сервис корректно реагирует на отмену (`CancellationToken`): на остановке хоста все запущенные задачи прерываются. Все ошибки логируются (`ILogger<BookingProcessor>`), но не приводят к падению сервиса.

## Пример сценария использования

```bash
# 1. Создаём событие
curl -X POST http://localhost:5134/events \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Митап по C#",
    "description": "Обсуждаем .NET 9",
    "startAt": "2025-09-15T18:00:00",
    "endAt":   "2025-09-15T20:00:00"
  }'
# → 201 Created, в теле объект события с id

# 2. Создаём бронь
curl -i -X POST http://localhost:5134/events/<event-id>/book
# → 202 Accepted
#   Location: /bookings/<booking-id>
#   В теле бронь со status=0 (Pending)

# 3. Сразу проверяем статус
curl http://localhost:5134/bookings/<booking-id>
# → 200 OK, status=0 (Pending)

# 4. Ждём ~3 секунды и повторяем запрос
sleep 3 && curl http://localhost:5134/bookings/<booking-id>
# → 200 OK, status=1 (Confirmed), processedAt заполнено
```

Для сценария Rejected — создайте бронь и сразу удалите событие через `DELETE /events/{id}`; фоновый сервис увидит, что события больше нет, и отметит бронь как `Rejected`.

## Формат ошибок

Все ошибки возвращаются в формате **Problem Details (RFC 7807)** с `Content-Type: application/problem+json`.

**Пример `404 Not Found`:**

```json
{
  "status": 404,
  "title": "Ресурс не найден",
  "detail": "Бронь с ID 3fa85f64-5717-4562-b3fc-2c963f66afa6 не найдена",
  "instance": "/bookings/3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Пример `400 Bad Request` (валидация события):**

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

## Тестирование

- xUnit + FluentAssertions, паттерн AAA.

**Что покрыто:**

- `EventService`: CRUD, фильтрация (`title`, `from`, `to`, комбинации), пагинация, валидация
- **Сущность `Booking`**: `CreatePending`, переходы `Confirm`/`Reject`, защита от повторных переходов
- **`InMemoryBookingStore`**: Add/Get/Update, фильтр Pending, дубликаты, неизвестные Id
- **`BookingService`**: создание для существующего/удалённого/несуществующего события, несколько броней с уникальными Id, чтение по Id, отражение смены статуса (Confirm/Reject)
- **`BookingProcessor`**: Pending → Confirmed, Pending → Rejected при удалённом событии, обработка нескольких броней, корректная отмена через `StopAsync`, отсутствие активности на пустом сторе

## Примечания

- Данные событий и бронирований хранятся в памяти (Singleton) и сбрасываются при перезапуске.
- Все даты бронирований фиксируются в UTC.
- Период опроса фонового сервиса — 500 мс, имитация задержки внешнего вызова — 2 с.
