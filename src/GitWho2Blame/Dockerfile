FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["GitWho2Blame/GitWho2Blame.csproj", "GitWho2Blame/"]
RUN dotnet restore "GitWho2Blame/GitWho2Blame.csproj"
COPY . .
WORKDIR "/src/GitWho2Blame"
RUN dotnet build "./GitWho2Blame.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GitWho2Blame.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GitWho2Blame.dll"]
