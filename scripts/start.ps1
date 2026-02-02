# start.ps1 - Script para Windows PowerShell

Write-Host " Iniciando Ailos Banking System..." -ForegroundColor Green

# 1. Parar tudo
Write-Host " Parando containers existentes..." -ForegroundColor Yellow
docker-compose down -v

# 2. Limpar dados antigos
Write-Host " Limpando dados antigos..." -ForegroundColor Yellow
Remove-Item -Recurse -Force data, logs -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path data, logs

# 3. Reconstruir imagens
Write-Host " Reconstruindo imagens Docker..." -ForegroundColor Cyan
docker-compose build --no-cache

# 4. Iniciar Kafka
Write-Host " Iniciando Kafka e Zookeeper..." -ForegroundColor Cyan
docker-compose up -d zookeeper kafka

Write-Host " Aguardando Kafka inicializar (30 segundos)..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

# 5. Verificar Kafka
Write-Host " Verificando status do Kafka..." -ForegroundColor Cyan
docker-compose ps

# 6. Iniciar Conta Corrente API
Write-Host " Iniciando Conta Corrente API..." -ForegroundColor Cyan
docker-compose up -d conta-corrente-api

Write-Host " Aguardando API inicializar (20 segundos)..." -ForegroundColor Yellow
Start-Sleep -Seconds 20

# 7. Verificar Conta Corrente
Write-Host " Verificando Conta Corrente API..." -ForegroundColor Cyan
docker-compose ps
docker logs ailos-conta-corrente-api --tail 20

# 8. Testar health check
Write-Host " Testando health check da Conta Corrente..." -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5080/health" -TimeoutSec 10
    Write-Host " Health check OK: $($response.Content)" -ForegroundColor Green
} catch {
    Write-Host " Health check falhou: $_" -ForegroundColor Red
    docker logs ailos-conta-corrente-api
    exit 1
}

# 9. Iniciar Transferência API e Worker
Write-Host " Iniciando Transferência API e Tarifa Worker..." -ForegroundColor Cyan
docker-compose up -d transferencia-api tarifa-worker

Write-Host " Aguardando serviços inicializarem (15 segundos)..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# 10. Verificar todos os serviços
Write-Host " Status final de todos os serviços:" -ForegroundColor Green
docker-compose ps

# 11. Testar endpoints
Write-Host " Testando endpoints..." -ForegroundColor Cyan

$services = @(
    @{Name="Conta Corrente"; Url="http://localhost:5080/health"},
    @{Name="Transferência"; Url="http://localhost:5081/health"}
)

foreach ($service in $services) {
    try {
        $response = Invoke-WebRequest -Uri $service.Url -TimeoutSec 5
        Write-Host " $($service.Name): $($response.Content)" -ForegroundColor Green
    } catch {
        Write-Host " $($service.Name): Falhou" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "   Sistema Ailos iniciado com sucesso!" -ForegroundColor Green
Write-Host ""
Write-Host "   Swagger UI:" -ForegroundColor Yellow
Write-Host "   Conta Corrente: http://localhost:5080" -ForegroundColor Cyan
Write-Host "   Transferência:  http://localhost:5081" -ForegroundColor Cyan
Write-Host ""
Write-Host "   Kafka UI: http://localhost:8082" -ForegroundColor Cyan
Write-Host ""
Write-Host "   Logs:" -ForegroundColor Yellow
Write-Host "   docker logs -f ailos-conta-corrente-api" -ForegroundColor Gray
Write-Host "   docker logs -f ailos-transferencia-api" -ForegroundColor Gray
Write-Host "   docker logs -f ailos-tarifa-worker" -ForegroundColor Gray
Write-Host ""
Write-Host "   Para parar todos os serviços: docker-compose down" -ForegroundColor Red