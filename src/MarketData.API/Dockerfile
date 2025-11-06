FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/MarketData.API/MarketData.API.csproj", "src/MarketData.API/"]
COPY ["src/MarketData.Application/MarketData.Application.csproj", "src/MarketData.Application/"]
COPY ["src/MarketData.Infrastructure/MarketData.Infrastructure.csproj", "src/MarketData.Infrastructure/"]
COPY ["src/MarketData.Domain/MarketData.Domain.csproj", "src/MarketData.Domain/"]

# Restore dependencies
RUN dotnet restore "src/MarketData.API/MarketData.API.csproj"

# Copy all source files
COPY . .

# Build
WORKDIR "/src/src/MarketData.API"
RUN dotnet build "MarketData.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MarketData.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MarketData.API.dll"]
