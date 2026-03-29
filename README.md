EventsApi/
├── Controllers/
│   └── EventsController.cs   # Эндпоинты API
├── DTOs/
│   ├── CreateEventDto.cs     # DTO для создания
│   ├── UpdateEventDto.cs     # DTO для обновления
│   └── EventDto.cs           # DTO ответа
├── Models/
│   └── Event.cs              # Доменная модель
├── Services/
│   ├── IEventService.cs      # Интерфейс сервиса
│   └── EventService.cs       # Реализация (хранение в памяти)
├── Program.cs                # Точка входа, DI, Swagger
└── EventsApi.csproj
