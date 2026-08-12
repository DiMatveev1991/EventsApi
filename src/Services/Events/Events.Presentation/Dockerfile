FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Services/Events/Events.Presentation/Events.Presentation.csproj
RUN dotnet publish src/Services/Events/Events.Presentation/Events.Presentation.csproj \
    --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "EventsApi.Presentation.dll"]
