EventsApi
REST API для управления мероприятиями, построенный на ASP.NET Core Web API (.NET 9)
с хранением данных в PostgreSQL через Entity Framework Core.
Быстрый старт
Требования
.NET 9 SDK (обязательно). Проект использует `TargetFramework=net9.0` во всех трёх csproj
(`EventsApi`, `EventsApi.DTOs`, `EventsApi.Tests`). На .NET 8 SDK сборка не пройдёт.
Проверить установленную версию:
```bash
  dotnet --list-sdks
  ```
Скачать .NET 9 SDK можно с официального сайта Microsoft: https://dotnet.microsoft.com/download/dotnet/9.0

PostgreSQL (обязательно для запуска приложения). Данные хранятся в PostgreSQL,
а не в памяти приложения. Тесты используют InMemory-провайдер EF Core и БД не требуют.
Быстрее всего поднять базу через Docker:
```bash
docker compose up -d
```
Пример `docker-compose.yml`:
```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: eventapi
    ports:
      - "5432:5432"
```
Настройка строки подключения
Строка подключения задаётся в `appsettings.json` в секции `ConnectionStrings:DefaultConnection`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=eventapi;Username=postgres;Password=postgres"
  }
}
```
Отредактируйте `Host`, `Port`, `Database`, `Username`, `Password` под своё окружение.
Схема БД (таблицы `events` и `bookings`) создаётся автоматически при первом запуске
приложения методом `EnsureCreated` — миграции не используются. При повторных запусках,
если схема уже существует, `EnsureCreated` ничего не делает.
Запуск
```bash
git clone <URL репозитория>
cd EventsApi

# поднимите PostgreSQL (см. выше) и при необходимости поправьте строку подключения
docker compose up -d

dotnet build
dotnet run --project EventsApi.csproj
```
После запуска API будет доступен по адресу:
HTTP: `http://localhost:5134`
HTTPS: `https://localhost:7201`
Swagger UI
Откройте в браузере: `http://localhost:5134/swagger`
Запуск тестов
```bash
dotnet test
```
Тесты не требуют PostgreSQL: они используют InMemory-провайдер EF Core
(`Microsoft.EntityFrameworkCore.InMemory`), для каждого тестового класса создаётся
уникальная InMemory-база.
При необходимости — только тестовый проект:
```bash
dotnet test EventsApi.Tests/EventsApi.Tests.csproj
```
Структура проекта
```
EventsApi/
├── Controllers/
│   ├── EventsController.cs            # Эндпоинты по событиям + POST /events/{id}/book
│   └── BookingsController.cs          # Эндпоинты по бронированиям
├── BackgroundServices/
│   └── BookingProcessor.cs            # Фоновая обработка Pending-броней (scoped через IServiceScopeFactory)
├── DataAccess/
│   ├── AppDbContext.cs                # DbContext c DbSet<Event> и DbSet<Booking>
│   └── Configurations/
│       ├── EventConfiguration.cs      # Fluent API-маппинг Event - таблица events
│       └── BookingConfiguration.cs    # Fluent API-маппинг Booking - таблица bookings
├── EventsApi.DTOs/
│   ├── DTOs/CreateEventDto.cs
│   ├── DTOs/UpdateEventDto.cs
│   ├── DTOs/EventDto.cs
│   ├── DTOs/EventQueryParameters.cs
│   ├── DTOs/PaginatedResult.cs
│   ├── DTOs/BookingDto.cs
│   └── DTOs/BookingStatus.cs
├── Exceptions/
│   └── AppException.cs                # NotFoundException, ValidationException,
│                                      # NoAvailableSeatsException
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs # Глобальный обработчик ошибок
├── Models/
│   ├── Event.cs                       # + навигация Bookings, TryReserveSeats/ReleaseSeats
│   └── Booking.cs                     # + навигация Event
├── Services/
│   ├── IEventService.cs
│   ├── EventService.cs                # Работает с AppDbContext напрямую (async)
│   ├── IBookingService.cs
│   └── BookingService.cs              # Критическая секция бронирования под SemaphoreSlim
├── EventsApi.Tests/                   # xUnit + FluentAssertions + EF Core InMemory
│   ├── TestHost.cs                    # DI-контейнер с InMemory-провайдером для тестов
│   ├── EventServiceCrudTests.cs
│   ├── EventServiceFilteringTests.cs
│   ├── EventServicePaginationTests.cs
│   ├── EventServiceValidationTests.cs
│   ├── BookingEntityTests.cs
│   ├── BookingServiceTests.cs
│   ├── BookingConcurrencyTests.cs     # Тесты на овербукинг и уникальность Id
│   └── BookingProcessorTests.cs
├── Program.cs
├── appsettings.json                   # ConnectionStrings:DefaultConnection
└── EventsApi.csproj
```
База данных и слой доступа к данным
Данные хранятся в PostgreSQL, взаимодействие — через Entity Framework Core.

