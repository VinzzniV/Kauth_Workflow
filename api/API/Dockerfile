# api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY API.csproj ./
RUN dotnet restore API.csproj

COPY . ./
RUN dotnet publish API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

EXPOSE 8080
ENTRYPOINT ["dotnet", "API.dll"]
