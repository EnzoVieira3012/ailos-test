Write-Host "Limpando ambiente..." -ForegroundColor Yellow

# Parar e remover containers
Write-Host "1. Parando containers..." -ForegroundColor Cyan
docker-compose down

# Remover volumes (opcional - cuidado com dados)
$removeData = Read-Host "Deseja remover os dados tambem? (s/n)"
if ($removeData -eq "s") {
    Write-Host "Removendo volumes..." -ForegroundColor Cyan
    docker volume prune -f
    Remove-Item -Path ".\data" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path ".\logs" -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path ".\data" -Force
    New-Item -ItemType Directory -Path ".\logs" -Force
}

# Rebuildar imagens
Write-Host "2. Rebuildando imagens..." -ForegroundColor Cyan
docker-compose build

# Iniciar containers
Write-Host "3. Iniciando containers..." -ForegroundColor Cyan
docker-compose up -d

# Aguardar inicializacao
Write-Host "4. Aguardando inicializacao..." -ForegroundColor Cyan
Start-Sleep -Seconds 20

# Verificar Kafka
Write-Host "5. Configurando Kafka..." -ForegroundColor Cyan
.\scripts\kafka\check-kafka.ps1

# Testar sistema
Write-Host "6. Testando sistema..." -ForegroundColor Cyan
.\scripts\test-system.ps1

Write-Host "Ambiente reiniciado com sucesso!" -ForegroundColor Green