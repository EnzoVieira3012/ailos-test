Write-Host "Verificando Kafka..." -ForegroundColor Green

# Verificar se o container do Kafka esta rodando
$kafkaContainer = docker ps --filter "name=ailos-kafka" --format "{{.Names}}"
if (-not $kafkaContainer) {
    Write-Host "Container do Kafka nao esta rodando." -ForegroundColor Red
    exit 1
}

Write-Host "Container do Kafka esta rodando: $kafkaContainer" -ForegroundColor Green

# Aguardar Kafka ficar pronto
Write-Host "Aguardando Kafka ficar pronto..." -ForegroundColor Yellow
$maxRetries = 30
$retryCount = 0
$kafkaReady = $false

while (-not $kafkaReady -and $retryCount -lt $maxRetries) {
    try {
        $topics = docker exec ailos-kafka kafka-topics --list --bootstrap-server localhost:9092 2>$null
        if ($LASTEXITCODE -eq 0) {
            $kafkaReady = $true
            Write-Host "Kafka esta pronto!" -ForegroundColor Green
        }
    } catch {
        # Ignorar erro
    }
    
    if (-not $kafkaReady) {
        $retryCount++
        Write-Host "Tentativa $retryCount de $maxRetries..." -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }
}

if (-not $kafkaReady) {
    Write-Host "Kafka nao ficou pronto apos $maxRetries tentativas." -ForegroundColor Red
    exit 1
}

# Criar topicos se nao existirem
Write-Host "Criando topicos..." -ForegroundColor Green

$topics = docker exec ailos-kafka kafka-topics --list --bootstrap-server localhost:9092

if ($topics -notcontains "transferencias-realizadas") {
    Write-Host "Criando topico: transferencias-realizadas" -ForegroundColor Yellow
    docker exec ailos-kafka kafka-topics --create --bootstrap-server localhost:9092 --topic transferencias-realizadas --partitions 1 --replication-factor 1 --config retention.ms=604800000
} else {
    Write-Host "Topico 'transferencias-realizadas' ja existe." -ForegroundColor Green
}

if ($topics -notcontains "tarifas-processadas") {
    Write-Host "Criando topico: tarifas-processadas" -ForegroundColor Yellow
    docker exec ailos-kafka kafka-topics --create --bootstrap-server localhost:9092 --topic tarifas-processadas --partitions 1 --replication-factor 1 --config retention.ms=604800000
} else {
    Write-Host "Topico 'tarifas-processadas' ja existe." -ForegroundColor Green
}

Write-Host "Topicos disponiveis:" -ForegroundColor Green
docker exec ailos-kafka kafka-topics --list --bootstrap-server localhost:9092

Write-Host "Configuracao do Kafka concluida!" -ForegroundColor Green