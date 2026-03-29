using EventsApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// DI — Singleton так как данные хранятся в памяти
builder.Services.AddSingleton<IEventService, EventService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
	{
		Title = "Events API",
		Version = "v1",
		Description = "REST API для управления мероприятиями"
	});
});

var app = builder.Build();

// Swagger доступен всегда
app.UseSwagger();
app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/swagger/v1/swagger.json", "Events API v1");
	options.RoutePrefix = string.Empty; // Swagger открывается на http://localhost:5000
});

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

