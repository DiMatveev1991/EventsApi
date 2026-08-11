FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Services/Users/Users.Presentation/Users.Presentation.csproj
RUN dotnet publish src/Services/Users/Users.Presentation/Users.Presentation.csproj \
    --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Users.Presentation.dll"]
