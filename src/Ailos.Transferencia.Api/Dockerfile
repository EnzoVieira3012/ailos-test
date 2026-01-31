FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Ailos.Transferencia.Api/Ailos.Transferencia.Api.csproj", "src/Ailos.Transferencia.Api/"]
COPY ["src/Ailos.EncryptedId/Ailos.EncryptedId.csproj", "src/Ailos.EncryptedId/"]
RUN dotnet restore "src/Ailos.Transferencia.Api/Ailos.Transferencia.Api.csproj"
COPY . .
WORKDIR "/src/src/Ailos.Transferencia.Api"
RUN dotnet build "Ailos.Transferencia.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ailos.Transferencia.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/data
ENTRYPOINT ["dotnet", "Ailos.Transferencia.Api.dll"]