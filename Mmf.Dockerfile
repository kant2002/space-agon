FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["mmf/MatchFunction.csproj", "mmf/"]
RUN dotnet restore "mmf/MatchFunction.csproj"
COPY . .
WORKDIR "/src/mmf"
RUN dotnet build "MatchFunction.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MatchFunction.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
EXPOSE 50502
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MatchFunction.dll"]