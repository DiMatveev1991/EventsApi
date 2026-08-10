EventsApi
REST API для управления мероприятиями, построенный на ASP.NET Core Web API (.NET 9)
с хранением данных в PostgreSQL через Entity Framework Core. Схема базы данных
управляется миграциями EF Core, а слой доступа к данным вынесен в репозитории
и покрыт интеграционными тестами на реальной PostgreSQL через Testcontainers.

## Авторизация и роли (Sprint 8)

API использует JWT Bearer Authentication. Пользователь регистрируется через
`POST /auth/register`, затем получает токен через `POST /auth/login`. Пароли в БД
не хранятся: сохраняется только SHA-256-хеш. Секрет подписи JWT обязательно передаётся
из переменной окружения и отсутствует в `appsettings*.json`.

Роли:

- `User` — просматривает события, создаёт брони и отменяет только свои брони;
- `Admin` — дополнительно создаёт, изменяет и удаляет события, а также может отменить
  любую бронь.

Защищённые эндпоинты ожидают заголовок `Authorization: Bearer <token>`. В Swagger UI
можно нажать **Authorize** и вставить JWT без префикса `Bearer`.

Правила бронирования:

- нельзя бронировать событие, которое уже началось (`400 Bad Request`);
- у пользователя может быть не более 10 активных броней (`Pending` или `Confirmed`),
  превышение возвращает `409 Conflict` с указанием лимита;
- отменённая бронь получает статус `Cancelled`, место возвращается событию;
- попытка отменить чужую бронь без роли `Admin` возвращает `403 Forbidden`.

