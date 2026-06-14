FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
# curl is used by the docker-compose healthcheck; not present in the slim base image
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["OrderRouter.Api/OrderRouter.Api.csproj", "OrderRouter.Api/"]
COPY ["OrderRouter.Services/OrderRouter.Services.csproj", "OrderRouter.Services/"]
RUN dotnet restore "OrderRouter.Api/OrderRouter.Api.csproj"

COPY . .
WORKDIR "/src/OrderRouter.Api"
RUN dotnet build "OrderRouter.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "OrderRouter.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Bake CSV data files into the image so the seeding step runs on startup
COPY data/ /app/data/

# /app/db is the mount point for the persistent SQLite volume
RUN mkdir -p /app/db

ENTRYPOINT ["dotnet", "OrderRouter.Api.dll"]
