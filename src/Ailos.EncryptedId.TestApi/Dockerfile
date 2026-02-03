FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Ailos.EncryptedId.TestApi/Ailos.EncryptedId.TestApi.csproj", "src/Ailos.EncryptedId.TestApi/"]
COPY ["src/Ailos.EncryptedId/Ailos.EncryptedId.csproj", "src/Ailos.EncryptedId/"]
RUN dotnet restore "src/Ailos.EncryptedId.TestApi/Ailos.EncryptedId.TestApi.csproj"
COPY . .
WORKDIR "/src/src/Ailos.EncryptedId.TestApi"
RUN dotnet build "Ailos.EncryptedId.TestApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ailos.EncryptedId.TestApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ailos.EncryptedId.TestApi.dll"]