Решение организовано по принципам **чистой архитектуры (Clean Architecture)** и
разделено на четыре отдельных проекта (сборки) — `EventsApi.Domain`,
`EventsApi.Application`, `EventsApi.Infrastructure`, `EventsApi.Presentation`.
Направление зависимостей всегда «внутрь» и проверяется компилятором через
`<ProjectReference>` (см. раздел [«Архитектура»](#архитектура)).

Быстрый старт
Требования
.NET 9 SDK (обязательно). Все проекты используют `TargetFramework=net9.0`
(`EventsApi.Domain`, `EventsApi.Application`, `EventsApi.Infrastructure`,
`EventsApi.Presentation`, `EventsApi.Tests`, `EventsApi.IntegrationTests`).
Проверить установленную версию:
```bash
  dotnet --list-sdks
  ```
Скачать .NET 9 SDK можно с официального сайта Microsoft: https://dotnet.microsoft.com/download/dotnet/9.0

PostgreSQL (обязательно для запуска приложения). Данные хранятся в PostgreSQL,
а не в памяти приложения. Юнит-тесты (`EventsApi.Tests`) используют InMemory-провайдер
EF Core и БД не требуют; интеграционные тесты (`EventsApi.IntegrationTests`) сами
поднимают PostgreSQL в контейнере (нужен Docker — см. раздел «Интеграционные тесты»).
Быстрее всего поднять базу для приложения через Docker:
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
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: eventapi
    ports:
      - "5432:5432"
```
Перед запуском задайте `POSTGRES_PASSWORD` в локальном окружении.

Настройка строки подключения
Строка подключения задаётся переменной окружения (секреты не хранятся в
`appsettings.json`):
```bash
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=eventapi;Username=postgres;Password=<password>'
export Jwt__Secret='<случайная строка длиной не менее 32 байт>'
```
Для локального профиля `Development` также используйте env-переменную или
`dotnet user-secrets`; credentials в файлах `appsettings*.json` не хранятся.

Схема БД (таблицы `events`, `bookings`, `users` и связи между ними) создаётся и обновляется
**миграциями EF Core**, а не `EnsureCreated()`. Приложение при старте автоматически
применяет все ещё не применённые миграции методом `MigrateWithLegacyBaselineAsync()` (см. раздел
«Миграции базы данных»).
Запуск
```bash
git clone <URL репозитория>
cd EventsApi

# поднимите PostgreSQL (см. выше) и при необходимости поправьте строку подключения
docker compose up -d

dotnet build
dotnet run --project src/EventsApi.Presentation/EventsApi.Presentation.csproj
```
При первом старте приложение применит миграции и создаст схему БД. Ручной запуск
SQL не требуется.
После запуска API будет доступен по адресу:
HTTP: `http://localhost:5134`
HTTPS: `https://localhost:7201`
Swagger UI
Откройте в браузере: `http://localhost:5134/swagger`

Основные auth-запросы:

```http
POST /auth/register
Content-Type: application/json

{"login":"dmitry","password":"Password123!","role":"User"}
```

Успешная регистрация возвращает `204 No Content`. Вход:

```http
POST /auth/login
Content-Type: application/json

{"login":"dmitry","password":"Password123!"}
```

Ответ `200 OK`: `{"token":"<jwt>"}`. Для неверного логина и неверного пароля API
возвращает одинаковый `404 Not Found`, не раскрывая существование пользователя.
Миграции базы данных
Схема БД управляется миграциями EF Core. Миграции и `AppDbContext` находятся в слое
**Infrastructure**. Начальная миграция `InitialCreate`
(папка `src/EventsApi.Infrastructure/Persistence/Migrations`) создаёт таблицы `events`
и `bookings`. Миграция `AddUsersAndBookingOwnership` добавляет `users`, уникальный индекс
по `Login` и внешний ключ `bookings.UserId → users.Id`.

Применение миграций при старте приложения (composition root, `Program.cs` в Presentation):
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateWithLegacyBaselineAsync();
}
```

Если таблицы были созданы старой версией через `EnsureCreated()` и таблица истории
миграций отсутствует, совместимая схема автоматически регистрируется как baseline,
после чего применяются новые миграции.

Для работы с миграциями нужен инструмент `dotnet-ef`:
```bash
dotnet tool install --global dotnet-ef
```

Миграции хранятся в проекте **Infrastructure** (там же `AppDbContext`), а точкой входа
(startup-project) выступает **Presentation** — именно его composition root настраивает
`AppDbContext` через `UseNpgsql(...)`. Поэтому в командах `dotnet ef` указываются оба проекта.

Создать новую миграцию (после изменения модели/конфигураций):
```bash
dotnet ef migrations add <ИмяМиграции> \
  --project src/EventsApi.Infrastructure/EventsApi.Infrastructure.csproj \
  --startup-project src/EventsApi.Presentation/EventsApi.Presentation.csproj \
  --output-dir Persistence/Migrations
```
(`--output-dir` задаётся относительно проекта Infrastructure, поэтому миграции
попадут в `src/EventsApi.Infrastructure/Persistence/Migrations`.)

Применить миграции к базе вручную (кроме автоприменения при старте):
```bash
dotnet ef database update \
  --project src/EventsApi.Infrastructure/EventsApi.Infrastructure.csproj \
  --startup-project src/EventsApi.Presentation/EventsApi.Presentation.csproj
```

Откатить последнюю ещё не применённую миграцию:
```bash
dotnet ef migrations remove \
  --project src/EventsApi.Infrastructure/EventsApi.Infrastructure.csproj \
  --startup-project src/EventsApi.Presentation/EventsApi.Presentation.csproj
```

> Пакет `Microsoft.EntityFrameworkCore.Design` подключён к проекту `EventsApi.Infrastructure` —
> он нужен инструменту `dotnet-ef` для генерации миграций.
Запуск тестов
```bash
dotnet test
```
Решение содержит два тестовых проекта:

`EventsApi.Tests` — юнит-тесты (xUnit + FluentAssertions) на InMemory-провайдере EF Core.
PostgreSQL и Docker не требуются; для каждого тестового класса создаётся уникальная
InMemory-база (см. `TestHost`).

`EventsApi.IntegrationTests` — интеграционные тесты слоя доступа к данным на **реальной
PostgreSQL** через Testcontainers. Требуется запущенный Docker (см. ниже).

Запустить только юнит-тесты (без Docker):
```bash
dotnet test EventsApi.Tests/EventsApi.Tests.csproj
```
Запустить только интеграционные тесты (нужен Docker):
```bash
dotnet test EventsApi.IntegrationTests/EventsApi.IntegrationTests.csproj
```
Интеграционные тесты
Проект `EventsApi.IntegrationTests` проверяет корректность интеграции с PostgreSQL
на реальной базе данных, а не на InMemory-провайдере.

Требуется запущенный Docker: перед тестами Testcontainers автоматически скачивает образ
`postgres:16-alpine`, поднимает контейнер, а по завершении — останавливает и удаляет его.
Порт подключения назначается динамически и берётся из объекта контейнера
(`container.GetConnectionString()`), никаких захардкоженных портов.

Организация тестового окружения:
Один контейнер PostgreSQL на весь набор тестов — через `PostgresDatabaseFixture`
(`IAsyncLifetime`) и `ICollectionFixture<>` (коллекция `postgres-integration`).
Перед каждым тестом база приводится к чистому состоянию: `EnsureDeletedAsync()` +
`MigrateAsync()` (базовый класс `IntegrationTestBase`). Это гарантирует изоляцию
и независимость тестов от порядка запуска, а также проверяет применимость миграций.

Что покрыто:
`MigrationTests` — миграции создают таблицы `events`, `bookings`, `users`, внешние ключи
`bookings → events/users`, индексы по `EventId`, `UserId` и уникальный индекс логина;
сама миграция фиксируется в
`__EFMigrationsHistory` (нет ожидающих применения миграций).
`EventRepositoryTests` — все методы `EventRepository`: добавление, чтение по Id
(в т. ч. `null` для отсутствующего), обновление полей, удаление с каскадным удалением
броней, а также постраничная выборка со всеми вариациями фильтров (`title`
регистронезависимо, `from`, `to`, их комбинация), корректный `TotalCount` и пагинация
(`skip`/`take`, стабильная сортировка, пустая страница за пределами данных).
`BookingRepositoryTests` — все методы `BookingRepository`: добавление брони, чтение
по Id (в т. ч. `null`), переход статуса и его сохранение, выборка Id только
`Pending`-броней; сохранение брони и резервирования места одной транзакцией;
срабатывание ограничения внешнего ключа при ссылке на несуществующее событие.
Архитектура
Решение разделено на четыре сборки со строго направленными «внутрь» зависимостями.
Компилятор не даст нарушить направление: соответствующих `<ProjectReference>` просто нет.

```
            ┌─────────────────────────────────────────────┐
            │              Presentation                    │  веб-проект, composition root
            │  (Controllers, Middleware, Program.cs)       │
            └───────────────┬──────────────┬───────────────┘
                            │              │
              ссылается на  │              │  ссылается на
                            ▼              ▼
            ┌───────────────────────┐   ┌──────────────────────────────┐
            │     Application        │◄──│        Infrastructure        │
            │ (use cases, порты,     │   │ (AppDbContext, репозитории,  │
            │  DTO, фоновый сервис)  │   │  миграции — реализации портов)│
            └───────────┬───────────┘   └───────────────┬──────────────┘
                        │                               │
                        ▼                               ▼
            ┌─────────────────────────────────────────────┐
            │                  Domain                      │  сущности, enum,
            │        (сущности, enum, исключения)          │  доменные исключения
            └─────────────────────────────────────────────┘
```

* **Domain** — сущности (`Event`, `Booking`), перечисление `BookingStatus`, доменные
  исключения (`AppException` и наследники). Не зависит **ни от чего**: ни от других
  проектов, ни от фреймворков (ASP.NET Core, EF Core). HTTP-код в исключениях хранится
  числом, чтобы не тянуть ASP.NET Core в Domain.
* **Application** — сценарии/сервисы (`EventService`, `BookingService`), **интерфейсы
  портов** (`IEventRepository`, `IBookingRepository`) — абстракции доступа к данным,
  DTO и фоновый сервис `BookingProcessor`. Зависит **только от Domain** и от абстракций
  `Microsoft.Extensions.*` (DI/Hosting/Logging). Ссылки на Infrastructure нет.
* **Infrastructure** — реализации портов поверх EF Core: `AppDbContext`, конфигурации
  маппинга, миграции, `EventRepository`, `BookingRepository`. Зависит от Application
  (интерфейсы портов) и Domain (сущности).
* **Presentation** — тонкие контроллеры, middleware-обработчик исключений и
  **composition root** (`Program.cs`), где через DI связываются реализации из
  Infrastructure с интерфейсами из Application. Зависит от Application и Infrastructure.

Регистрация зависимостей каждого слоя вынесена в extension-методы, поэтому `Program.cs`
остаётся компактным:
* `AddApplicationServices()` — `EventsApi.Application.DependencyInjection`;
* `AddInfrastructureServices(IConfiguration)` — `EventsApi.Infrastructure.DependencyInjection`.

Структура проекта
```
EventsApi/
├── src/
│   ├── EventsApi.Domain/                       # Слой Domain (ни от чего не зависит)
│   │   ├── Entities/
│   │   │   ├── Event.cs                         # + навигация Bookings, TryReserveSeats/ReleaseSeats
│   │   │   └── Booking.cs                       # + навигация Event, Confirm/Reject
│   │   ├── Enums/
│   │   │   ├── BookingStatus.cs                 # Pending / Confirmed / Rejected / Cancelled
│   │   │   └── UserRole.cs                      # User / Admin
│   │   └── Exceptions/
│   │       └── AppException.cs                  # NotFoundException, ValidationException, NoAvailableSeatsException
│   │
│   ├── EventsApi.Application/                   # Слой Application (зависит только от Domain)
│   │   ├── Abstractions/                        # Порты (интерфейсы), которые реализует Infrastructure
│   │   │   ├── IEventRepository.cs
│   │   │   └── IBookingRepository.cs
│   │   ├── Dtos/
│   │   │   ├── CreateEventDto.cs / UpdateEventDto.cs / EventWriteDto.cs
│   │   │   ├── EventDto.cs / EventQueryParameters.cs / PaginatedResult.cs
│   │   │   └── BookingDto.cs
│   │   ├── Services/
│   │   │   ├── IEventService.cs / EventService.cs
│   │   │   └── IBookingService.cs / BookingService.cs
│   │   ├── BackgroundServices/
│   │   │   └── BookingProcessor.cs              # Фоновая обработка Pending-броней (порты через IServiceScopeFactory)
│   │   └── DependencyInjection/
│   │       └── ApplicationServiceCollectionExtensions.cs   # AddApplicationServices()
│   │
│   ├── EventsApi.Infrastructure/                # Слой Infrastructure (зависит от Application и Domain)
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs                  # DbContext c DbSet<Event> и DbSet<Booking>
│   │   │   ├── Configurations/                  # Fluent API-маппинг сущностей
│   │   │   │   ├── EventConfiguration.cs
│   │   │   │   └── BookingConfiguration.cs
│   │   │   └── Migrations/                      # Миграции EF Core (InitialCreate + snapshot)
│   │   ├── Repositories/
│   │   │   ├── EventRepository.cs               # Реализация IEventRepository поверх AppDbContext
│   │   │   └── BookingRepository.cs             # Реализация IBookingRepository поверх AppDbContext
│   │   └── DependencyInjection/
│   │       └── InfrastructureServiceCollectionExtensions.cs # AddInfrastructureServices(IConfiguration)
│   │
│   └── EventsApi.Presentation/                  # Слой Presentation (веб-проект, composition root)
│       ├── Controllers/
│       │   ├── EventsController.cs              # Эндпоинты по событиям + POST /events/{id}/book
│       │   └── BookingsController.cs            # Эндпоинты по бронированиям
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs   # Глобальный обработчик ошибок (доменные исключения → HTTP)
│       ├── Program.cs                           # Composition root: AddApplicationServices + AddInfrastructureServices
│       └── appsettings.json                     # Общие настройки без credentials
│
├── EventsApi.Tests/                             # xUnit + FluentAssertions + EF Core InMemory (юнит-тесты)
│   ├── TestHost.cs                              # DI-контейнер с InMemory-провайдером для тестов
│   ├── EventServiceCrudTests.cs
│   ├── EventServiceFilteringTests.cs
│   ├── EventServicePaginationTests.cs
│   ├── EventServiceValidationTests.cs
│   ├── BookingEntityTests.cs
│   ├── BookingServiceTests.cs
│   ├── BookingConcurrencyTests.cs               # Тесты на овербукинг и уникальность Id
│   └── BookingProcessorTests.cs
├── EventsApi.IntegrationTests/                  # xUnit + Testcontainers (реальная PostgreSQL)
│   ├── Infrastructure/
│   │   ├── PostgresDatabaseFixture.cs           # Один контейнер PostgreSQL + сброс базы миграциями
│   │   ├── PostgresCollection.cs                # Коллекция тестов вокруг общего контейнера
│   │   ├── IntegrationTestBase.cs               # Чистое состояние базы перед каждым тестом
│   │   └── TestData.cs                          # Фабрики тестовых сущностей
│   ├── MigrationTests.cs
│   ├── EventRepositoryTests.cs
│   └── BookingRepositoryTests.cs
└── EventsApi.sln
```
База данных и слой доступа к данным
Данные хранятся в PostgreSQL, взаимодействие — через Entity Framework Core.

`AppDbContext` (проект `EventsApi.Infrastructure`, папка `Persistence`) наследуется от
`DbContext`, принимает `DbContextOptions<AppDbContext>` и содержит два `DbSet`: `Events`
и `Bookings`. В `OnModelCreating` вызывается `ApplyConfigurationsFromAssembly`, что
автоматически подключает все конфигурации `IEntityTypeConfiguration<T>` из сборки.

Маппинг сущностей описан через Fluent API (папка `Persistence/Configurations` проекта Infrastructure):

`EventConfiguration` — таблица `events`, первичный ключ по `Id` с `ValueGeneratedNever()`
(идентификатор генерируется в коде через `Guid.NewGuid()`), ограничения `IsRequired()`
и `HasMaxLength()` для строк, связь «один–ко–многим» с `Booking` через навигационные свойства.

`BookingConfiguration` — таблица `bookings`, первичный ключ по `Id` с `ValueGeneratedNever()`,
`Status` хранится строкой через `HasConversion<string>()`, настроены связи с `Event`
и `User` через внешние ключи `EventId` и `UserId`.

Сущности `Event` и `Booking` имеют приватный конструктор без параметров — он нужен EF Core
для создания экземпляров через рефлексию при чтении данных из БД, а также навигационные
свойства для связи между собой (`Event.Bookings` и `Booking.Event`).
Репозитории (порты и адаптеры)
Доступ к данным описан **портами** — интерфейсами `IEventRepository` и `IBookingRepository`
в слое **Application** (папка `Abstractions`). Их реализации (**адаптеры**) —
`EventRepository` и `BookingRepository` — живут в слое **Infrastructure** и инкапсулируют
всю работу с `AppDbContext`. Сервисы и фоновый обработчик обращаются к данным только через
интерфейсы портов и напрямую к контексту не ходят; сами репозитории содержат только логику
доступа к данным, без бизнес-правил.

`IEventRepository` / `EventRepository` — постраничная выборка событий с фильтрами
(`GetPagedAsync`), чтение по Id, добавление, обновление, удаление.

`IBookingRepository` / `BookingRepository` — чтение брони по Id, выборка Id
`Pending`-броней (`GetPendingIdsAsync`), добавление и обновление брони.

Регистрация в DI — в **composition root** (`Program.cs` проекта Presentation) через
extension-методы слоёв:
* `AddInfrastructureServices(builder.Configuration)` регистрирует `AppDbContext` через
  `AddDbContext(...).UseNpgsql(...)` и адаптеры портов (`IEventRepository`, `IBookingRepository`);
* `AddApplicationServices()` регистрирует сервисы (`IEventService`, `IBookingService`)
  и фоновый `BookingProcessor`.

`AppDbContext`, репозитории и сервисы зарегистрированы как **scoped**, поэтому в пределах
одного запроса все они делят один экземпляр `AppDbContext`. Благодаря этому сохранение брони
и уменьшение `AvailableSeats` у события фиксируются одной транзакцией. Application при этом
не знает о конкретных реализациях — их подставляет DI-контейнер в Presentation.

`EventService` и `BookingService` реализуют бизнес-логику (валидация, нормализация
пагинации, критическая секция бронирования) и получают данные исключительно через
репозитории. `BookingProcessor` (`BackgroundService` — синглтон) получает репозитории
через `IServiceScopeFactory`: на каждую операцию создаётся свой scope со своими
scoped-репозиториями и своим `AppDbContext`.
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
`userId`	`guid`	да	Идентификатор владельца брони
`status`	`BookingStatus`	да	Текущий статус (`Pending`/`Confirmed`/`Rejected`/`Cancelled`)
`createdAt`	`datetime`	да	Время создания брони (UTC)
`processedAt`	`datetime?`	нет	Время обработки фоновым сервисом (UTC)
Статусы (`BookingStatus`):
Значение	Описание
`Pending`	Бронь создана, ожидает обработки фоновым сервисом
`Confirmed`	Бронь подтверждена
`Rejected`	Бронь отклонена (например, событие было удалено)
`Cancelled`	Бронь отменена пользователем или администратором
> В JSON-ответах API статусы сериализуются строкой (`"Pending"`, `"Confirmed"`, `"Rejected"`, `"Cancelled"`)
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
Только `Admin`: `201 Created` / `400 Bad Request` / `401 Unauthorized` / `403 Forbidden`.
`PUT /events/{id}` — полное обновление
Только `Admin`: `200 OK` / `400 Bad Request` / `404 Not Found`.
`DELETE /events/{id}`
Только `Admin`: `204 No Content` / `404 Not Found`.
Эндпоинты — бронирования
`POST /events/{id}/book` — создать бронь
Создаёт бронь для указанного события. Реализует паттерн «быстрый ответ + отложенная обработка»: эндпоинт мгновенно возвращает созданную бронь в статусе `Pending`, а её обработка выполняется фоновым сервисом. Каждая успешная бронь атомарно резервирует одно место (`availableSeats` уменьшается на 1).
Требуется JWT; `UserId` берётся из claims токена, а не из тела запроса.
Ответ `202 Accepted`:
```http
HTTP/1.1 202 Accepted
Location: /bookings/3fa85f64-5717-4562-b3fc-2c963f66afa6
Content-Type: application/json

{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventId": "11111111-2222-3333-4444-555555555555",
  "userId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
  "status": "Pending",
  "createdAt": "2025-09-15T12:00:00Z",
  "processedAt": null
}
```
`202 Accepted` — бронь принята в обработку, заголовок `Location` указывает на ресурс брони
`404 Not Found` — событие с указанным `id` не существует
`400 Bad Request` — событие уже началось
`409 Conflict` — нет мест или достигнут лимит 10 активных броней
`GET /bookings/{id}` — получить текущее состояние брони
Требуется JWT.
`200 OK` — возвращает актуальную информацию о брони (включая текущий `status` и `processedAt`)
`404 Not Found` — бронь не найдена
`DELETE /bookings/{id}` — отменить бронь. Пользователь отменяет только свою бронь,
администратор — любую. Успех: `204 No Content`; чужая бронь: `403 Forbidden`.
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
`BackgroundService` — синглтон, а репозитории и стоящий за ними `AppDbContext` — scoped, поэтому напрямую внедрить их нельзя.
Вместо этого сервис получает `IServiceScopeFactory` и на каждую операцию создаёт свой scope со своими репозиториями.
Алгоритм работы:
Каждые `500 мс` (`PollingInterval`) в отдельном scope извлекаются идентификаторы броней в статусе `Pending` (`IBookingRepository.GetPendingIdsAsync`), после чего scope закрывается.
Для каждой брони запускается отдельная задача — в своём scope со своими репозиториями; все задачи выполняются параллельно через `Task.WhenAll`. Для каждой брони выполняется `Task.Delay(2 сек)` (`ProcessingDelay`) — имитация обращения к внешней системе; задержки разных броней идут одновременно.
После «обработки» в рамках своего scope проверяется, существует ли ещё событие, к которому относится бронь:
событие найдено → бронь переводится в `Confirmed`;
событие удалено → бронь переводится в `Rejected` (лог `Warning`);
непредвиденная ошибка → бронь переводится в `Rejected`, место возвращается в пул через `ReleaseSeats()`.
Заполняется поле `ProcessedAt` (UTC), изменения сохраняются через `IBookingRepository.UpdateAsync`.
Поскольку каждая задача работает со своим экземпляром контекста, дополнительные примитивы синхронизации в фоновом сервисе не нужны — изоляцию обеспечивает отдельный `AppDbContext` на каждую задачу.
Сервис корректно реагирует на отмену (`CancellationToken`): на остановке хоста все запущенные задачи прерываются, необработанные брони остаются `Pending`. Все ошибки логируются (`ILogger<BookingProcessor>`), но не приводят к падению сервиса.
Синхронизация при бронировании
Критическая проблема бронирования — овербукинг: два потока одновременно читают `availableSeats > 0`,
оба решают создать бронь — в итоге броней больше, чем мест. Поэтому в `BookingService.CreateBookingAsync`
секция «получение события → проверка и резервирование места (`TryReserveSeats`) → создание брони →
сохранение» выполняется атомарно.

Так как внутри секции есть `await`-вызовы (обращения к БД), обычный `lock` использовать нельзя.
Singleton `EventBookingLock` хранит отдельный `SemaphoreSlim(1, 1)` для каждого `EventId`:
запросы к одному событию сериализуются, а бронирования разных событий не мешают друг другу.
Неиспользуемые блокировки удаляются. Так как `IEventRepository` и `IBookingRepository` в пределах запроса делят один
`AppDbContext`, сохранение новой брони одной транзакцией фиксирует и изменение `AvailableSeats`
у отслеживаемого события. Если мест нет, выбрасывается `NoAvailableSeatsException` → `409 Conflict`.
Операции чтения (`GetBookingByIdAsync`) синхронизацией не охватываются.
Пример сценария использования
Сценарий 1. Pending → Confirmed
```bash
# 1. Создаём событие
curl -X POST http://localhost:5134/events \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Митап по C#",
    "description": "Обсуждаем .NET",
    "startAt": "2030-09-15T18:00:00Z",
    "endAt":   "2030-09-15T20:00:00Z",
    "totalSeats": 50
  }'
