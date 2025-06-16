FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .

# Restore and publish only the base project (pulls in others via .csproj references)
RUN dotnet restore "src/GitWho2Blame/GitWho2Blame.csproj"
RUN dotnet publish "src/GitWho2Blame/GitWho2Blame.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app

COPY --from=build /app/publish .

# Pass transport explicitly for MCP
ENTRYPOINT ["./GitWho2Blame", "--transport", "stdio"]
