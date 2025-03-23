FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/MongoPatterns.Sample/MongoPatterns.Sample.csproj", "src/MongoPatterns.Sample/"]
COPY ["src/MongoPatterns.Repository/MongoPatterns.Repository.csproj", "src/MongoPatterns.Repository/"]
RUN dotnet restore "src/MongoPatterns.Sample/MongoPatterns.Sample.csproj"
COPY . .
WORKDIR "/src/src/MongoPatterns.Sample"
RUN dotnet build "MongoPatterns.Sample.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MongoPatterns.Sample.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
# Install wget for health checks
RUN apt-get update && apt-get install -y wget && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MongoPatterns.Sample.dll"] 