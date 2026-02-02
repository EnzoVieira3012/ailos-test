# start.ps1 - Script para Windows PowerShell

# Adicione esta função no início do script:
function Wait-ForKafka {
    param([int]$MaxRetries = 20)
    
    $retryCount = 0
    $kafkaReady = $false
    
    Write-Host " Aguardando Kafka ficar pronto..." -ForegroundColor Yellow
    
    while ($retryCount -lt $MaxRetries -and -not $kafkaReady) {
        try {
            $kafkaStatus = docker-compose ps kafka --format json | ConvertFrom-Json
            if ($kafkaStatus.Status -like "*Up*") {
                # Testar conexão com Kafka
                docker-compose exec -T kafka bash -c "kafka-topics --list --bootstrap-server localhost:9092" > $null 2>&1
                if ($LASTEXITCODE -eq 0) {
                    $kafkaReady = $true
                    Write-Host " Kafka pronto! (tentativa $($retryCount + 1))" -ForegroundColor Green
                }
            }
        } catch {
            # Ignorar erros e continuar tentando
        }
        
        if (-not $kafkaReady) {
            $retryCount++
            Write-Host " Aguardando Kafka... ($retryCount/$MaxRetries)" -ForegroundColor Yellow
            Start-Sleep -Seconds 5
        }
    }
    
    if (-not $kafkaReady) {
        Write-Host " Kafka não ficou pronto após $MaxRetries tentativas" -ForegroundColor Red
        Write-Host " Verificando logs do Kafka..." -ForegroundColor Yellow
        docker logs ailos-kafka --tail 50
        exit 1
    }
}

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

# 4. Iniciar Kafka e Zookeeper
Write-Host " Iniciando Kafka e Zookeeper..." -ForegroundColor Cyan
docker-compose up -d zookeeper kafka

# 5. Aguardar Kafka inicializar
Wait-ForKafka -MaxRetries 30

# 6. Criar tópicos manualmente se necessário
Write-Host " Criando tópicos Kafka..." -ForegroundColor Cyan
try {
    $topic1Cmd = "kafka-topics --create --if-not-exists --topic transferencias-realizadas --bootstrap-server localhost:9092 --partitions 1 --replication-factor 1"
    docker-compose exec -T kafka bash -c $topic1Cmd
        
    $topic2Cmd = "kafka-topics --create --if-not-exists --topic tarifas-processadas --bootstrap-server localhost:9092 --partitions 1 --replication-factor 1"
    docker-compose exec -T kafka bash -c $topic2Cmd
        
    Write-Host " Tópicos criados com sucesso!" -ForegroundColor Green
} catch {
    Write-Host " Erro ao criar tópicos: $_" -ForegroundColor Yellow
    Write-Host " Continuando, os tópicos podem ser auto-criados..." -ForegroundColor Yellow
}

# 7. Listar tópicos para verificar
Write-Host " Listando tópicos Kafka..." -ForegroundColor Cyan
docker-compose exec -T kafka bash -c "kafka-topics --list --bootstrap-server localhost:9092"

# 8. Iniciar Conta Corrente API
Write-Host " Iniciando Conta Corrente API..." -ForegroundColor Cyan
docker-compose up -d conta-corrente-api

Write-Host " Aguardando API inicializar (20 segundos)..." -ForegroundColor Yellow
Start-Sleep -Seconds 20

# 9. Verificar Conta Corrente
Write-Host " Verificando Conta Corrente API..." -ForegroundColor Cyan
docker-compose ps
docker logs ailos-conta-corrente-api --tail 20

# 10. Testar health check
Write-Host " Testando health check da Conta Corrente..." -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5080/health" -TimeoutSec 10
    Write-Host " Health check OK: $($response.Content)" -ForegroundColor Green
} catch {
    Write-Host " Health check falhou: $_" -ForegroundColor Red
    docker logs ailos-conta-corrente-api
    exit 1
}

# 11. Iniciar Transferência API e Worker
Write-Host " Iniciando Transferência API e Tarifa Worker..." -ForegroundColor Cyan
docker-compose up -d transferencia-api tarifa-worker

Write-Host " Aguardando serviços inicializarem (15 segundos)..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# 12. Verificar todos os serviços
Write-Host " Status final de todos os serviços:" -ForegroundColor Green
docker-compose ps

# 13. Testar endpoints
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