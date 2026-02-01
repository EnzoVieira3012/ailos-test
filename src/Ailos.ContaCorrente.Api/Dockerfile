# src/Ailos.ContaCorrente.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
RUN mkdir -p /app/logs /app/data && chmod 777 /app/logs

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Ailos.ContaCorrente.Api/Ailos.ContaCorrente.Api.csproj", "src/Ailos.ContaCorrente.Api/"]
COPY ["src/Ailos.EncryptedId/Ailos.EncryptedId.csproj", "src/Ailos.EncryptedId/"]
COPY ["src/Ailos.Common/Ailos.Common.csproj", "src/Ailos.Common/"]
RUN dotnet restore "src/Ailos.ContaCorrente.Api/Ailos.ContaCorrente.Api.csproj"
COPY . .
WORKDIR "/src/src/Ailos.ContaCorrente.Api"
RUN dotnet build "Ailos.ContaCorrente.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ailos.ContaCorrente.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Ailos.ContaCorrente.Api.dll"]