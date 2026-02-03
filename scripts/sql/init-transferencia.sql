-- Tabela principal de transferências
CREATE TABLE IF NOT EXISTS transferencia (
    idtransferencia INTEGER PRIMARY KEY AUTOINCREMENT,
    idcontacorrente_origem INTEGER NOT NULL,
    idcontacorrente_destino INTEGER NOT NULL,
    datamovimento TEXT NOT NULL DEFAULT (datetime('now')),
    valor REAL NOT NULL,
    tarifa_aplicada REAL,
    status TEXT NOT NULL DEFAULT 'PROCESSANDO',
    mensagem_erro TEXT,
    identificacao_requisicao TEXT UNIQUE,
    CHECK (status IN ('PROCESSANDO', 'CONCLUIDA', 'FALHA', 'ESTORNADA'))
);

-- Tabela de idempotência (específica para transferência)
CREATE TABLE IF NOT EXISTS idempotencia_transferencia (
    chave_idempotencia TEXT PRIMARY KEY,
    requisicao TEXT,
    resultado TEXT,
    data_criacao TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Tabela de tarifas
CREATE TABLE IF NOT EXISTS tarifa (
    idtarifa INTEGER PRIMARY KEY AUTOINCREMENT,
    idcontacorrente INTEGER NOT NULL,
    idtransferencia INTEGER,
    datamovimento TEXT NOT NULL DEFAULT (datetime('now')),
    valor REAL NOT NULL,
    processada INTEGER NOT NULL DEFAULT 0,
    mensagem_erro TEXT,
    data_processamento TEXT,
    CHECK (processada IN (0, 1))
);

-- Tabela de histórico de tarifas processadas
CREATE TABLE IF NOT EXISTS tarifa_processada (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    transferencia_id INTEGER NOT NULL,
    conta_origem_id INTEGER NOT NULL,
    valor_tarifa REAL NOT NULL,
    data_processamento TEXT NOT NULL DEFAULT (datetime('now')),
    status TEXT NOT NULL,
    mensagem TEXT,
    topico_kafka TEXT NOT NULL,
    offset_kafka INTEGER NOT NULL,
    UNIQUE(transferencia_id, topico_kafka, offset_kafka)
);

-- Índices para performance
CREATE INDEX IF NOT EXISTS idx_transferencia_origem ON transferencia(idcontacorrente_origem);
CREATE INDEX IF NOT EXISTS idx_transferencia_destino ON transferencia(idcontacorrente_destino);
CREATE INDEX IF NOT EXISTS idx_transferencia_data ON transferencia(datamovimento);
CREATE INDEX IF NOT EXISTS idx_transferencia_status ON transferencia(status);
CREATE INDEX IF NOT EXISTS idx_transferencia_requisicao ON transferencia(identificacao_requisicao);

CREATE INDEX IF NOT EXISTS idx_idempotencia_transf_chave ON idempotencia_transferencia(chave_idempotencia);

CREATE INDEX IF NOT EXISTS idx_tarifa_conta ON tarifa(idcontacorrente);
CREATE INDEX IF NOT EXISTS idx_tarifa_transferencia ON tarifa(idtransferencia);
CREATE INDEX IF NOT EXISTS idx_tarifa_data ON tarifa(datamovimento);
CREATE INDEX IF NOT EXISTS idx_tarifa_processada ON tarifa(processada);

CREATE INDEX IF NOT EXISTS idx_historico_transferencia ON tarifa_processada(transferencia_id);
CREATE INDEX IF NOT EXISTS idx_historico_conta ON tarifa_processada(conta_origem_id);
CREATE INDEX IF NOT EXISTS idx_historico_data ON tarifa_processada(data_processamento);
CREATE INDEX IF NOT EXISTS idx_historico_status ON tarifa_processada(status);