`AppDbContext` (папка `DataAccess`) наследуется от `DbContext`, принимает
`DbContextOptions<AppDbContext>` и содержит два `DbSet`: `Events` и `Bookings`.
В `OnModelCreating` вызывается `ApplyConfigurationsFromAssembly`, что автоматически
подключает все конфигурации `IEntityTypeConfiguration<T>` из сборки.

Маппинг сущностей описан через Fluent API (папка `DataAccess/Configurations`):

`EventConfiguration` — таблица `events`, первичный ключ по `Id` с `ValueGeneratedNever()`
(идентификатор генерируется в коде через `Guid.NewGuid()`), ограничения `IsRequired()`
и `HasMaxLength()` для строк, связь «один–ко–многим» с `Booking` через навигационные свойства.

`BookingConfiguration` — таблица `bookings`, первичный ключ по `Id` с `ValueGeneratedNever()`,
`Status` хранится строкой через `HasConversion<string>()`, связь с `Event` через
`HasOne`/`WithMany` и внешний ключ `EventId`.

Сущности `Event` и `Booking` имеют приватный конструктор без параметров — он нужен EF Core
для создания экземпляров через рефлексию при чтении данных из БД, а также навигационные
свойства для связи между собой (`Event.Bookings` и `Booking.Event`).

Схема создаётся при старте приложения (`Program.cs`):
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}
```
> `EnsureCreated` не совместим с миграциями. Для этого учебного проекта он выбран как
> самый простой вариант создания схемы; при переходе на миграции потребуется
> пересоздать БД или использовать `Migrate()`.

Регистрация в DI (`Program.cs`): `AppDbContext` регистрируется через
`AddDbContext(...).UseNpgsql(...)` (жизненный цикл — scoped). Поскольку сервисы зависят
от scoped-контекста, `IEventService` и `IBookingService` тоже зарегистрированы как scoped.
Документация API
Модель мероприятия
Поле	Тип	Обязательное	Описание
`id`	`guid`	—	Уникальный идентификатор
`title`	`string`	да	Название мероприятия
`description`	`string?`	нет	Описание
`startAt`	`datetime`	да	Дата и время начала
`endAt`	`datetime`	да	Дата и время окончания
`totalSeats`	`int`	да	Общее количество мест; при создании должно быть > 0
`availableSeats`	`int`	—	Свободные места; при создании равно `totalSeats`, уменьшается при каждой успешной брони
Модель брони
Поле	Тип	Обязательное	Описание
`id`	`guid`	—	Уникальный идентификатор брони
`eventId`	`guid`	да	Идентификатор события
`status`	`BookingStatus`	да	Текущий статус (`Pending`/`Confirmed`/`Rejected`)
`createdAt`	`datetime`	да	Время создания брони (UTC)
`processedAt`	`datetime?`	нет	Время обработки фоновым сервисом (UTC)
Статусы (`BookingStatus`):
Значение	Описание
`Pending`	Бронь создана, ожидает обработки фоновым сервисом
`Confirmed`	Бронь подтверждена
`Rejected`	Бронь отклонена (например, событие было удалено)
> В JSON-ответах API статусы сериализуются строкой (`"Pending"`, `"Confirmed"`, `"Rejected"`)
> благодаря `JsonStringEnumConverter`. То же видно в Swagger UI.
> В таблице `bookings` статус тоже хранится строкой (`HasConversion<string>()`).
Эндпоинты — события
`GET /events` — список с фильтрацией и пагинацией
Query-параметры:
Параметр	Тип	По умолчанию	Описание
`title`	`string?`	—	Частичное совпадение, регистронезависимо
`from`	`datetime?`	—	События, начинающиеся не раньше указанной даты
`to`	`datetime?`	—	События, заканчивающиеся не позже указанной даты
`page`	`int`	`1`	Номер страницы (нумерация с 1)
`pageSize`	`int`	`10`	Количество элементов на странице
`GET /events/{id}` — мероприятие по ID
`200 OK` / `404 Not Found`
`POST /events` — создание
`201 Created` / `400 Bad Request` (в т. ч. если `totalSeats` отсутствует или не больше нуля)
`PUT /events/{id}` — полное обновление
`200 OK` / `400 Bad Request` / `404 Not Found`
`DELETE /events/{id}`
`204 No Content` / `404 Not Found`
Эндпоинты — бронирования
`POST /events/{id}/book` — создать бронь
Создаёт бронь для указанного события. Реализует паттерн «быстрый ответ + отложенная обработка»: эндпоинт мгновенно возвращает созданную бронь в статусе `Pending`, а её обработка выполняется фоновым сервисом. Каждая успешная бронь атомарно резервирует одно место (`availableSeats` уменьшается на 1).
Ответ `202 Accepted`:
```http
HTTP/1.1 202 Accepted
Location: /bookings/3fa85f64-5717-4562-b3fc-2c963f66afa6
Content-Type: application/json

