FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["samples/MongoRepository.Sample/MongoRepository.Sample.csproj", "samples/MongoRepository.Sample/"]
COPY ["src/MongoRepository.Core/MongoRepository.Core.csproj", "src/MongoRepository.Core/"]
RUN dotnet restore "samples/MongoRepository.Sample/MongoRepository.Sample.csproj"
COPY . .
WORKDIR "/src/samples/MongoRepository.Sample"
RUN dotnet build "MongoRepository.Sample.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MongoRepository.Sample.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
# Install wget for health checks
RUN apt-get update && apt-get install -y wget && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MongoRepository.Sample.dll"] 