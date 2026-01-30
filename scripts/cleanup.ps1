Write-Host "Limpando projeto..." -ForegroundColor Yellow

# Parar containers Docker
docker-compose down

# Limpar binários
$directories = @(
    "src/Ailos.EncryptedId.TestApi/bin",
    "src/Ailos.EncryptedId.TestApi/obj",
    "src/Ailos.EncryptedId/bin", 
    "src/Ailos.EncryptedId/obj",
    "tests/Ailos.EncryptedId.Tests/bin",
    "tests/Ailos.EncryptedId.Tests/obj"
)

foreach ($dir in $directories) {
    if (Test-Path $dir) {
        Remove-Item -Recurse -Force $dir
        Write-Host "Removido: $dir" -ForegroundColor Gray
    }
}

# Remover diretórios problemáticos (se existirem)
$problemDirs = @(
    "src/Ailos.EncryptedId/DependencyInjection",
    "src/Ailos.EncryptedId/Configuration"
)

foreach ($dir in $problemDirs) {
    if (Test-Path $dir) {
        Remove-Item -Recurse -Force $dir
        Write-Host "Removido diretório problemático: $dir" -ForegroundColor Yellow
    }
}

Write-Host "Restaurando pacotes..." -ForegroundColor Cyan
dotnet restore

Write-Host "Build do projeto..." -ForegroundColor Cyan
dotnet build --no-restore

Write-Host "Executando testes..." -ForegroundColor Cyan
dotnet test --no-build --verbosity normal

Write-Host "Limpeza concluída!" -ForegroundColor Green
