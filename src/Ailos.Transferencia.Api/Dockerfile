FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# ✅ ADICIONE ESTAS LINHAS PARA DIAGNÓSTICO
RUN apt-get update && \
    apt-get install -y curl iproute2 procps netcat-openbsd && \
    rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Ailos.Transferencia.Api/Ailos.Transferencia.Api.csproj", "src/Ailos.Transferencia.Api/"]
COPY ["src/Ailos.EncryptedId/Ailos.EncryptedId.csproj", "src/Ailos.EncryptedId/"]
COPY ["src/Ailos.Common/Ailos.Common.csproj", "src/Ailos.Common/"]
RUN dotnet restore "src/Ailos.Transferencia.Api/Ailos.Transferencia.Api.csproj"
COPY . .
WORKDIR "/src/src/Ailos.Transferencia.Api"
RUN dotnet build "Ailos.Transferencia.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ailos.Transferencia.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/data /app/logs && chmod 777 /app/logs

# ✅ ADICIONE HEALTHCHECK REAL
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "Ailos.Transferencia.Api.dll"]