{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventId": "11111111-2222-3333-4444-555555555555",
  "status": "Pending",
  "createdAt": "2025-09-15T12:00:00Z",
  "processedAt": null
}
```
`202 Accepted` — бронь принята в обработку, заголовок `Location` указывает на ресурс брони
`404 Not Found` — событие с указанным `id` не существует
`409 Conflict` — на событии не осталось свободных мест (`availableSeats = 0`)
`GET /bookings/{id}` — получить текущее состояние брони
`200 OK` — возвращает актуальную информацию о брони (включая текущий `status` и `processedAt`)
`404 Not Found` — бронь не найдена
Пример ответа после обработки:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventId": "11111111-2222-3333-4444-555555555555",
  "status": "Confirmed",
  "createdAt": "2025-09-15T12:00:00Z",
  "processedAt": "2025-09-15T12:00:02Z"
}
```
Фоновая обработка бронирований
За обработку бронирований отвечает класс `BookingProcessor` — `BackgroundService`, регистрируемый через `AddHostedService`.
`BackgroundService` — синглтон, а `AppDbContext` — scoped, поэтому напрямую внедрить контекст нельзя.
Вместо этого сервис получает `IServiceScopeFactory` и на каждую операцию создаёт свой scope со своим `AppDbContext`.
Алгоритм работы:
Каждые `500 мс` (`PollingInterval`) в отдельном scope извлекаются идентификаторы броней в статусе `Pending`, после чего scope закрывается.
Для каждой брони запускается отдельная задача — в своём scope со своим `AppDbContext`; все задачи выполняются параллельно через `Task.WhenAll`. Для каждой брони выполняется `Task.Delay(2 сек)` (`ProcessingDelay`) — имитация обращения к внешней системе; задержки разных броней идут одновременно.
После «обработки» в рамках своего scope проверяется, существует ли ещё событие, к которому относится бронь:
событие найдено → бронь переводится в `Confirmed`;
событие удалено → бронь переводится в `Rejected` (лог `Warning`);
непредвиденная ошибка → бронь переводится в `Rejected`, место возвращается в пул через `ReleaseSeats()`.
Заполняется поле `ProcessedAt` (UTC), изменения сохраняются `SaveChangesAsync()`.
Поскольку каждая задача работает со своим экземпляром контекста, дополнительные примитивы синхронизации в фоновом сервисе не нужны — изоляцию обеспечивает отдельный `AppDbContext` на каждую задачу.
Сервис корректно реагирует на отмену (`CancellationToken`): на остановке хоста все запущенные задачи прерываются, необработанные брони остаются `Pending`. Все ошибки логируются (`ILogger<BookingProcessor>`), но не приводят к падению сервиса.
Синхронизация при бронировании
Критическая проблема бронирования — овербукинг: два потока одновременно читают `availableSeats > 0`,
оба решают создать бронь — в итоге броней больше, чем мест. Поэтому в `BookingService.CreateBookingAsync`
секция «получение события → проверка и резервирование места (`TryReserveSeats`) → создание брони →
`SaveChangesAsync`» выполняется атомарно.

