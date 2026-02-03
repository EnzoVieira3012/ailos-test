# ============================================
# SCRIPT PARA CRIAR CONTA ADMIN
# ============================================

Write-Host "Criando conta ADMIN no Ailos..."
Write-Host "=================================="

# Configuracoes
$apiUrl = "http://localhost:5080/api/contacorrente/cadastrar"
$requestBody = '{"cpf": "11144477735", "senha": "Admin@123", "nome": "ADMIN USER"}'

# Headers
$headers = @{
    "Content-Type" = "application/json"
    "Accept" = "application/json"
}

# Fazer requisicao
Write-Host "Enviando requisicao para: $apiUrl"
Write-Host "Dados: $requestBody"

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method POST -Headers $headers -Body $requestBody -ErrorAction Stop
    
    Write-Host ""
    Write-Host "CONTA CRIADA COM SUCESSO!"
    Write-Host "=================================="
    Write-Host "ID Ofuscado: $($response.id)"
    Write-Host "Numero da Conta: $($response.numero)"
    Write-Host "CPF: 111.444.777-35"
    Write-Host "Nome: ADMIN USER"
    
    # Salvar JSON
    $response | ConvertTo-Json | Out-File -FilePath ".\conta-admin.json" -Encoding UTF8
    Write-Host ""
    Write-Host "Informacoes salvas em: .\conta-admin.json"
    
} catch {
    Write-Host ""
    Write-Host "ERRO AO CRIAR CONTA!"
    Write-Host "=================================="
    Write-Host "Erro: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "=================================="
Write-Host "Script finalizado!"