# → 201 Created, в теле объект события с id

# 2. Создаём бронь
curl -i -X POST http://localhost:5134/events/<event-id>/book \
  -H "Authorization: Bearer $USER_TOKEN"
# → 202 Accepted
#   Location: /bookings/<booking-id>
#   В теле бронь со status="Pending"

# 3. Сразу проверяем статус
curl http://localhost:5134/bookings/<booking-id> \
  -H "Authorization: Bearer $USER_TOKEN"
# → 200 OK, status="Pending"

# 4. Ждём ~3 секунды и повторяем запрос
sleep 3 && curl http://localhost:5134/bookings/<booking-id> \
  -H "Authorization: Bearer $USER_TOKEN"
# → 200 OK, status="Confirmed", processedAt заполнено
```
Данные сохраняются в PostgreSQL и доступны после перезапуска приложения.
Сценарий 2. Овербукинг: 3 места, 4 брони
```bash
# 1. Создаём событие на 3 места
curl -X POST http://localhost:5134/events \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "title": "Маленький зал", "startAt": "2030-09-15T18:00:00Z", "endAt": "2030-09-15T20:00:00Z", "totalSeats": 3 }'
# → 201 Created, totalSeats=3, availableSeats=3

# 2. Создаём три брони — все успешны
curl -i -X POST http://localhost:5134/events/<event-id>/book -H "Authorization: Bearer $USER_TOKEN"   # → 202
curl -i -X POST http://localhost:5134/events/<event-id>/book -H "Authorization: Bearer $USER_TOKEN"   # → 202
curl -i -X POST http://localhost:5134/events/<event-id>/book -H "Authorization: Bearer $USER_TOKEN"   # → 202

