﻿FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MediaFeeder/MediaFeeder/MediaFeeder.csproj", "MediaFeeder/MediaFeeder/"]
RUN dotnet restore "MediaFeeder/MediaFeeder/MediaFeeder.csproj"
COPY . .
WORKDIR "/src/MediaFeeder/MediaFeeder"
RUN dotnet build "MediaFeeder.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG HASH="none"
RUN dotnet publish "MediaFeeder.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:VersionSuffix=${HASH}

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MediaFeeder.dll"]
