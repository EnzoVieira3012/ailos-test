-- Tabela de transferências
CREATE TABLE IF NOT EXISTS transferencia (
    idtransferencia INTEGER PRIMARY KEY AUTOINCREMENT,
    idcontacorrente_origem INTEGER NOT NULL,
    idcontacorrente_destino INTEGER NOT NULL,
    datamovimento TEXT NOT NULL DEFAULT (datetime('now')),
    valor REAL NOT NULL,
    tarifa_aplicada REAL DEFAULT 0.00,
    status TEXT NOT NULL DEFAULT 'PROCESSANDO',
    mensagem_erro TEXT,
    identificacao_requisicao TEXT UNIQUE,
    CHECK (status IN ('PROCESSANDO', 'CONCLUIDA', 'FALHA', 'ESTORNADA'))
);

-- Tabela de idempotência
CREATE TABLE IF NOT EXISTS idempotencia (
    chave_idempotencia TEXT PRIMARY KEY,
    requisicao TEXT,
    resultado TEXT,
    data_criacao TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Índices para performance
CREATE INDEX IF NOT EXISTS idx_transferencia_origem ON transferencia(idcontacorrente_origem);
CREATE INDEX IF NOT EXISTS idx_transferencia_destino ON transferencia(idcontacorrente_destino);
CREATE INDEX IF NOT EXISTS idx_transferencia_data ON transferencia(datamovimento);
CREATE INDEX IF NOT EXISTS idx_transferencia_status ON transferencia(status);
CREATE INDEX IF NOT EXISTS idx_transferencia_identificacao ON transferencia(identificacao_requisicao);
CREATE INDEX IF NOT EXISTS idx_idempotencia_chave ON idempotencia(chave_idempotencia);
CREATE INDEX IF NOT EXISTS idx_idempotencia_data ON idempotencia(data_criacao);