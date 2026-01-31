FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Ailos.Tarifa.Worker/Ailos.Tarifa.Worker.csproj", "src/Ailos.Tarifa.Worker/"]
COPY ["src/Ailos.EncryptedId/Ailos.EncryptedId.csproj", "src/Ailos.EncryptedId/"]
RUN dotnet restore "src/Ailos.Tarifa.Worker/Ailos.Tarifa.Worker.csproj"
COPY . .
WORKDIR "/src/src/Ailos.Tarifa.Worker"
RUN dotnet build "Ailos.Tarifa.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ailos.Tarifa.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/data /app/logs
ENTRYPOINT ["dotnet", "Ailos.Tarifa.Worker.dll"]
