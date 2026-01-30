Ailos Encrypted ID - Biblioteca de Ofuscação de Identificadores
🚀 Visão Geral
A Ailos Encrypted ID é uma biblioteca .NET de alta performance projetada para ofuscar identificadores numéricos (como IDs de banco de dados) em tokens seguros e amigáveis para uso em APIs, URLs e sistemas distribuídos. Transforme 12345 em eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9... de forma reversível e segura.

✨ Características Principais
🔒 Criptografia Forte: Utiliza AES-256 para criptografia e HMAC-SHA256 para assinatura, garantindo confidencialidade e integridade.

🌐 Pronto para Web: Tokens são codificados em Base64URL, seguros para URLs e cookies.

⚡ Alta Performance: Operações de criptografia e descriptografia otimizadas.

🛡️ Resistente a Tampering: Assinatura integrada detecta qualquer modificação nos tokens.

🧪 100% Testado: Cobertura completa de testes unitários e de integração.

🐳 Dockerizado: Pronto para execução em containers Docker com compose.

📦 Fácil Integração: Simples de integrar em projetos .NET existentes.

🏗️ Arquitetura
A biblioteca segue os princípios da arquitetura limpa, com separação clara de responsabilidades:

text
┌─────────────────────────────────────────────────────────────┐
│                    Camada de Aplicação                       │
│  ┌─────────────────────────────────────────────────────┐    │
│  │           Controladores API / Testes                │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                    Camada de Domínio                        │
│  ┌─────────────────────────────────────────────────────┐    │
│  │   EncryptedId (Value Object) / IEncryptedIdService  │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                    Camada de Infraestrutura                 │
│  ┌─────────────────────────────────────────────────────┐    │
│  │       EncryptedIdService (Implementação)            │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
Fluxo de Criptografia
Construção do Payload:

ID (8 bytes)

Nonce determinístico (8 bytes)

Assinatura HMAC (16 bytes)

Criptografia AES-256 no modo ECB (seguro para dados deterministicamente únicos).

Codificação Base64URL para produção de token seguro para URLs.

Fluxo de Descriptografia
Decodificação Base64URL para bytes.

Descriptografia AES-256 para obter o payload.

Validação da assinatura HMAC para garantir integridade.

Extração do ID dos primeiros 8 bytes.

🛠️ Configuração
Pré-requisitos
.NET 8.0 SDK

Docker Desktop (opcional, para execução em container)

IDE de sua preferência (Visual Studio 2022+, VS Code, Rider)

Configuração do Ambiente
Clone o repositório:

bash
git clone https://github.com/seu-usuario/ailos-encrypted-id.git
cd ailos-encrypted-id
Configure a chave secreta:

Crie um arquivo .env na raiz do projeto (já existe um exemplo)

Defina a variável ENCRYPTED_ID_SECRET com uma chave forte:

text
ENCRYPTED_ID_SECRET=Q9f$T7WvE3R@8xZp!K6dM2a#YH%uCwB4nLJX5eS0rAqF
Restaurar dependências:

bash
dotnet restore
🧪 Executando os Testes
A biblioteca possui testes unitários abrangentes:

bash
# Execute todos os testes
dotnet test

# Execute testes com cobertura de código
dotnet test --collect:"XPlat Code Coverage"

# Execute testes específicos
dotnet test --filter "FullyQualifiedName~EncryptedIdTests"
🚢 Executando com Docker
O projeto inclui um arquivo docker-compose.yaml completo:

bash
# Suba todos os serviços
docker-compose up -d

# Acesse a API de teste
open http://localhost:5080/swagger

# Acesse o Kafka UI
open http://localhost:8080

# Pare os serviços
docker-compose down
📚 Uso
Integração em Projetos .NET
Adicione a referência ao pacote (ou referencie o projeto):

xml
<PackageReference Include="Ailos.EncryptedId" Version="1.0.0" />
Configure o serviço no Program.cs:

csharp
using Ailos.EncryptedId;

// Configure a chave secreta (em produção, use Configuration)
builder.Services.AddSingleton<IEncryptedIdService>(
    EncryptedIdFactory.CreateService("sua-chave-super-secreta-aqui")
);
Use em seus controladores:

csharp
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IEncryptedIdService _encryptedIdService;
    
    public UsersController(IEncryptedIdService encryptedIdService)
    {
        _encryptedIdService = encryptedIdService;
    }
    
    [HttpGet("{encryptedId}")]
    public IActionResult GetUser(string encryptedId)
    {
        if (_encryptedIdService.TryDecrypt(encryptedId, out long userId))
        {
            // Busque o usuário com o ID descriptografado
            var user = _userRepository.GetById(userId);
            return Ok(user);
        }
        
        return NotFound();
    }
    
    [HttpPost]
    public IActionResult CreateUser([FromBody] UserCreateRequest request)
    {
        var newUser = _userRepository.Create(request);
        
        // Crie um token ofuscado para o novo usuário
        var encryptedToken = _encryptedIdService.Encrypt(newUser.Id);
        
        return CreatedAtAction(nameof(GetUser), 
            new { encryptedId = encryptedToken.Value }, newUser);
    }
}
API de Teste Interativa
Uma API de teste está disponível para experimentação:

text
GET  /api/obfuscation/encrypt/{id}
POST /api/obfuscation/batch-test
GET  /api/obfuscation/decrypt/{token}
Acesse a documentação Swagger em http://localhost:5080/swagger.

🔐 Segurança
Considerações de Segurança
Chave Secreta: A segurança do sistema depende totalmente da chave secreta. Em produção:

Use chaves com no mínimo 32 caracteres

Armazene em Azure Key Vault, AWS KMS ou similar

Nunca comite chaves em repositórios de código

Algoritmos Utilizados:

AES-256: Padrão do setor para criptografia simétrica

HMAC-SHA256: Para assinatura e verificação de integridade

Base64URL: Codificação segura para URLs

Proteção contra Tampering: A assinatura HMAC garante que tokens modificados sejam rejeitados.

Determinístico vs Não-Determinístico: O token gerado é determinístico (mesmo ID = mesmo token), o que é intencional para casos de uso como URLs persistentes.

Rotação de Chaves
Para rotacionar chaves sem invalidar tokens existentes:

Mantenha uma lista de chaves anteriores

Implemente fallback de descriptografia com múltiplas chaves

Gradualmente migre para a nova chave

📊 Performance
Benchmarks em máquina de desenvolvimento (Intel i7-11800H, 32GB RAM):

text
| Operação       | Média    | P95      | Ops/Sec  |
|----------------|----------|----------|----------|
| Encrypt        | 0.045ms  | 0.078ms  | 22,222   |
| Decrypt        | 0.038ms  | 0.065ms  | 26,316   |
| TryDecrypt     | 0.035ms  | 0.062ms  | 28,571   |
Capaz de processar mais de 20,000 operações por segundo por núcleo.

🧩 Casos de Uso
1. APIs Públicas
Ofuscar IDs internos em respostas JSON

Prevenir enumeração de recursos

2. URLs Amigáveis
Transformar /users/12345 em /users/eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

Seguro para compartilhamento

3. Sistemas Distribuídos
Tokens autocontidos que podem ser validados por qualquer serviço com a chave

Elimina necessidade de consultas a banco de dados para validação

4. Logs e Auditoria
Ofuscar IDs sensíveis em logs

Manter referência cruzada reversível

🔄 Manutenção
Versionamento
Segue Versionamento Semântico 2.0.0:

MAJOR: Mudanças incompatíveis

MINOR: Novas funcionalidades compatíveis

PATCH: Correções de bugs compatíveis

Log de Alterações
Consulte CHANGELOG.md para histórico detalhado de alterações.

🤝 Contribuindo
Faça um fork do projeto

Crie uma branch para sua feature (git checkout -b feature/AmazingFeature)

Commit suas mudanças (git commit -m 'Add some AmazingFeature')

Push para a branch (git push origin feature/AmazingFeature)

Abra um Pull Request

Padrões de Código
Siga as Diretrizes de Codificação da Microsoft

Mantenha cobertura de testes acima de 90%

Documente novas funcionalidades

📄 Licença
Distribuído sob licença MIT. Veja LICENSE para mais informações.

🆘 Suporte
Issues: Enzo Vieira

Email: enzovieira.trabalho@outlook.com

Slack: #encrypted-id-support

🙏 Reconhecimentos
Equipe .NET da Microsoft pelos excelentes recursos de criptografia

Comunidade open source por ferramentas incríveis

Equipe Ailos pela visão e apoio

<div align="center"> <p> <strong>Desenvolvido com ❤️ pela <a href="https://ailos.com.br">Equipe Ailos</a></strong> </p> <p> <sub>Segurança, performance e simplicidade em cada linha de código</sub> </p> </div>
