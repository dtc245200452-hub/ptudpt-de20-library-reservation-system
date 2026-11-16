FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for caching restore layer
COPY ["src/Library.Contracts/Library.Contracts.csproj", "src/Library.Contracts/"]
COPY ["src/Library.Data/Library.Data.csproj", "src/Library.Data/"]
COPY ["src/Library.Api/Library.Api.csproj", "src/Library.Api/"]

RUN dotnet restore "src/Library.Api/Library.Api.csproj"

# Copy all source files
COPY src/ src/

# Publish Release
WORKDIR "/src/src/Library.Api"
RUN dotnet publish "Library.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Library.Api.dll"]
