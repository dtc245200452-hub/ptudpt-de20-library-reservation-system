FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for caching restore layer
COPY ["src/Library.Contracts/Library.Contracts.csproj", "src/Library.Contracts/"]
COPY ["src/Library.Data/Library.Data.csproj", "src/Library.Data/"]
COPY ["src/Library.Worker/Library.Worker.csproj", "src/Library.Worker/"]

RUN dotnet restore "src/Library.Worker/Library.Worker.csproj"

# Copy all source files
COPY src/ src/

# Publish Release
WORKDIR "/src/src/Library.Worker"
RUN dotnet publish "Library.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Library.Worker.dll"]
