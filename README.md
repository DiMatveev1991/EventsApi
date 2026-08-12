# Events system — Sprint 10

Система управления мероприятиями декомпозирована на три независимых ASP.NET Core
микросервиса. Каждый сервис владеет своей PostgreSQL-базой и построен по Clean
Architecture: Domain, Application, Infrastructure, Presentation.

## Состав системы

| Сервис | Ответственность | HTTP | База |
|---|---|---:|---:|
| Users/Auth | регистрация, вход, PBKDF2-хеширование, выдача JWT | 5001 | 5433 |
| Events | CRUD событий, доступные места, Kafka consumer | 5002 | 5434 |
| Bookings | создание/отмена броней, Kafka producer | 5003 | 5435 |

Redis используется Events как необязательный ускоряющий слой. Он не хранит
источник истины и не участвует в согласованности PostgreSQL-транзакций.

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
6. После фиксации изменений Events инвалидирует `event:{id}`. Ошибка Redis не
   отменяет уже сохранённое изменение.
7. Kafka offset фиксируется только после успешной обработки.

Если публикация не удалась после сохранения брони, Bookings найдёт подтверждённую,
но не опубликованную запись и повторит отправку. Если сообщение было доставлено
повторно, Events увидит уже обработанный BookingId и не уменьшит места ещё раз.
Отсутствующее событие и нехватка мест логируются, сообщение помечается обработанным,
а consumer продолжает работу.

## Redis и стратегия кеширования

Events использует Cache-Aside через порт `ICacheService` из Application. Redis и
JSON-сериализация находятся только в Infrastructure. `IConnectionMultiplexer`
зарегистрирован в DI как singleton: это тяжёлый потокобезопасный клиент, который
переиспользуется всеми запросами.

| Сценарий | Ключ | TTL | Обновление |
|---|---|---:|---|
| `GET /events/{id}` | `event:{id}` | 300 секунд | инвалидация после записи в БД |
| `GET /events/top` | `events:top10` | 60 секунд | только истечение TTL |

При чтении отдельного события и топа сначала проверяется Redis. Попадание сразу
возвращается клиенту без обращения к репозиторию. При промахе данные читаются из
PostgreSQL и сохраняются в Redis. Ответ `404` не кешируется, поэтому недавно
созданное событие не может быть скрыто отрицательным кешем.

Для отдельного события выбрана **инвалидация при записи**. После успешных
`POST`, `PUT` и `DELETE` удаляется `event:{id}`; следующий запрос прогревает его
актуальными данными. Тот же ключ удаляется после успешно применённого
`BookingConfirmed`, потому что Kafka меняет `AvailableSeats`. Дубликат Kafka-
сообщения кеш не трогает. Во всех случаях порядок один: сначала commit в БД,
затем операция с кешем. Поэтому сбой между шагами не теряет основное изменение.

Топ не инвалидируется при каждом бронировании или CRUD: это рейтинговый агрегат,
для которого допустимо устаревание не более минуты. Такой подход не создаёт
лишнюю Redis-нагрузку на горячем потоке Kafka. События сортируются по формуле
`(TotalSeats - AvailableSeats) / TotalSeats`; ответ содержит вычисленный
`soldPercentage` и не более десяти элементов.

TTL различаются: карточка события живёт пять минут, чтобы повторные чтения
редко ходили в БД, а быстро меняющийся топ — одну минуту. Оба значения и строка
подключения вынесены в `appsettings.json`:

    Redis__ConnectionString=redis:6379
    Redis__EventTtlSeconds=300
    Redis__TopEventsTtlSeconds=60

Redis не является обязательной зависимостью запуска. Адаптер логирует ошибки
чтения, записи и удаления, но не пробрасывает их клиенту: чтение деградирует до
обычного запроса PostgreSQL. Docker smoke-test останавливает Redis, перезапускает
Events API и подтверждает, что `GET /events/{id}` продолжает работать.

Задержка подтверждения брони и интервал опроса задаются в секции
`BookingProcessing`. Искусственная задержка по умолчанию равна нулю, поэтому
рост нагрузки не создаёт очередь обязательных двухсекундных ожиданий. Producer
явно выполняет `Flush` при штатной остановке, а неподтверждённые публикации всё
равно подхватываются следующим циклом фонового обработчика.

## Запуск одной командой

Нужен Docker Desktop с поддержкой Docker Compose.

    docker compose up --build

Команда поднимает Zookeeper, Kafka, Redis, три PostgreSQL, три API и автоматически
применяет отдельные EF Core migrations каждого сервиса.

Эндпоинт `/health` каждого API проверяет собственную PostgreSQL-базу, а Events
и Bookings дополнительно проверяют Kafka. Поэтому Docker и внешняя система
мониторинга различают запущенный процесс и действительно готовый сервис.

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
6. Вызовите GET /events/top без токена. Ответ содержит до десяти событий,
   отсортированных по убыванию `soldPercentage`.

Проверить TTL вручную можно командами:

    docker compose exec redis redis-cli TTL event:EVENT_ID
    docker compose exec redis redis-cli TTL events:top10

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

Токен содержит NameIdentifier, имя и роль. Все три API используют одинаковую
JWT-валидацию; Events и Bookings при этом не имеют таблиц пользователей в своих
базах. Кнопка Authorize в Swagger Users теперь соответствует реальному
authentication pipeline, а не только добавляет декоративный заголовок.

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

Для Events пути имеют вид
`src/Services/Events/Events.Infrastructure/Events.Infrastructure.csproj` и
`src/Services/Events/Events.Presentation/Events.Presentation.csproj`.

## Сборка и тесты

    dotnet restore EventsApi.sln
    dotnet build EventsApi.sln --configuration Release --no-restore
    dotnet test EventsApi.sln --configuration Release --no-build

Решение содержит два тестовых проекта:

- `EventsApi.Tests` — unit-тесты сервисов Users, Events и Bookings, доменных
  инвариантов, JWT/PBKDF2, фильтрации, пагинации, Kafka-контракта,
  попаданий/промахов кеша, разных TTL и инвалидации после CRUD и Kafka;
  unit-проект не ссылается на Presentation;
- `EventsApi.IntegrationTests` — проверки HTTP-контрактов и трёх независимых
  EF Core-контекстов на настоящем PostgreSQL 16 через Testcontainers, включая
  индексы, миграции, сортировку топ-10 и конкурентную inbox-идемпотентность
  `BookingConfirmed`.

GitHub Actions выполняет restore, format verification, Release build, оба
тестовых проекта и полный Docker smoke-сценарий на каждый push и pull request.

## Структура

    src/
    ├── BuildingBlocks/Contracts
    └── Services/
        ├── Events/
        │   ├── Events.Domain
        │   ├── Events.Application
        │   ├── Events.Infrastructure
        │   └── Events.Presentation
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
