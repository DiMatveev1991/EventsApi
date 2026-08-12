# Events system — Sprint 9

Система управления мероприятиями декомпозирована на три независимых ASP.NET Core
микросервиса. Каждый сервис владеет своей PostgreSQL-базой и построен по Clean
Architecture: Domain, Application, Infrastructure, Presentation.

## Состав системы

| Сервис | Ответственность | HTTP | База |
|---|---|---:|---:|
| Users/Auth | регистрация, вход, PBKDF2-хеширование, выдача JWT | 5001 | 5433 |
| Events | CRUD событий, доступные места, Kafka consumer | 5002 | 5434 |
| Bookings | создание/отмена броней, Kafka producer | 5003 | 5435 |

Swagger:

- Users: http://localhost:5001/swagger
- Events: http://localhost:5002/swagger
- Bookings: http://localhost:5003/swagger

Общий проект src/BuildingBlocks/Contracts содержит неизменяемый контракт
BookingConfirmed и константу топика booking-confirmed. Сервисы не вызывают
друг друга по HTTP.

## Поток BookingConfirmed

1. Авторизованный пользователь создаёт бронь в Bookings.
2. Фоновый обработчик переводит бронь в Confirmed и сначала сохраняет статус
   в bookings_db.
3. После фиксации статуса Bookings публикует JSON-сообщение в Kafka. Ключ —
   EventId, поэтому брони одного события попадают в один partition и сохраняют порядок.
4. Events читает сообщение в consumer group events-service, создавая отдельный
   DI scope для каждого сообщения.
5. Events атомарно уменьшает AvailableSeats и записывает BookingId в inbox-таблицу
   processed_booking_messages.
6. Kafka offset фиксируется только после успешной обработки.

Если публикация не удалась после сохранения брони, Bookings найдёт подтверждённую,
но не опубликованную запись и повторит отправку. Если сообщение было доставлено
повторно, Events увидит уже обработанный BookingId и не уменьшит места ещё раз.
Отсутствующее событие и нехватка мест логируются, сообщение помечается обработанным,
а consumer продолжает работу.

## Запуск одной командой

Нужен Docker Desktop с поддержкой Docker Compose.

    docker compose up --build

Команда поднимает Zookeeper, Kafka, три PostgreSQL, три API и автоматически
применяет отдельные EF Core migrations каждого сервиса.

Для локального запуска предусмотрен development JWT secret. Его можно заменить:

    JWT_SECRET="replace-with-at-least-32-bytes-secret" docker compose up --build

Остановка:

    docker compose down

Полный сброс баз и повторная проверка первого запуска:

    docker compose down -v
    docker compose up --build

## Проверка сценария

Все действия удобно выполнить через Swagger.

1. В Users вызовите POST /auth/register с ролью Admin, затем POST /auth/login.
2. В Events нажмите **Authorize**, вставьте токен и создайте событие через
   POST /events. Запомните id и availableSeats.
3. В Users зарегистрируйте обычного пользователя с ролью User, выполните login.
4. В Bookings авторизуйтесь пользовательским токеном и вызовите POST /bookings:

    {
      "eventId": "EVENT_ID",
      "seats": 1
    }

5. Подождите несколько секунд и повторите GET /events/{id}. Значение
   availableSeats уменьшится через Kafka.

Проверки безопасности:

- POST/PUT/DELETE /events без токена возвращают 401;
- те же запросы с ролью User возвращают 403;
- все /bookings требуют JWT;
- пользователь не может читать или отменять чужую бронь, Admin может.

## JWT

JWT выдаёт только Users. Все сервисы используют одинаковые:

- Jwt__Secret;
- Jwt__Issuer=EventsSystem;
- Jwt__Audience=EventsSystem.Clients.

Токен содержит NameIdentifier, имя и роль. Events и Bookings только проверяют
подпись и claims; таблиц пользователей в их базах нет.

## Отдельные базы и миграции

Контексты и начальные миграции:

- UsersDbContext → users_db → InitialUsers;
- AppDbContext (Events) → events_db → InitialEvents;
- BookingsDbContext → bookings_db → InitialBookings.

Связи между сервисами представлены только значениями UserId и EventId.
Межбазовых внешних ключей и EF navigation properties нет.

Пример создания следующей миграции Users:

    dotnet ef migrations add MigrationName \
      --project src/Services/Users/Users.Infrastructure/Users.Infrastructure.csproj \
      --startup-project src/Services/Users/Users.Presentation/Users.Presentation.csproj \
      --output-dir Persistence/Migrations

Для Events и Bookings используются соответствующие пары Infrastructure/Presentation.

## Сборка и тесты

    dotnet restore EventsApi.sln
    dotnet build EventsApi.sln --configuration Release --no-restore
    dotnet test EventsApi.sln --configuration Release --no-build

Решение содержит два тестовых проекта:

- `EventsApi.Tests` — unit-тесты сервисов Users, Events и Bookings, доменных
  инвариантов, JWT/PBKDF2, авторизации, фильтрации, пагинации и Kafka-контракта;
- `EventsApi.IntegrationTests` — проверки трёх независимых EF Core-контекстов,
  репозиториев, индексов, миграций, inbox-идемпотентности и обработки
  `BookingConfirmed`.

GitHub Actions выполняет restore, format verification, Release build, оба
тестовых проекта и полный Docker smoke-сценарий на каждый push и pull request.

## Структура

    src/
    ├── BuildingBlocks/Contracts
    ├── EventsApi.Domain
    ├── EventsApi.Application
    ├── EventsApi.Infrastructure
    ├── EventsApi.Presentation
    └── Services/
        ├── Users/
        │   ├── Users.Domain
        │   ├── Users.Application
        │   ├── Users.Infrastructure
        │   └── Users.Presentation
        └── Bookings/
            ├── Bookings.Domain
            ├── Bookings.Application
            ├── Bookings.Infrastructure
            └── Bookings.Presentation

    EventsApi.Tests/
    EventsApi.IntegrationTests/

В каждом сервисе направление зависимостей одинаково:
Presentation связывает Application и Infrastructure; Infrastructure реализует
порты Application; Domain не зависит от EF Core, ASP.NET Core или Kafka.
