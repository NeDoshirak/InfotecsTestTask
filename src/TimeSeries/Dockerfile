FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/TimeSeries/TimeSeries.sln", "src/TimeSeries/"]
COPY ["src/TimeSeries/TimeSeries.csproj", "src/TimeSeries/"]
COPY ["src/TimeSeries.Domain/TimeSeries.Domain.csproj", "src/TimeSeries.Domain/"]
RUN dotnet restore "src/TimeSeries/TimeSeries.csproj"

COPY . .
WORKDIR "/src/src/TimeSeries"
RUN dotnet build "TimeSeries.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "TimeSeries.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TimeSeries.dll"]
