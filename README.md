# Ailos Conta Corrente API

API para gerenciamento de conta corrente com segurança avançada, idempotência e ofuscação de IDs.

## 🚀 Visão Geral

API RESTful para sistema bancário com funcionalidades completas de conta corrente, incluindo cadastro, login, movimentações (crédito/débito), consulta de saldo e inativação de contas. Desenvolvida em .NET 8 com arquitetura limpa e boas práticas de segurança.

## ✨ Funcionalidades Principais

### 🔐 Autenticação & Segurança
- **JWT Authentication**: Tokens com expiração configurável
- **Senhas Criptografadas**: Hash com BCrypt + salt único
- **CPF Validado**: Validação completa de dígitos verificadores
- **IDs Ofuscados**: Encrypted ID para proteção de identificadores internos

### 💳 Operações Bancárias
- **Cadastro de Conta**: Criação com CPF, nome e senha
- **Login Flexível**: Por CPF ou número da conta
- **Movimentações**: Crédito (C) e Débito (D) com validação de saldo
- **Consulta de Saldo**: Em tempo real com extrato implícito
- **Inativação de Conta**: Com validação de senha

### ⚡ Recursos Avançados
- **Idempotência**: Processamento seguro de requisições duplicadas
- **Validações de Domínio**: Regras de negócio aplicadas
- **Tratamento de Erros**: Respostas padronizadas com ProblemDetails
- **Health Checks**: Monitoramento de saúde da aplicação

## 🏗️ Arquitetura

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│  ┌─────────────────────────────────────────────────────┐    │
│  │   Controllers + Middleware + Filters + DTOs         │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                         │
│  ┌─────────────────────────────────────────────────────┐    │
│  │   Services + Command/Query + Application Logic      │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                    Domain Layer                             │
│  ┌─────────────────────────────────────────────────────┐    │
│  │   Entities + Value Objects + Domain Services        │    │
│  │   + Domain Exceptions + Business Rules              │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                    Infrastructure Layer                      │
│  ┌─────────────────────────────────────────────────────┐    │
│  │   Repositories + Security + Data Access + External  │    │
│  │   Services + Configuration                          │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

## 📋 Endpoints da API

### 🔓 Endpoints Públicos
- `POST /api/contacorrente/cadastrar` - Cadastro de nova conta
- `POST /api/contacorrente/login` - Autenticação de usuário

### 🔐 Endpoints Protegidos (Requirem JWT)
- `PUT /api/contacorrente/inativar` - Inativação de conta
- `GET /api/contacorrente/saldo` - Consulta de saldo
- `POST /api/movimentacao` - Realizar movimentação (crédito/débito)

### 🛠️ Endpoints Administrativos
- `GET /api/admin/idempotencia/{chave}` - Consulta de idempotência
- `GET /api/admin/idempotencia/verificar/{chave}` - Verificação de processamento
- `DELETE /api/admin/idempotencia/{chave}` - Remoção de registro

## 🛠️ Tecnologias Utilizadas

- **.NET 8** - Framework principal
- **SQLite** - Banco de dados leve
- **Dapper** - Micro ORM para acesso a dados
- **JWT Bearer** - Autenticação por tokens
- **BCrypt.Net** - Criptografia de senhas
- **Swagger/OpenAPI** - Documentação interativa
- **Docker** - Containerização
- **FluentValidation** - Validação de dados
- **System.Text.Json** - Serialização JSON

## 🚀 Começando

### Pré-requisitos
- .NET 8.0 SDK
- Docker (opcional, para containerização)
- IDE (Visual Studio 2022+, VS Code, ou Rider)

### Configuração do Ambiente

1. **Clone o repositório**
```bash
git clone https://github.com/seu-usuario/ailos-conta-corrente.git
cd ailos-conta-corrente
```

2. **Configure as variáveis de ambiente**
Crie um arquivo `.env` na raiz (baseado no `.env.example`):
```env
ENCRYPTED_ID_SECRET=sua-chave-secreta-aqui
JwtSettings__Secret=super-secret-jwt-key-2024!
JwtSettings__Issuer=AilosContaCorrenteApi
JwtSettings__Audience=AilosClient
JwtSettings__ExpirationMinutes=60
ConnectionStrings__DefaultConnection=Data Source=ailos.db
```

3. **Restaure as dependências**
```bash
dotnet restore
```

4. **Execute a aplicação**
```bash
cd src/Ailos.ContaCorrente.Api
dotnet run
```

A API estará disponível em: `https://localhost:5001` (ou `http://localhost:5000`)

## 🐳 Executando com Docker

```bash
# Construir e executar os containers
docker-compose up -d

# Acessar a API
# Swagger: http://localhost:5081/swagger
# Health Check: http://localhost:5081/health

# Parar os containers
docker-compose down
```

## 📊 Banco de Dados

### Estrutura das Tabelas

#### `contacorrente`
```sql
CREATE TABLE contacorrente (
    idcontacorrente INTEGER PRIMARY KEY AUTOINCREMENT,
    cpf TEXT NOT NULL UNIQUE,
    numero INTEGER NOT NULL UNIQUE,
    nome TEXT NOT NULL,
    ativo INTEGER NOT NULL DEFAULT 1,
    senha_hash TEXT NOT NULL,
    salt TEXT NOT NULL,
    data_criacao TEXT NOT NULL DEFAULT (datetime('now')),
    data_atualizacao TEXT,
    CHECK (ativo IN (0, 1))
);
```

