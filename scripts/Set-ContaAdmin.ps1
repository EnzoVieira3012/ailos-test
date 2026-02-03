# ============================================
# SCRIPT PARA TORNAR CONTA ID 1 EM ADMIN
# ============================================

Write-Host "Configurando conta ID 1 como ADMIN..." -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

# 1. Configuracoes - Caminho ABSOLUTO do banco de dados
$databasePath = "C:\Users\enzov\source\repos\ailos-test\data\ailos.db"

# 2. Verificar se o arquivo do banco existe
if (-not (Test-Path $databasePath)) {
    Write-Host "Banco de dados nao encontrado em: $databasePath" -ForegroundColor Red
    Write-Host "Procurando em outros locais..." -ForegroundColor Yellow
    
    # Tentar outros caminhos possíveis
    $possiblePaths = @(
        "C:\Users\enzov\source\repos\ailos-test\data\ailos.db",
        ".\data\ailos.db",
        "..\data\ailos.db",
        "..\..\data\ailos.db"
    )
    
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $databasePath = $path
            Write-Host "Encontrado em: $databasePath" -ForegroundColor Green
            break
        }
    }
    
    if (-not (Test-Path $databasePath)) {
        Write-Host "Banco de dados nao encontrado em nenhum local comum." -ForegroundColor Red
        Write-Host "Verifique se:" -ForegroundColor Yellow
        Write-Host "   1. Docker Compose esta rodando (docker-compose up)" -ForegroundColor Gray
        Write-Host "   2. O volume foi criado corretamente" -ForegroundColor Gray
        Write-Host "" -ForegroundColor Yellow
        
        $databasePath = Read-Host "Digite o caminho COMPLETO do arquivo ailos.db (ex: C:\caminho\completo\ailos.db)"
        
        if (-not (Test-Path $databasePath)) {
            Write-Host "Arquivo nao encontrado. Encerrando." -ForegroundColor Red
            
            # Opcao: Tentar acessar via Docker
            Write-Host "" -ForegroundColor Yellow
            Write-Host "Deseja acessar o banco via Docker? (S/N)" -ForegroundColor Cyan
            $dockerOption = Read-Host
            if ($dockerOption -eq "S" -or $dockerOption -eq "s") {
                Write-Host "Acessando banco via Docker..." -ForegroundColor Yellow
                Execute-ViaDocker
                exit
            }
            
            exit 1
        }
    }
}

Write-Host "Usando banco de dados: $databasePath" -ForegroundColor Green

# 3. Verificar se temos sqlite3 disponivel
$sqliteAvailable = $false
if (Get-Command sqlite3 -ErrorAction SilentlyContinue) {
    $sqliteAvailable = $true
    Write-Host "SQLite3 encontrado no sistema" -ForegroundColor Green
} else {
    Write-Host "SQLite3 nao encontrado no PATH" -ForegroundColor Yellow
    Write-Host "Tentando usar modulo PSSQLite..." -ForegroundColor Yellow
}

# 4. Funcao para executar comandos SQL
function Execute-SqliteQuery {
    param(
        [string]$DatabasePath,
        [string]$Query
    )
    
    if ($sqliteAvailable) {
        # Usando sqlite3 CLI
        Write-Host "Executando SQL via sqlite3..." -ForegroundColor Gray
        $Query | & sqlite3 $DatabasePath
    } else {
        # Tentar usar modulo PSSQLite
        try {
            Import-Module PSSQLite -ErrorAction Stop
            Write-Host "Executando SQL via PSSQLite..." -ForegroundColor Gray
            Invoke-SqliteQuery -DataSource $DatabasePath -Query $Query
        } catch {
            Write-Host "Nao foi possivel executar query SQL" -ForegroundColor Red
            Write-Host "Tentando instalar SQLite automaticamente..." -ForegroundColor Yellow
            
            try {
                # Tentar instalar via Chocolatey
                if (Get-Command choco -ErrorAction SilentlyContinue) {
                    Write-Host "Instalando SQLite via Chocolatey..." -ForegroundColor Yellow
                    choco install sqlite -y
                    RefreshEnv
                    $sqliteAvailable = $true
                    $Query | & sqlite3 $DatabasePath
                } else {
                    Write-Host "Instale SQLite3 manualmente:" -ForegroundColor Yellow
                    Write-Host "   winget install SQLite.SQLite" -ForegroundColor Gray
                    Write-Host "   OU baixe de: https://sqlite.org/download.html" -ForegroundColor Gray
                    exit 1
                }
            } catch {
                Write-Host "Falha na instalacao automatica" -ForegroundColor Red
                exit 1
            }
        }
    }
}

