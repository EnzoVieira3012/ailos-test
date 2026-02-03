-- Banco principal (usado por todas APIs)
CREATE TABLE IF NOT EXISTS contacorrente (
    idcontacorrente INTEGER PRIMARY KEY AUTOINCREMENT,
    cpf TEXT NOT NULL UNIQUE,
    numero INTEGER NOT NULL UNIQUE,
    nome TEXT NOT NULL,
    ativo INTEGER NOT NULL DEFAULT 1,
    senha_hash TEXT NOT NULL,
    data_criacao TEXT NOT NULL DEFAULT (datetime('now')),
    data_atualizacao TEXT,
    role TEXT DEFAULT 'conta-corrente',  -- COLUNA ROLE ADICIONADA
    CHECK (ativo IN (0, 1))
);

CREATE TABLE IF NOT EXISTS movimento (
    idmovimento INTEGER PRIMARY KEY AUTOINCREMENT,
    idcontacorrente INTEGER NOT NULL,
    datamovimento TEXT NOT NULL DEFAULT (datetime('now')),
    tipomovimento TEXT NOT NULL,
    valor REAL NOT NULL,
    descricao TEXT,
    identificacao_requisicao TEXT,
    CHECK (tipomovimento IN ('C', 'D')),
    FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
);

CREATE TABLE IF NOT EXISTS idempotencia (
    chave_idempotencia TEXT PRIMARY KEY,
    requisicao TEXT,
    resultado TEXT,
    data_criacao TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Índices
CREATE INDEX IF NOT EXISTS idx_conta_cpf ON contacorrente(cpf);
CREATE INDEX IF NOT EXISTS idx_conta_numero ON contacorrente(numero);
CREATE INDEX IF NOT EXISTS idx_movimento_conta ON movimento(idcontacorrente);
CREATE INDEX IF NOT EXISTS idx_movimento_data ON movimento(datamovimento);
CREATE INDEX IF NOT EXISTS idx_idempotencia_chave ON idempotencia(chave_idempotencia);
CREATE INDEX IF NOT EXISTS idx_idempotencia_data ON idempotencia(data_criacao);