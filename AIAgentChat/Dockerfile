# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY AIAgentChat/*.csproj ./AIAgentChat/
RUN dotnet restore AIAgentChat/AIAgentChat.csproj

# Copy everything and build
COPY AIAgentChat/ ./AIAgentChat/
WORKDIR /src/AIAgentChat
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The app needs interactive mode, so we just set the entrypoint
ENTRYPOINT ["dotnet", "AIAgentChat.dll"]