# 3. Четвёртая бронь — мест больше нет
curl -i -X POST http://localhost:5134/events/<event-id>/book -H "Authorization: Bearer $USER_TOKEN"
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
Юнит-тесты (`EventsApi.Tests`) — xUnit + FluentAssertions, паттерн AAA. Тесты
`BookingService` используют изолированные тестовые реализации `IEventRepository` и
`IBookingRepository` без EF Core. Остальные сервисные и конкурентные тесты используют
InMemory-провайдер через `TestHost`; для каждого контейнера создаётся уникальная база.
Тесты на конкурентность создают отдельный scope для каждого параллельного запроса.
Что покрыто:
`EventService`: CRUD, фильтрация (`title`, `from`, `to`, комбинации), пагинация, валидация
(включая `TotalSeats`: обязательность и значение > 0; `AvailableSeats = TotalSeats` при создании)
Сущность `Booking`: `CreatePending`, переходы `Confirm`/`Reject`, защита от повторных переходов
`BookingService`: создание для существующего/удалённого/несуществующего события, несколько броней с уникальными Id, чтение по Id, отражение смены статуса (Confirm/Reject); уменьшение `AvailableSeats` после каждой успешной брони; брони до лимита; `NoAvailableSeatsException` при исчерпании мест; восстановление мест после `Reject()` + `ReleaseSeats()` и новая бронь на освободившееся место
Конкурентность (`BookingConcurrencyTests`): защита от овербукинга — событие на 5 мест, 20 конкурентных запросов (`Task.Run` + `Task.WhenAll`, отдельный scope на запрос) → ровно 5 успешных броней, 15 `NoAvailableSeatsException`, `AvailableSeats = 0`; уникальность Id — 10 мест, 10 одновременных запросов → 10 броней с уникальными Id
`BookingProcessor`: Pending → Confirmed, Pending → Rejected при удалённом событии, параллельная обработка нескольких броней (3 брони по 2 с обрабатываются быстрее последовательных 6 с), корректная отмена через `StopAsync`, отсутствие активности на пустой базе

Интеграционные тесты (`EventsApi.IntegrationTests`) — xUnit + Testcontainers на реальной
PostgreSQL. Покрывают применение миграций, HTTP pipeline через `WebApplicationFactory`
и все методы обоих репозиториев (см. раздел
«Интеграционные тесты»). Требуют запущенного Docker.
Примечания
Данные событий и бронирований хранятся в PostgreSQL через EF Core и сохраняются между перезапусками приложения.
Схема БД создаётся и обновляется миграциями EF Core
(`MigrateWithLegacyBaselineAsync()` при старте), а не `EnsureCreated()`.
Все даты бронирований фиксируются в UTC.
Период опроса фонового сервиса — 500 мс (`PollingInterval`), имитация задержки внешнего вызова — 2 с (`ProcessingDelay`).