Так как внутри секции есть `await`-вызовы (обращения к БД), обычный `lock` использовать нельзя —
применяется `SemaphoreSlim(1, 1)`. Сервис — scoped (у каждого запроса свой `AppDbContext`),
поэтому семафор объявлен `static`: он синхронизирует критическую секцию между всеми экземплярами
сервиса. Один вызов `SaveChangesAsync()` сохраняет и новую бронь, и изменение `AvailableSeats`
у события — оба объекта отслеживаются одним контекстом. Если мест нет, выбрасывается
`NoAvailableSeatsException` → `409 Conflict`. Операции чтения (`GetBookingByIdAsync`)
синхронизацией не охватываются.
Пример сценария использования
Сценарий 1. Pending → Confirmed
```bash
# 1. Создаём событие
curl -X POST http://localhost:5134/events \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Митап по C#",
    "description": "Обсуждаем .NET 9",
    "startAt": "2025-09-15T18:00:00",
    "endAt":   "2025-09-15T20:00:00",
    "totalSeats": 50
  }'
# → 201 Created, в теле объект события с id

# 2. Создаём бронь
curl -i -X POST http://localhost:5134/events/<event-id>/book
# → 202 Accepted
#   Location: /bookings/<booking-id>
#   В теле бронь со status="Pending"

# 3. Сразу проверяем статус
curl http://localhost:5134/bookings/<booking-id>
# → 200 OK, status="Pending"

# 4. Ждём ~3 секунды и повторяем запрос
sleep 3 && curl http://localhost:5134/bookings/<booking-id>
# → 200 OK, status="Confirmed", processedAt заполнено
```
Данные сохраняются в PostgreSQL и доступны после перезапуска приложения.
Сценарий 2. Овербукинг: 3 места, 4 брони
```bash
# 1. Создаём событие на 3 места
curl -X POST http://localhost:5134/events \
  -H "Content-Type: application/json" \
  -d '{ "title": "Маленький зал", "startAt": "2025-09-15T18:00:00", "endAt": "2025-09-15T20:00:00", "totalSeats": 3 }'
# → 201 Created, totalSeats=3, availableSeats=3

# 2. Создаём три брони — все успешны
curl -i -X POST http://localhost:5134/events/<event-id>/book   # → 202 Accepted
curl -i -X POST http://localhost:5134/events/<event-id>/book   # → 202 Accepted
curl -i -X POST http://localhost:5134/events/<event-id>/book   # → 202 Accepted

# 3. Четвёртая бронь — мест больше нет
curl -i -X POST http://localhost:5134/events/<event-id>/book
# → 409 Conflict, detail: "No available seats for this event"

# 4. Проверяем событие — свободных мест не осталось
curl http://localhost:5134/events/<event-id>
# → 200 OK, "availableSeats": 0
```
Даже если все запросы на бронирование придут одновременно, успешных броней будет
ровно `totalSeats` — атомарность пары «проверка + резервирование» гарантирует `SemaphoreSlim`.
Формат ошибок
Все ошибки возвращаются в формате Problem Details (RFC 7807) с `Content-Type: application/problem+json`.
Пример `404 Not Found`:
```json
{
  "status": 404,
  "title": "Ресурс не найден",
  "detail": "Бронь с ID 3fa85f64-5717-4562-b3fc-2c963f66afa6 не найдена",
  "instance": "/bookings/3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```
Пример `400 Bad Request` (валидация события):
```json
{
  "status": 400,
  "title": "Ошибка валидации",
  "detail": "Один или несколько параметров запроса некорректны",
  "instance": "/events",
  "errors": {
    "Title": ["Title не может быть пустым"],
    "EndAt": ["EndAt должен быть позже StartAt"],
    "TotalSeats": ["TotalSeats должен быть больше нуля"]
  }
}
```
Пример `409 Conflict` (нет свободных мест):
```json
{
  "status": 409,
  "title": "Конфликт",
  "detail": "No available seats for this event",
  "instance": "/events/11111111-2222-3333-4444-555555555555/book"
}
```
Тестирование
xUnit + FluentAssertions, паттерн AAA. Тесты используют InMemory-провайдер EF Core:
через `ServiceCollection` настраивается DI с `AddDbContext(...).UseInMemoryDatabase(...)`,
регистрируются `AppDbContext` и сервисы. Для каждого тестового класса создаётся уникальная
InMemory-база (имя — новый `Guid`), чтобы тесты не влияли друг на друга (см. `TestHost`).
Тесты на конкурентность создают отдельный scope для каждого параллельного запроса.
Что покрыто:
`EventService`: CRUD, фильтрация (`title`, `from`, `to`, комбинации), пагинация, валидация
(включая `TotalSeats`: обязательность и значение > 0; `AvailableSeats = TotalSeats` при создании)
Сущность `Booking`: `CreatePending`, переходы `Confirm`/`Reject`, защита от повторных переходов
`BookingService`: создание для существующего/удалённого/несуществующего события, несколько броней с уникальными Id, чтение по Id, отражение смены статуса (Confirm/Reject); уменьшение `AvailableSeats` после каждой успешной брони; брони до лимита; `NoAvailableSeatsException` при исчерпании мест; восстановление мест после `Reject()` + `ReleaseSeats()` и новая бронь на освободившееся место
Конкурентность (`BookingConcurrencyTests`): защита от овербукинга — событие на 5 мест, 20 конкурентных запросов (`Task.Run` + `Task.WhenAll`, отдельный scope на запрос) → ровно 5 успешных броней, 15 `NoAvailableSeatsException`, `AvailableSeats = 0`; уникальность Id — 10 мест, 10 одновременных запросов → 10 броней с уникальными Id
`BookingProcessor`: Pending → Confirmed, Pending → Rejected при удалённом событии, параллельная обработка нескольких броней (3 брони по 2 с обрабатываются быстрее последовательных 6 с), корректная отмена через `StopAsync`, отсутствие активности на пустой базе
Примечания
Данные событий и бронирований хранятся в PostgreSQL через EF Core и сохраняются между перезапусками приложения.
Схема БД создаётся автоматически при старте через `EnsureCreated`.
Все даты бронирований фиксируются в UTC.
Период опроса фонового сервиса — 500 мс (`PollingInterval`), имитация задержки внешнего вызова — 2 с (`ProcessingDelay`).