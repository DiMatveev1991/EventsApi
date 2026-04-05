EventsApi
REST API для управления мероприятиями, построенный на ASP.NET Core Web API (.NET 9).
Быстрый старт
Требования

.NET 9 SDK

Запуск
bash# Клонировать репозиторий
git clone <URL репозитория>
cd EventsApi

# Собрать проект
dotnet build

# Запустить
dotnet run
После запуска API будет доступен по адресу:

HTTP: http://localhost:5134
HTTPS: https://localhost:7201

Swagger UI
Откройте в браузере: http://localhost:5134/swagger

Структура проекта
EventsApi/
├── Controllers/
│   └── EventsController.cs   # Эндпоинты API
├── EventsApi.DTOs/
│   ├── CreateEventDto.cs     # DTO для создания мероприятия
│   ├── UpdateEventDto.cs     # DTO для обновления мероприятия
│   └── EventDto.cs           # DTO ответа
├── Models/
│   └── Event.cs              # Доменная модель
├── Services/
│   ├── IEventService.cs      # Интерфейс сервиса
│   └── EventService.cs       # Реализация (хранение в памяти)
├── Program.cs                # Точка входа, DI, Swagger
└── EventsApi.csproj

Документация API
Модель мероприятия
ПолеТипОбязательноеОписаниеidguid—Уникальный идентификаторtitlestring✅Название мероприятияdescriptionstring❌Описание (может быть null)startAtdatetime✅Дата и время началаendAtdatetime✅Дата и время окончания
Эндпоинты
GET /events — получить список всех мероприятий
Ответ 200 OK:
json[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Конференция DevDays",
    "description": "Ежегодная конференция разработчиков",
    "startAt": "2025-06-01T10:00:00",
    "endAt": "2025-06-01T18:00:00"
  }
]

GET /events/{id} — получить мероприятие по ID
Параметры пути: id (guid)
Ответы:

200 OK — мероприятие найдено
404 Not Found — мероприятие не найдено

json// 404
{ "message": "Мероприятие с ID ... не найдено" }

POST /events — создать мероприятие
Тело запроса:
json{
  "title": "Митап по C#",
  "description": "Обсуждаем новинки .NET 9",
  "startAt": "2025-09-15T18:00:00",
  "endAt": "2025-09-15T20:00:00"
}
Ответы:

201 Created — мероприятие создано, в теле — созданный объект
400 Bad Request — ошибки валидации

Правила валидации:

title — обязательное, не пустое
startAt — обязательное
endAt — обязательное, должно быть позже startAt


PUT /events/{id} — обновить мероприятие целиком
Параметры пути: id (guid)
Тело запроса — аналогично POST /events.
Ответы:

200 OK — мероприятие обновлено
400 Bad Request — ошибки валидации
404 Not Found — мероприятие не найдено


DELETE /events/{id} — удалить мероприятие
Параметры пути: id (guid)
Ответы:

204 No Content — мероприятие удалено
404 Not Found — мероприятие не найдено


Примечания

Данные хранятся в памяти и сбрасываются при перезапуске приложения.
Все даты передаются в формате ISO 8601, например: 2025-09-15T18:00:00.