-- Tabela de tarifas (para a API Conta Corrente)
CREATE TABLE IF NOT EXISTS tarifa (
    idtarifa INTEGER PRIMARY KEY AUTOINCREMENT,
    idcontacorrente INTEGER NOT NULL,
    idtransferencia INTEGER,
    datamovimento TEXT NOT NULL DEFAULT (datetime('now')),
    valor REAL NOT NULL,
    processada INTEGER NOT NULL DEFAULT 0,
    mensagem_erro TEXT,
    data_processamento TEXT,
    CHECK (processada IN (0, 1)),
    FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente) ON DELETE CASCADE,
    FOREIGN KEY(idtransferencia) REFERENCES transferencia(idtransferencia) ON DELETE SET NULL
);

-- Índices para performance
CREATE INDEX IF NOT EXISTS idx_tarifa_conta ON tarifa(idcontacorrente);
CREATE INDEX IF NOT EXISTS idx_tarifa_transferencia ON tarifa(idtransferencia);
CREATE INDEX IF NOT EXISTS idx_tarifa_data ON tarifa(datamovimento);
CREATE INDEX IF NOT EXISTS idx_tarifa_processada ON tarifa(processada);

-- Tabela de histórico de tarifas processadas (para o Worker)
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

-- Índices para o histórico
CREATE INDEX IF NOT EXISTS idx_historico_transferencia ON tarifa_processada(transferencia_id);
CREATE INDEX IF NOT EXISTS idx_historico_conta ON tarifa_processada(conta_origem_id);
CREATE INDEX IF NOT EXISTS idx_historico_data ON tarifa_processada(data_processamento);
