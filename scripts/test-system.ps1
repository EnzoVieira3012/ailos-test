Write-Host "Testando Sistema Ailos..." -ForegroundColor Green
Write-Host "========================================="

# 1. Testar API Conta Corrente
Write-Host "1. Testando API Conta Corrente..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5080/health" -Method Get -TimeoutSec 5
    Write-Host "   API Conta Corrente: OK" -ForegroundColor Green
} catch {
    Write-Host "   API Conta Corrente: OFFLINE" -ForegroundColor Red
}

# 2. Testar API Transferencia
Write-Host "2. Testando API Transferencia..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5081/health" -Method Get -TimeoutSec 5
    Write-Host "   API Transferencia: OK" -ForegroundColor Green
} catch {
    Write-Host "   API Transferencia: OFFLINE" -ForegroundColor Red
}

# 3. Testar Kafka
Write-Host "3. Testando Kafka..." -ForegroundColor Yellow
$kafkaContainer = docker ps --filter "name=ailos-kafka" --format "{{.Names}}"
if ($kafkaContainer) {
    Write-Host "   Kafka Container: RODANDO" -ForegroundColor Green
    
    # Verificar topicos
    $topics = docker exec ailos-kafka kafka-topics --list --bootstrap-server localhost:9092 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   Kafka Topics: OK" -ForegroundColor Green
        $topics | ForEach-Object { Write-Host "      - $_" }
    } else {
        Write-Host "   Kafka Topics: ERRO" -ForegroundColor Red
    }
} else {
    Write-Host "   Kafka Container: PARADO" -ForegroundColor Red
}

# 4. Testar Worker de Tarifa
Write-Host "4. Testando Worker de Tarifa..." -ForegroundColor Yellow
$workerContainer = docker ps --filter "name=ailos-tarifa-worker" --format "{{.Names}}"
if ($workerContainer) {
    Write-Host "   Worker Container: RODANDO" -ForegroundColor Green
} else {
    Write-Host "   Worker Container: PARADO" -ForegroundColor Red
}

Write-Host "========================================="
Write-Host "Teste concluido!" -ForegroundColor Green