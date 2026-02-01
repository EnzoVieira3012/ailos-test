Write-Host "🚀 Testando Fluxo Completo..." -ForegroundColor Green
Write-Host "========================================="

# 1. Criar conta
Write-Host "1. Criando conta..." -ForegroundColor Yellow
$body = @{
    cpf = "12345678909"
    senha = "senha123"
    nome = "João Silva"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5080/api/contacorrente/cadastrar" -Method Post -Body $body -ContentType "application/json"
    Write-Host "   ✅ Conta criada:" -ForegroundColor Green
    Write-Host "      ID: $($response.id.value)" -ForegroundColor Gray
    Write-Host "      Número: $($response.numero)" -ForegroundColor Gray
    
    # Salvar dados da conta
    $conta1 = @{
        Id = $response.id.value
        Numero = $response.numero
    }
} catch {
    Write-Host "   ❌ Erro ao criar conta: $_" -ForegroundColor Red
    exit 1
}

# 2. Fazer login
Write-Host "2. Fazendo login..." -ForegroundColor Yellow
$body = @{
    cpf = "12345678909"
    senha = "senha123"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5080/api/contacorrente/login" -Method Post -Body $body -ContentType "application/json"
    Write-Host "   ✅ Login realizado" -ForegroundColor Green
    
    $token = $response.token
    $contaId = $response.contaId.value
    
    Write-Host "      Token obtido: $($token.Substring(0, 20))..." -ForegroundColor Gray
    Write-Host "      Conta ID: $contaId" -ForegroundColor Gray
} catch {
    Write-Host "   ❌ Erro no login: $_" -ForegroundColor Red
    exit 1
}

# 3. Consultar saldo
Write-Host "3. Consultando saldo..." -ForegroundColor Yellow
try {
    $headers = @{
        Authorization = "Bearer $token"
    }
    
    $response = Invoke-RestMethod -Uri "http://localhost:5080/api/contacorrente/saldo" -Method Get -Headers $headers -ContentType "application/json"
    Write-Host "   ✅ Saldo atual: R$ $($response.saldo)" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Erro ao consultar saldo: $_" -ForegroundColor Yellow
}

# 4. Criar movimentação (crédito)
Write-Host "4. Criando movimentação (crédito)..." -ForegroundColor Yellow
$body = @{
    identificacaoRequisicao = "teste-credito-" + (Get-Date -Format "yyyyMMddHHmmss")
    valor = 1000.50
    tipoMovimento = "C"
    descricao = "Crédito inicial"
} | ConvertTo-Json

try {
    $headers = @{
        Authorization = "Bearer $token"
    }
    
    $response = Invoke-RestMethod -Uri "http://localhost:5080/api/movimentacao" -Method Post -Headers $headers -Body $body -ContentType "application/json"
    Write-Host "   ✅ Crédito realizado:" -ForegroundColor Green
    Write-Host "      Movimento ID: $($response.movimentoId.value)" -ForegroundColor Gray
    Write-Host "      Saldo atual: R$ $($response.saldoAtual)" -ForegroundColor Gray
} catch {
    Write-Host "   ❌ Erro ao criar movimentação: $_" -ForegroundColor Red
}

Write-Host "========================================="
Write-Host "🎊 Teste concluído com sucesso!" -ForegroundColor Green
Write-Host ""
Write-Host "🌐 URLs disponíveis:" -ForegroundColor Cyan
Write-Host "   Conta Corrente API: http://localhost:5080" -ForegroundColor Gray
Write-Host "   Transferência API: http://localhost:5081" -ForegroundColor Gray
Write-Host "   Kafka UI: http://localhost:8082" -ForegroundColor Gray