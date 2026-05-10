FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Struct.API/Struct.API.csproj", "src/Struct.API/"]
COPY ["src/Struct.BLL/Struct.BLL.csproj", "src/Struct.BLL/"]
COPY ["src/Struct.DAL/Struct.DAL.csproj", "src/Struct.DAL/"]
RUN dotnet restore "src/Struct.API/Struct.API.csproj"
COPY . .
WORKDIR "/src/src/Struct.API"
RUN dotnet build "./Struct.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Struct.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Struct.API.dll"]
