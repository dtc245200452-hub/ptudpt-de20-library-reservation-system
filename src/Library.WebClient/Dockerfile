FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for caching restore layer
COPY ["src/Library.Contracts/Library.Contracts.csproj", "src/Library.Contracts/"]
COPY ["src/Library.WebClient/Library.WebClient.csproj", "src/Library.WebClient/"]

RUN dotnet restore "src/Library.WebClient/Library.WebClient.csproj"

# Copy all source files
COPY src/ src/

# Publish Release
WORKDIR "/src/src/Library.WebClient"
RUN dotnet publish "Library.WebClient.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Library.WebClient.dll"]
