EventsApi
REST API для управления мероприятиями, построенный на ASP.NET Core Web API (.NET 9).
Быстрый старт
Требования
.NET 9 SDK (обязательно). Проект использует `TargetFramework=net9.0` во всех трёх csproj
(`EventsApi`, `EventsApi.DTOs`, `EventsApi.Tests`). На .NET 8 SDK сборка не пройдёт.
Проверить установленную версию:
```bash
  dotnet --list-sdks
  ```
Скачать .NET 9 SDK можно с официального сайта Microsoft: https://dotnet.microsoft.com/download/dotnet/9.0
Запуск
```bash
git clone <URL репозитория>
cd EventsApi

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
│   └── BookingProcessor.cs            # Параллельная фоновая обработка Pending-броней
├── DataAccess/
│   ├── IBookingStore.cs
│   ├── InMemoryBookingStore.cs        # In-memory хранилище бронирований
│   ├── IEventStore.cs
│   └── InMemoryEventStore.cs          # In-memory хранилище событий
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
│   ├── Event.cs                       # + TotalSeats/AvailableSeats, TryReserveSeats/ReleaseSeats
│   └── Booking.cs
├── Services/
│   ├── IEventService.cs
│   ├── EventService.cs
│   ├── IBookingService.cs
│   └── BookingService.cs              # Критическая секция бронирования под lock
├── EventsApi.Tests/                   # xUnit + FluentAssertions
│   ├── EventServiceCrudTests.cs
│   ├── EventServiceFilteringTests.cs
│   ├── EventServicePaginationTests.cs
│   ├── EventServiceValidationTests.cs
│   ├── BookingEntityTests.cs
│   ├── InMemoryBookingStoreTests.cs
│   ├── BookingServiceTests.cs
│   ├── BookingConcurrencyTests.cs     # Тесты на овербукинг и уникальность Id
│   └── BookingProcessorTests.cs
├── Program.cs
└── EventsApi.csproj
```
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
Алгоритм работы:
Каждые `500 мс` (`PollingInterval`) сервис опрашивает `IBookingStore` и забирает брони в статусе `Pending`.
Все найденные брони обрабатываются параллельно через `Task.WhenAll`; логика одной брони вынесена в `ProcessBookingAsync`. Для каждой брони выполняется `Task.Delay(2 сек)` (`ProcessingDelay`) — имитация обращения к внешней системе; задержки разных броней идут одновременно.
После «обработки» под семафором проверяется, существует ли ещё событие, к которому относится бронь:
событие найдено → бронь переводится в `Confirmed`;
событие удалено → бронь переводится в `Rejected` (лог `Warning`);
непредвиденная ошибка → бронь переводится в `Rejected`, место возвращается в пул через `ReleaseSeats()`, обновляются оба хранилища.
Заполняется поле `ProcessedAt` (UTC), бронь сохраняется в хранилище.
Сервис корректно реагирует на отмену (`CancellationToken`): на остановке хоста все запущенные задачи прерываются, необработанные брони остаются `Pending`. Все ошибки логируются (`ILogger<BookingProcessor>`), но не приводят к падению сервиса.
Примитивы синхронизации
В спринте 4 сервис доработан для корректной обработки конкурентных запросов. Используются два примитива:
`lock (_bookingLock)` в `BookingService.CreateBookingAsync`. Классическая конкурентная
проблема: два потока одновременно читают `availableSeats > 0`, оба решают создать бронь —
в итоге броней больше, чем мест (овербукинг). Поэтому критическая секция
«получение события → проверка и резервирование места (`TryReserveSeats`) → сохранение
события → создание и сохранение брони» выполняется атомарно: через неё в любой момент
проходит ровно один поток. Если мест нет, выбрасывается `NoAvailableSeatsException` → `409 Conflict`.
Операции чтения (`GetBookingByIdAsync`) блокировкой не охватываются — `lock` покрывает
минимально необходимую секцию.
`SemaphoreSlim(1, 1)` (`_processingSemaphore`) в `BookingProcessor`. Защищает секцию
«проверка события + смена статуса + запись в хранилища» при параллельной обработке броней.
Семафор захватывается через `WaitAsync` после имитации внешнего вызова (поэтому задержки
выполняются параллельно) и освобождается в `finally`. Обычный `lock` здесь не подходит:
внутри защищаемой секции есть `await`, а `lock` нельзя использовать с `await` —
`SemaphoreSlim` является асинхронным аналогом мьютекса.
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
Сценарий 2. Pending → Rejected
```bash
# 1. Создаём событие
curl -X POST http://localhost:5134/events \
  -H "Content-Type: application/json" \
  -d '{ "title": "Будет удалено", "startAt": "2025-09-15T18:00:00", "endAt": "2025-09-15T20:00:00", "totalSeats": 10 }'

# 2. Создаём бронь
curl -i -X POST http://localhost:5134/events/<event-id>/book
# → 202 Accepted, status="Pending"

# 3. Сразу удаляем событие
curl -X DELETE http://localhost:5134/events/<event-id>
# → 204 No Content

# 4. Ждём ~3 секунды
sleep 3 && curl http://localhost:5134/bookings/<booking-id>
# → 200 OK, status="Rejected", processedAt заполнено
```
Сценарий 3. Овербукинг: 3 места, 4 брони
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
ровно `totalSeats` — атомарность пары «проверка + резервирование» гарантирует `lock`.
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
xUnit + FluentAssertions, паттерн AAA.
Что покрыто:
`EventService`: CRUD, фильтрация (`title`, `from`, `to`, комбинации), пагинация, валидация
(включая `TotalSeats`: обязательность и значение > 0; `AvailableSeats = TotalSeats` при создании)
Сущность `Booking`: `CreatePending`, переходы `Confirm`/`Reject`, защита от повторных переходов
`InMemoryBookingStore`: Add/Get/Update, фильтр Pending, дубликаты, неизвестные Id
`BookingService`: создание для существующего/удалённого/несуществующего события, несколько броней с уникальными Id, чтение по Id, отражение смены статуса (Confirm/Reject); уменьшение `AvailableSeats` после каждой успешной брони; брони до лимита; `NoAvailableSeatsException` при исчерпании мест; восстановление мест после `Reject()` + `ReleaseSeats()` и новая бронь на освободившееся место
Конкурентность (`BookingConcurrencyTests`): защита от овербукинга — событие на 5 мест, 20 конкурентных запросов (`Task.Run` + `Task.WhenAll`) → ровно 5 успешных броней, 15 `NoAvailableSeatsException`, `AvailableSeats = 0`; уникальность Id — 10 мест, 10 одновременных запросов → 10 броней с уникальными Id
`BookingProcessor`: Pending → Confirmed, Pending → Rejected при удалённом событии, параллельная обработка нескольких броней (3 брони по 2 с обрабатываются быстрее последовательных 6 с), корректная отмена через `StopAsync`, отсутствие активности на пустом сторе
Примечания
Данные событий и бронирований хранятся в памяти (Singleton) и сбрасываются при перезапуске.
Все даты бронирований фиксируются в UTC.
Период опроса фонового сервиса — 500 мс (`PollingInterval`), имитация задержки внешнего вызова — 2 с (`ProcessingDelay`).