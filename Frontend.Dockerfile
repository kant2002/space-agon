FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["frontend/Frontend.csproj", "frontend/"]
RUN dotnet restore "frontend/Frontend.csproj"
COPY . .
WORKDIR "/src/frontend"
RUN dotnet build "Frontend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Frontend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Frontend.dll"]