# 5. Funcao para acessar via Docker
function Execute-ViaDocker {
    Write-Host "Acessando banco via Docker..." -ForegroundColor Yellow
    
    # Verificar se Docker está rodando
    try {
        docker ps | Out-Null
    } catch {
        Write-Host "Docker nao esta rodando. Inicie o Docker Desktop." -ForegroundColor Red
        exit 1
    }
    
    # Verificar se o container existe
    $containerExists = docker ps -a | Select-String "ailos-conta-corrente-api"
    if (-not $containerExists) {
        Write-Host "Container ailos-conta-corrente-api nao encontrado." -ForegroundColor Red
        Write-Host "Execute: docker-compose up -d" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "Executando comandos SQL no container..." -ForegroundColor Green
    
    # Comandos SQL para executar
    $sqlCommands = @"
-- Adicionar coluna role se nao existir
ALTER TABLE contacorrente 
ADD COLUMN role TEXT DEFAULT 'user';

-- Tornar conta ID 1 admin
UPDATE contacorrente 
SET role = 'admin', 
    nome = 'ADMIN USER',
    data_atualizacao = datetime('now')
WHERE idcontacorrente = 1;

-- Verificar resultado
SELECT 
    idcontacorrente as ID,
    numero as Numero,
    nome as Nome,
    role as Role
FROM contacorrente
WHERE idcontacorrente = 1;
"@

    # Executar via Docker
    $commands = $sqlCommands -split "`n"
    foreach ($cmd in $commands) {
        if ($cmd.Trim() -ne "" -and -not $cmd.Trim().StartsWith("--")) {
            Write-Host "Executando: $cmd" -ForegroundColor Gray
            docker exec ailos-conta-corrente-api sqlite3 /app/data/ailos.db "$cmd"
        }
    }
    
    Write-Host "Comandos executados via Docker com sucesso!" -ForegroundColor Green
    Write-Host "Reinicie o container para aplicar as mudancas:" -ForegroundColor Yellow
    Write-Host "   docker-compose restart conta-corrente-api" -ForegroundColor Gray
}

# 6. Verificar se quer executar via Docker ou local
Write-Host "" -ForegroundColor Cyan
Write-Host "Como deseja executar?" -ForegroundColor Yellow
Write-Host "   1. Localmente (no arquivo .db)" -ForegroundColor White
Write-Host "   2. Via Docker (no container)" -ForegroundColor White
Write-Host "" -ForegroundColor Cyan

$option = Read-Host "Digite 1 ou 2"

if ($option -eq "2") {
    Execute-ViaDocker
    exit
}

# 7. Executar localmente
Write-Host "Executando localmente..." -ForegroundColor Yellow

# Verificar estrutura atual da tabela
Write-Host "Analisando estrutura da tabela contacorrente..." -ForegroundColor Yellow

$tableInfo = Execute-SqliteQuery -DatabasePath $databasePath -Query @"
.schema contacorrente
"@

Write-Host "Estrutura atual:" -ForegroundColor Gray
Write-Host $tableInfo -ForegroundColor Gray

# Verificar se ja existe coluna 'role' ou 'admin'
$hasRoleColumn = $tableInfo -like "*role*" -or $tableInfo -like "*admin*"

if (-not $hasRoleColumn) {
    Write-Host "Adicionando coluna 'role' na tabela..." -ForegroundColor Yellow
    
    Execute-SqliteQuery -DatabasePath $databasePath -Query @"
ALTER TABLE contacorrente 
ADD COLUMN role TEXT DEFAULT 'user';
"@
    
    Write-Host "Coluna 'role' adicionada" -ForegroundColor Green
}

# Verificar conta com ID 1
Write-Host "Verificando conta com ID 1..." -ForegroundColor Yellow

$contaInfo = Execute-SqliteQuery -DatabasePath $databasePath -Query @"
SELECT 
    idcontacorrente as ID,
    cpf as CPF,
    numero as Numero,
    nome as Nome,
    ativo as Ativo,
    data_criacao as Criacao
FROM contacorrente 
WHERE idcontacorrente = 1;
"@

if ([string]::IsNullOrEmpty($contaInfo)) {
    Write-Host "Conta com ID 1 nao encontrada!" -ForegroundColor Red
    Write-Host "Criando conta admin padrao..." -ForegroundColor Yellow
    
    Execute-SqliteQuery -DatabasePath $databasePath -Query @"
INSERT INTO contacorrente (
    cpf, numero, nome, ativo, senha_hash, data_criacao, role
) VALUES (
    '11144477735',
    1,
    'ADMIN USER',
    1,
    '\$2a\$12\$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW', -- Hash de 'Admin@123'
    datetime('now'),
    'admin'
);
"@
    
    Write-Host "Conta admin criada com ID 1" -ForegroundColor Green
} else {
    Write-Host "Conta encontrada:" -ForegroundColor Green
    Write-Host $contaInfo -ForegroundColor Cyan
    
    # Atualizar conta para admin
    Write-Host "Atualizando conta para role 'admin'..." -ForegroundColor Yellow
    
    Execute-SqliteQuery -DatabasePath $databasePath -Query @"
UPDATE contacorrente 
SET role = 'admin', 
    nome = 'ADMIN USER',
    data_atualizacao = datetime('now')
WHERE idcontacorrente = 1;
"@
    
    Write-Host "Conta ID 1 agora e ADMIN" -ForegroundColor Green
}

# Verificar todas as contas
Write-Host "Listando todas as contas e suas roles:" -ForegroundColor Yellow

$todasContas = Execute-SqliteQuery -DatabasePath $databasePath -Query @"
SELECT 
    idcontacorrente as ID,
    numero as Numero,
    nome as Nome,
    CASE WHEN role = 'admin' THEN 'ADMIN' ELSE 'USER' END as Role,
    ativo as Ativo,
    data_criacao as Criacao
FROM contacorrente
ORDER BY idcontacorrente;
"@

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CONTAS NO SISTEMA:" -ForegroundColor Green
Write-Host $todasContas -ForegroundColor Gray

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SCRIPT CONCLUIDO!" -ForegroundColor Green
Write-Host "" -ForegroundColor Cyan
Write-Host "Reinicie o container para aplicar as mudancas:" -ForegroundColor Yellow
Write-Host "   docker-compose restart conta-corrente-api" -ForegroundColor Gray
Write-Host "" -ForegroundColor Cyan