#### `movimento`
```sql
CREATE TABLE movimento (
    idmovimento INTEGER PRIMARY KEY AUTOINCREMENT,
    idcontacorrente INTEGER NOT NULL,
    datamovimento TEXT NOT NULL DEFAULT (datetime('now')),
    tipomovimento TEXT NOT NULL,
    valor REAL NOT NULL,
    descricao TEXT,
    CHECK (tipomovimento IN ('C', 'D')),
    FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente) ON DELETE CASCADE
);
```

#### `idempotencia`
```sql
CREATE TABLE idempotencia (
    chave_idempotencia TEXT PRIMARY KEY,
    requisicao TEXT,
    resultado TEXT,
    data_criacao TEXT NOT NULL DEFAULT (datetime('now'))
);
```

## 🔐 Segurança

### Ofuscação de IDs
- **Encrypted ID**: IDs internos são ofuscados usando criptografia AES-256
- **Proteção**: Evita enumeração e exposição de IDs sequenciais
- **Reversibilidade**: Ofuscação reversível apenas com a chave secreta

### Autenticação
- **JWT Tokens**: Validação automática com middleware ASP.NET Core
- **Claims**: `contaId` e `numeroConta` incluídos no token
- **Expiração**: Configurável via `JwtSettings__ExpirationMinutes`

### Validações
- **CPF**: Validação completa dos dígitos verificadores
- **Senha**: Mínimo de 6 caracteres, hash BCrypt
- **Saldo**: Verificação prévia para débitos
- **Ativo**: Apenas contas ativas podem operar

## ⚡ Idempotência

### Como Funciona
1. **Chave de Idempotência**: Enviada no header `X-Idempotency-Key`
2. **Verificação**: Middleware verifica se requisição já foi processada
3. **Cache**: Resultados armazenados no banco de dados
4. **Retorno**: Respostas idênticas para requisições duplicadas

### Implementação
```csharp
// Middleware verifica idempotência
public class IdempotenciaMiddleware
{
    public async Task InvokeAsync(HttpContext context, IIdempotenciaService service)
    {
        if (ShouldCheckIdempotency(context))
        {
            var key = GetIdempotencyKey(context);
            if (await service.RequisicaoJaProcessadaAsync(key))
            {
                // Retorna resultado cacheado
                await ReturnCachedResult(context, await service.ObterResultadoAsync(key));
                return;
            }
        }
        await _next(context);
    }
}
```

## 📝 Exemplos de Uso

### 1. Cadastro de Conta
```http
POST /api/contacorrente/cadastrar
Content-Type: application/json

{
  "cpf": "12345678909",
  "nome": "João Silva",
  "senha": "senha123"
}
```

### 2. Login
```http
POST /api/contacorrente/login
Content-Type: application/json

{
  "cpf": "12345678909",
  "senha": "senha123"
}
```

### 3. Movimentação com Idempotência
```http
POST /api/movimentacao
Authorization: Bearer {token}
X-Idempotency-Key: unique-request-id-123
Content-Type: application/json

{
  "identificacaoRequisicao": "unique-request-id-123",
  "valor": 100.50,
  "tipoMovimento": "C",
  "descricao": "Depósito inicial"
}
```

### 4. Consulta de Saldo
```http
GET /api/contacorrente/saldo
Authorization: Bearer {token}
```

## 🧪 Testes

```bash
# Executar testes unitários
dotnet test

# Testes com cobertura
dotnet test --collect:"XPlat Code Coverage"

# Testes específicos
dotnet test --filter "FullyQualifiedName~ContaCorrenteTests"
```

## 🚢 Deploy

### Docker
```bash
# Build da imagem
docker build -t ailos-conta-corrente:latest .

# Executar container
docker run -d \
  -p 8080:80 \
  -e ENCRYPTED_ID_SECRET=${ENCRYPTED_ID_SECRET} \
  -e JwtSettings__Secret=${JWT_SECRET} \
  -v /path/to/data:/app/data \
  ailos-conta-corrente:latest
```

### Kubernetes (Exemplo)
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ailos-conta-corrente
spec:
  replicas: 3
  selector:
    matchLabels:
      app: conta-corrente
  template:
    metadata:
      labels:
        app: conta-corrente
    spec:
      containers:
      - name: api
        image: ailos-conta-corrente:latest
        ports:
        - containerPort: 80
        env:
        - name: ENCRYPTED_ID_SECRET
          valueFrom:
            secretKeyRef:
              name: app-secrets
              key: encrypted-id-secret
```

## 📈 Monitoramento

### Health Checks
```http
GET /health
```

### Logs
- Structured logging com Serilog (configurável)
- Níveis: Information, Warning, Error
- Integração com sistemas de monitoramento

### Métricas
- Request/response times
- Error rates
- Database connection health
- Memory usage

## 🤝 Contribuindo

1. Fork o projeto
2. Crie uma feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit suas mudanças (`git commit -m 'Add some AmazingFeature'`)
4. Push para a branch (`git push origin feature/AmazingFeature`)
5. Abra um Pull Request

## 📄 Licença

Distribuído sob licença MIT. Veja `LICENSE` para mais informações.

## 🆘 Suporte

- **Issues**: [GitHub Issues](https://github.com/seu-usuario/ailos-conta-corrente/issues)
- **Email**: enzovieira.trabalho@outlook.com
- **Documentação**: [Swagger UI](http://localhost:5081/swagger)

---

<div align="center">
  <p><strong>Desenvolvido com ❤️ pela Equipe Ailos</strong></p>
  <p><sub>Soluções bancárias seguras, escaláveis e de alta performance</sub></p>
</div>