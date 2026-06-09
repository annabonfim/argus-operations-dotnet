<p align="center">
  <img src="Argus.Operations.API/wwwroot/images/argus_symbol_only_transparent.png" alt="Argus" width="180">
</p>

# Argus Operations API

<p align="center">
  <img alt=".NET 9" src="https://img.shields.io/badge/.NET%209-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">
  <img alt="C#" src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white">
  <img alt="ASP.NET Core" src="https://img.shields.io/badge/ASP.NET%20Core-Web%20API-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">
  <img alt="Entity Framework Core" src="https://img.shields.io/badge/EF%20Core%209-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">
  <img alt="Oracle" src="https://img.shields.io/badge/Oracle%20DB-F80000?style=for-the-badge&logo=oracle&logoColor=white">
  <img alt="JWT" src="https://img.shields.io/badge/JWT-Bearer-000000?style=for-the-badge&logo=jsonwebtokens&logoColor=white">
  <img alt="Swagger" src="https://img.shields.io/badge/Swagger-OpenAPI-85EA2D?style=for-the-badge&logo=swagger&logoColor=black">
  <img alt="Serilog" src="https://img.shields.io/badge/Serilog-Structured%20Logs-1C1C1C?style=for-the-badge">
  <img alt="xUnit" src="https://img.shields.io/badge/xUnit-38%2F38%20%E2%9C%93-5C2D91?style=for-the-badge">
  <img alt="Azure" src="https://img.shields.io/badge/Azure-App%20Service-0078D4?style=for-the-badge&logo=microsoftazure&logoColor=white">
</p>

API operacional do sistema **Argus**, voltada a operações de combate a incêndios florestais. Concentra a gestão de brigadas, brigadistas, recursos materiais, ocorrências em campo e registros de atendimento, expondo um conjunto de endpoints REST protegidos por autenticação JWT.

O projeto faz parte da Global Solution 2026/1 da FIAP, cujo tema é **economia espacial**. Entre as direções sugeridas pela banca, escolhemos aplicar dados de satélite a um problema real em terra: o combate a incêndios florestais. O Argus se posiciona no eixo de **monitoramento ambiental e resposta a desastres** — a API operacional aqui presente é o backend que recebe os alertas de queimadas detectados por satélite (vindos do domínio Java, externo a esta API) e coordena a resposta das brigadas em campo.

## Vídeos da apresentação

- 🎤 **Pitch (3 min):** https://youtu.be/TGzXcXq6UF4
- 🎬 **Demo completa (8 min):** https://youtu.be/a-449ypErKE

## Sumário

- [Vídeos da apresentação](#vídeos-da-apresentação)
- [Stack](#stack)
- [Arquitetura](#arquitetura)
- [Modelo de domínio](#modelo-de-domínio)
- [Como rodar localmente](#como-rodar-localmente)
- [Autenticação e autorização](#autenticação-e-autorização)
- [Matriz de permissões](#matriz-de-permissões)
- [Endpoints](#endpoints)
- [Como testar no Swagger](#como-testar-no-swagger)
- [Tratamento global de erros](#tratamento-global-de-erros)
- [Health check](#health-check)
- [Integração com a API Java](#integração-com-a-api-java)
- [Mensageria assíncrona (RabbitMQ)](#mensageria-assíncrona-rabbitmq)
- [Landing page institucional](#landing-page-institucional)
- [Testes automatizados](#testes-automatizados)
- [Estrutura de pastas](#estrutura-de-pastas)
- [Decisões técnicas relevantes](#decisões-técnicas-relevantes)
- [Deploy na Azure](#deploy-na-azure)
- [Evidências em produção](#evidências-em-produção)
- [Integrantes](#integrantes)

## Stack

| Categoria | Tecnologia |
|---|---|
| Runtime | .NET 9 |
| Web | ASP.NET Core 9 (Controllers + Swagger UI via Swashbuckle 6.9) |
| Persistência | Entity Framework Core 9 com provider Oracle (Oracle.EntityFrameworkCore 9.x) |
| Banco | Oracle 19c (servidor FIAP) |
| Autenticação | JWT Bearer + BCrypt para hash de senhas |
| Logging | Serilog (estruturado, JSON-friendly) + `UseSerilogRequestLogging` |
| Mensageria | RabbitMQ.Client 7.x consumindo broker AMQP gerenciado (CloudAMQP/LavinMQ) |
| Front estático | `wwwroot/index.html` servido em `/` com identidade visual compartilhada Java + .NET |
| Testes | xUnit 2.9 + EF Core InMemory |

## Arquitetura

A solução segue o padrão Clean Architecture com quatro projetos. As dependências apontam sempre em direção ao domínio: a API conhece todas as camadas, mas o domínio não conhece ninguém. Isso permite que regras de negócio fiquem isoladas de detalhes de infraestrutura (qual ORM, qual provider, qual mecanismo de hash).

```mermaid
graph TD
    API["Argus.Operations.API<br/>Controllers, DTOs, Swagger, JWT middleware"]
    APP["Argus.Operations.Application<br/>Interfaces de contratos (ITokenService, IPasswordHasher)"]
    INF["Argus.Operations.Infrastructure<br/>ArgusDbContext, TokenService, BcryptPasswordHasher, Seeder"]
    DOM["Argus.Operations.Domain<br/>Entidades e enums"]

    API --> APP
    API --> INF
    API --> DOM
    INF --> APP
    INF --> DOM
    APP --> DOM
```

Os contratos de autenticação ficam na Application (`ITokenService`, `IPasswordHasher`); a implementação concreta (`TokenService`, `BcryptPasswordHasher`) está na Infrastructure. O AuthController depende das interfaces, não da implementação — substituir BCrypt por Argon2 amanhã exigiria uma classe nova na Infrastructure e zero linha alterada no controller.

## Modelo de domínio

```mermaid
erDiagram
    BRIGADA ||--o{ BRIGADISTA : "lotada em"
    BRIGADA ||--o{ RECURSO : "possui"
    BRIGADA ||--o{ OCORRENCIA : "atende"
    BRIGADISTA ||--o{ OCORRENCIA : "responsavel por"
    OCORRENCIA ||--o{ REGISTRO_CAMPO : "gera"
    USUARIO |o--o| BRIGADISTA : "vincula (opcional)"

    BRIGADA {
        long ID_BRIGADA PK
        string NOME
        string BASE_OPERACIONAL
        string TELEFONE
        bool ATIVA
    }
    BRIGADISTA {
        long ID_BRIGADISTA PK
        string NOME
        string MATRICULA
        string EMAIL
        string TELEFONE
        string FUNCAO
        bool ATIVO
        date DATA_ADMISSAO
        long ID_BRIGADA FK
    }
    RECURSO {
        long ID_RECURSO PK
        string NOME
        int TIPO
        bool DISPONIVEL
        long ID_BRIGADA FK
    }
    OCORRENCIA {
        long ID_OCORRENCIA PK
        string DESCRICAO
        double LATITUDE
        double LONGITUDE
        int STATUS
        date DATA_ABERTURA
        date DATA_FINALIZACAO
        long ID_BRIGADISTA FK
        long ID_BRIGADA FK
        long ID_ALERTA "FK cross-domain Java"
    }
    REGISTRO_CAMPO {
        long ID_REGISTRO_CAMPO PK
        string OBSERVACAO
        string URL_FOTO
        double LATITUDE
        double LONGITUDE
        date DATA_REGISTRO
        long ID_OCORRENCIA FK
    }
    USUARIO {
        long ID_USUARIO PK
        string NOME
        string EMAIL "unique"
        string SENHA_HASH
        int PERFIL
        bool ATIVO
        date DATA_CRIACAO
        date ULTIMO_LOGIN
        long ID_BRIGADISTA "FK opcional"
    }
```

A entidade `Usuario` representa quem opera o sistema (admin, coordenador ou brigadista). Carrega uma FK **opcional** `ID_BRIGADISTA` que vincula o usuário à entidade operacional Brigadista correspondente — quando preenchida, identifica "qual membro da brigada esse login representa". Nullable porque Admin/Coordenador podem não atuar em campo, e brigadistas voluntários podem se auto-cadastrar antes de serem oficialmente alocados a uma brigada. A vinculação acontece automaticamente no auto-cadastro quando o email bate (padrão SSO — ver [Decisões técnicas](#decisões-técnicas-relevantes)) ou manualmente via `PUT /api/usuarios/{id}`.

O `ID_ALERTA` em `OCORRENCIA` é uma referência cross-domain ao módulo Java do projeto (não está no escopo deste repositório); por isso é nullable e a FK não é criada via EF Core. Como cada microserviço (Java e .NET) opera contra um schema Oracle FIAP **separado** — decisão tomada para evitar a competição pelo limite de `SESSIONS_PER_USER = 10` no servidor da FIAP —, essa referência é puramente lógica: o `AlertaId` carrega o identificador do alerta do lado do Java sem qualquer constraint física entre os schemas. A integração entre os dois lados acontece via HTTP (proxy) e mensageria assíncrona, não via banco.

Os enums `PerfilUsuario`, `TipoRecurso` e `StatusOcorrencia` são mapeados como `int` no Oracle (via `HasConversion<int>()`), o que permite filtros e relatórios sem precisar de joins com tabelas de domínio.

## Como rodar localmente

### Pré-requisitos

- .NET 9 SDK
- Acesso ao banco Oracle do FIAP (ou outro Oracle 11+ com `UseOracleSQLCompatibility(DatabaseVersion19)`)
- Opcional: `jq` para formatar respostas de cURL no terminal

### Connection string

A connection string do Oracle **não** está versionada. Configure via user-secrets:

```bash
cd Argus.Operations.API
dotnet user-secrets init    # se ainda não inicializado
dotnet user-secrets set "ConnectionStrings:OracleDb" \
  "User Id=SEU_RM;Password=SUA_SENHA;Data Source=oracle.fiap.com.br:1521/ORCL"
```

Alternativamente, defina a variável de ambiente `ConnectionStrings__OracleDb`.

### Migrations

As migrations já estão versionadas em `Argus.Operations.Infrastructure/Migrations/`. Aplique-as no banco com:

```bash
dotnet ef database update --project Argus.Operations.Infrastructure --startup-project Argus.Operations.API
```

Se preferir criar o schema diretamente via SQL (sem usar EF Core para gerenciar versões), o arquivo `argus-tabelas-dotnet.sql` na raiz contém o DDL consolidado.

### Rodando a API

```bash
dotnet run --project Argus.Operations.API
```

A API sobe em `http://localhost:5215`. A documentação Swagger fica em `http://localhost:5215/swagger`.

No primeiro startup, o `UsuariosSeeder` cria automaticamente o usuário administrador (idempotente — startups subsequentes verificam por email antes de inserir).

## Autenticação e autorização

A API usa JWT Bearer. O fluxo padrão é: o cliente faz POST em `/api/auth/login` com email/senha, recebe um token, e envia esse token no header `Authorization: Bearer <token>` em todas as requisições subsequentes.

### Credenciais de teste

| Email | Senha | Perfil | Como foi criado |
|---|---|---|---|
| `admin@argus.com` | `Admin@123` | Admin | Seed automático no startup |
| `brig@argus.com` | `Brig@123` | Brigadista | Registrado via `POST /api/auth/register` durante o desenvolvimento |

Os usuários persistem no Oracle entre restarts da API. Para resetar, basta deletar as linhas correspondentes em `USUARIO`. Para criar um novo usuário pelo `POST /api/auth/register`, veja [Como testar no Swagger](#como-testar-no-swagger) — usa o código de convite `ARGUS-2026`.

### Login pelo Swagger

1. Abra `http://localhost:5215/swagger`.
2. Em **POST `/api/auth/login`**, clique em "Try it out" e cole:
   ```json
   { "email": "admin@argus.com", "senha": "Admin@123" }
   ```
3. Copie o valor do campo `token` da resposta.
4. Clique no botão **Authorize** no topo da página, cole `Bearer <token>` (com o prefixo `Bearer`), confirme e feche o modal.
5. Todos os endpoints protegidos agora vão receber o header automaticamente.

Para trocar de usuário durante os testes: Authorize > Logout > cola o novo token.

### Endpoint de inspeção

O endpoint `GET /api/auth/me` devolve as claims do token atual e indica em quais roles o usuário está. É útil tanto para debug quanto para o consumo pelo aplicativo mobile.

### Registro de novos usuários

`POST /api/auth/register` é público e cria sempre um **Brigadista** (para criar Coordenadores ou Admins é preciso editar o `AdminSeed` no `appsettings.json` ou inserir direto no banco). Exige um campo `codigoConvite` configurado em `Auth:CodigoConvite` — exemplo curl pronto em [Como testar no Swagger](#como-testar-no-swagger).

Campos do payload:

| Campo | Obrigatório | Restrição | Descrição |
|---|---|---|---|
| `nome` | sim | máx 150 | Nome completo |
| `email` | sim | formato e-mail, máx 150, único | Identidade de login |
| `telefone` | sim | máx 20 | Contato principal |
| `nomeEmergencia` | não | máx 100 | Nome da pessoa a acionar em emergência |
| `telefoneEmergencia` | não | máx 20 | Telefone da pessoa a acionar em emergência |
| `relacaoEmergencia` | não | máx 30 | Relação com o brigadista (ex.: "Mãe", "Cônjuge", "Irmão") |
| `senha` | sim | mín 6 caracteres | É hasheada com BCrypt (workfactor 11) antes de gravar |
| `codigoConvite` | sim | igual ao configurado | Verificação de inscrição autorizada |

Os 3 campos do contato de emergência andam juntos por convenção (todos preenchidos ou nenhum). A validação dessa coesão fica a cargo do cliente — o mobile mostra/esconde o trio como bloco único.

## Matriz de permissões

| Endpoint | Admin | Coordenador | Brigadista |
|---|:-:|:-:|:-:|
| CRUD `/api/usuarios` | sim | nao | nao |
| GET `/api/brigadas`, `/brigadistas`, `/recursos` | sim | sim | sim |
| POST/PUT/DELETE em `/brigadas`, `/brigadistas`, `/recursos` | sim | sim | nao |
| GET `/api/ocorrencias` | sim | sim | sim |
| PUT `/api/ocorrencias` (atualizar status em campo) | sim | sim | sim |
| POST/DELETE `/api/ocorrencias` | sim | sim | nao |
| GET `/api/registroscampo` | sim | sim | sim |
| POST/PUT/DELETE `/api/registroscampo` | sim | sim | só na própria brigada¹ |

¹ **Brigadista só pode escrever (criar/editar/deletar) registros de ocorrências da própria brigada** — `Ocorrencia.BrigadaId` precisa bater com a brigada do `Brigadista` vinculado ao `Usuario` logado (`Usuario.BrigadistaId → Brigadista.BrigadaId`). Se o Usuario brigadista não tiver vínculo (`BrigadistaId == null`), também recebe 403 nas escritas — fica como "voluntário pré-vinculação". Admin/Coordenador escrevem em qualquer brigada.

A restrição é aplicada em duas camadas. No backend, atributos `[Authorize(Roles = "...")]` controlam o acesso a cada endpoint, e a constante única `Roles.AdminECoordenador` evita strings mágicas espalhadas pelo código. No JWT, o claim `role` carrega o perfil do usuário, lido a partir do enum `PerfilUsuario.ToString()`. A regra granular por brigada em `/api/registroscampo` é aplicada **em código** (checagem programática contra o banco no `RegistrosCampoController`), porque atributos `[Authorize]` não cobrem regras que dependem de dados — coberto por testes que incluem caso de bypass (brigadista tentando trocar `OcorrenciaId` no body pra forjar acesso).

## Endpoints

### Autenticação (`/api/auth`)

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/login` | público | autentica e devolve JWT |
| POST | `/register` | público (exige `codigoConvite`) | cria novo Brigadista |
| GET | `/me` | qualquer logado | devolve claims e roles do token atual |
| PUT | `/me` | qualquer logado | usuário edita o próprio perfil (nome, telefone, contato de emergência) — nunca toca em `Perfil`, `Ativo`, `Email`, `SenhaHash` |

### Integração com Java

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/alertas` | qualquer logado | proxy pra API Java; lista alertas críticos filtrados pelo trigger PL/SQL (vindos da NASA FIRMS) |
| GET | `/api/alertas/{id}` | qualquer logado | proxy pra API Java; devolve um alerta específico |
| POST | `/api/alertas/{id}/criar-ocorrencia` | Admin/Coordenador | "promove" o alerta a uma ocorrência operacional — busca o alerta no Java, monta a descrição a partir do título + descrição + recomendação operacional, e cria a ocorrência atribuída à brigada/brigadista informados |
| GET | `/api/focos` | qualquer logado | proxy pra API Java; lista focos de calor brutos (NASA FIRMS) usados pelo mapa do mobile |

### Recursos do domínio

Todos os controllers seguem o padrão CRUD:

| Método | Rota | Função |
|---|---|---|
| GET | `/api/{recurso}` | lista todos |
| GET | `/api/{recurso}/{id}` | busca por id (404 se não existir) |
| POST | `/api/{recurso}` | cria (devolve 201 com `Location` para o `GET /{id}`) |
| PUT | `/api/{recurso}/{id}` | atualiza (204 No Content; 400 se id da URL ≠ id do corpo) |
| DELETE | `/api/{recurso}/{id}` | remove (204 No Content) |

Onde `{recurso}` é um de: `brigadas`, `brigadistas`, `recursos`, `ocorrencias`, `registroscampo`, `usuarios`.

## Como testar no Swagger

A API entrega a documentação interativa em `http://localhost:5215/swagger`. Os passos abaixo cobrem o fluxo típico de avaliação — autenticar uma vez, depois exercitar cada endpoint com os payloads prontos.

**1. Faça login como Admin** (`POST /api/auth/login`):

```json
{ "email": "admin@argus.com", "senha": "Admin@123" }
```

Copie o valor do campo `token` na resposta.

**2. Clique no botão 🔒 Authorize** no topo da página, cole `Bearer <token>` (com o prefixo `Bearer ` e um espaço) e confirme. Todos os endpoints protegidos passam a enviar o header automaticamente.

**3. Para testar como Brigadista** (perfil restrito), repita o login com `brig@argus.com` / `Brig@123` e troque o token no Authorize.

A partir daqui é só ir nos endpoints, abrir **Try it out**, colar o JSON correspondente e clicar **Execute**. Os payloads abaixo cobrem todos os endpoints que aceitam corpo — os GETs (lista e busca por id) não precisam de payload, só rodar Execute.

### `POST /api/auth/register` — auto-cadastro de Brigadista (público)

`codigoConvite` precisa bater com `Auth:CodigoConvite` — atualmente **`ARGUS-2026`**. Os 3 campos de emergência são opcionais e devem andar juntos.

```json
{
  "nome": "Brigadista de Teste",
  "email": "teste@argus.com",
  "telefone": "11900000000",
  "nomeEmergencia": "Mãe do Teste",
  "telefoneEmergencia": "11988888888",
  "relacaoEmergencia": "Mãe",
  "senha": "Senha@123",
  "codigoConvite": "ARGUS-2026"
}
```

Resposta: `200 OK` com `token`, `expiraEm` e `usuario` (perfil Brigadista, `brigadistaId` preenchido se o email bateu com algum Brigadista cadastrado — ver "demonstrando a auto-vinculação" abaixo).

### Demonstrando a auto-vinculação `Usuario` ↔ `Brigadista`

Pra ver a vinculação automática por email acontecendo (padrão SSO descrito em [Decisões técnicas](#decisões-técnicas-relevantes)):

1. Login como admin (`admin@argus.com` / `Admin@123`), autoriza no Swagger
2. `GET /api/brigadistas` — escolha um da lista e anote o **email** (ex.: `maria.silva@argus.com`, id 1)
3. **Logout** no Authorize
4. `POST /api/auth/register` com o **mesmo email** que você anotou:
   ```json
   {
     "nome": "Maria Silva",
     "email": "maria.silva@argus.com",
     "telefone": "11900000000",
     "senha": "Senha@123",
     "codigoConvite": "ARGUS-2026"
   }
   ```
5. Na resposta, o objeto `usuario` vai vir com `brigadistaId: 1` preenchido — a vinculação aconteceu automaticamente pelo email
6. Login com o user recém-criado → `GET /api/auth/me` confirma o claim/contexto

Se você usar um email que **não bate** com nenhum brigadista (ex.: `aleatorio@teste.com`), o cadastro funciona mas o `brigadistaId` vem `null`. Admin pode vincular depois via `PUT /api/usuarios/{id}` passando o `brigadistaId`.

### `PUT /api/auth/me` — usuário atualiza o próprio perfil

Qualquer perfil autenticado. Nunca altera `Perfil`, `Ativo`, `Email` ou `SenhaHash`.

```json
{
  "nome": "Brigadista Teste Atualizado",
  "telefone": "11912345678",
  "nomeEmergencia": "Pai do Teste",
  "telefoneEmergencia": "11977777777",
  "relacaoEmergencia": "Pai"
}
```

Resposta: `204 No Content`.

### `POST /api/brigadas` — criar brigada (Admin/Coordenador)

```json
{
  "nome": "Brigada PrevFogo Cerrado Norte",
  "baseOperacional": "Brasília, DF",
  "telefone": "6133331234",
  "ativa": true
}
```

### `POST /api/brigadistas` — criar brigadista (Admin/Coordenador)

```json
{
  "nome": "Maria Silva",
  "matricula": "BRG-042",
  "email": "maria.silva@argus.com",
  "telefone": "11987654321",
  "funcao": "Líder de Esquadrão",
  "ativo": true,
  "dataAdmissao": "2024-03-15T00:00:00",
  "brigadaId": 1
}
```

### `POST /api/recursos` — criar recurso (Admin/Coordenador)

`tipo` é enum int: `1` = Veiculo, `2` = Ferramenta, `3` = EPI, `4` = Comunicacao.

```json
{
  "nome": "Caminhão-pipa Mercedes 1620",
  "tipo": 1,
  "disponivel": true,
  "brigadaId": 1
}
```

### `POST /api/ocorrencias` — abrir ocorrência manualmente (Admin/Coordenador)

`status` é enum int: `1` = Aberta, `2` = EmAtendimento, `3` = Controlada, `4` = Finalizada. `alertaId` é opcional — `null` para ocorrência sem alerta vinculado.

```json
{
  "descricao": "Foco de incêndio em vegetação seca, vento moderado",
  "latitude": -15.789,
  "longitude": -47.882,
  "status": 1,
  "dataAbertura": "2026-06-01T14:30:00",
  "brigadistaId": 1,
  "brigadaId": 1,
  "alertaId": null
}
```

### `PUT /api/ocorrencias/{id}` — atualizar status (qualquer perfil logado)

Brigadistas podem dar PUT pra registrar mudança de status (atendimento → finalizada) mesmo sem permissão de criar ou deletar. Lembre de **trocar o token pro de Brigadista** antes de tentar pra ver isso funcionar. O `id` no corpo precisa bater com o `id` da URL.

```json
{
  "id": 1,
  "descricao": "Foco extinto e área monitorada",
  "latitude": -15.789,
  "longitude": -47.882,
  "status": 4,
  "dataAbertura": "2026-06-01T14:30:00",
  "dataFinalizacao": "2026-06-01T17:45:00",
  "brigadistaId": 1,
  "brigadaId": 1
}
```

Resposta: `204 No Content`.

### `POST /api/alertas/{id}/criar-ocorrencia` — promover alerta a ocorrência (Admin/Coordenador)

Endpoint estrela do projeto: busca o alerta no Java pelo proxy, monta a descrição com `titulo` + `descricao` + `recomendacaoOperacional` do alerta e cria a ocorrência já vinculada. Se `descricao` vier `null`, o servidor monta automaticamente; se preenchida, sobrescreve. Coloque o id de um alerta real (ex.: `114`) na URL.

```json
{
  "brigadaId": 1,
  "brigadistaId": 1,
  "latitude": -16.5,
  "longitude": -56.5,
  "descricao": null
}
```

Resposta: `201 Created` com a `Ocorrencia` (já com `alertaId` preenchido). Erros: `404` se o alerta não existe no Java, `400` se brigada/brigadista não existe no banco. Esse mesmo fluxo é automatizado pelo [consumer RabbitMQ](#mensageria-assíncrona-rabbitmq) — o endpoint manual continua existindo como fallback para ocorrências sem alerta.

### `POST /api/registroscampo` — evidência de campo

Foto + GPS + observação coletados pelo brigadista durante o atendimento. Uma ocorrência aceita N registros.

```json
{
  "observacao": "Frente do fogo controlada. Solicitando reforço para extinção total.",
  "urlFoto": "https://storage.argus.com/registros/2026-06-01-001.jpg",
  "latitude": -15.7891,
  "longitude": -47.8823,
  "dataRegistro": "2026-06-01T15:20:00",
  "ocorrenciaId": 1
}
```

> ⚠️ **Regra granular por brigada:** se o usuário logado for Brigadista, a `ocorrenciaId` precisa pertencer à **mesma brigada** do brigadista vinculado ao usuário (via `Usuario.BrigadistaId → Brigadista.BrigadaId`). Brigadista de outra brigada → `403 Forbidden`. Admin/Coordenador escrevem em qualquer brigada. Detalhes na seção [Matriz de permissões](#matriz-de-permissões).

### `POST /api/usuarios` — criar usuário (apenas Admin)

Diferente do `/api/auth/register` (público e Brigadista-only), esta rota é Admin-only e permite definir o `perfil` direto. Enum: `1` = Admin, `2` = Coordenador, `3` = Brigadista.

```json
{
  "nome": "Coordenador Regional Norte",
  "email": "coord.norte@argus.com",
  "telefone": "92988887777",
  "senha": "Coord@123",
  "perfil": 2
}
```

### `PUT /api/{recurso}/{id}` — padrão de update em todos os CRUDs

Toda entidade (`brigadas`, `brigadistas`, `recursos`, `ocorrencias`, `registroscampo`, `usuarios`) aceita PUT com **o mesmo body do POST + o `id` repetido no corpo**. O id do corpo precisa bater com o id da URL, senão retorna `400`. Resposta padrão: `204 No Content`. Exemplo para `PUT /api/brigadas/1`:

```json
{
  "id": 1,
  "nome": "Brigada PrevFogo Cerrado Norte (renomeada)",
  "baseOperacional": "Brasília, DF",
  "telefone": "6133331234",
  "ativa": false
}
```

### Cenários que demonstram a matriz de permissões

Pra ver a autorização por role em ação, faça login como Brigadista (`brig@argus.com` / `Brig@123`) e tente:

- **`DELETE /api/brigadas/1`** → esperado `403 Forbidden` (DELETE é Admin/Coordenador-only)
- **`GET /api/usuarios`** → esperado `403 Forbidden` (qualquer operação em `/api/usuarios` é Admin-only)

Volte pro token de Admin e os mesmos endpoints respondem `204` e `200` respectivamente.

**Cenário da regra granular por brigada** (a parte mais interessante):

1. Logue como `maria.silva@argus.com` / `Teste@123` (brigadista vinculada à Brigada 1 via auto-link por email).
2. `POST /api/registroscampo` apontando para uma **ocorrência da Brigada 1** → `201 Created`.
3. Mesmo POST apontando para uma **ocorrência de outra brigada** → `403 Forbidden`, mesmo o brigadista sendo um perfil que normalmente "pode" escrever registros — a regra é por brigada, não só por role.
4. Logue como Admin e o mesmo POST de cross-brigada responde `201` — Admin/Coordenador escrevem em qualquer brigada.

Isso prova que a autorização tem **duas camadas**: `[Authorize(Roles = ...)]` no atributo + checagem programática contra o banco no controller (incluindo proteção contra bypass via troca de `OcorrenciaId` no body do PUT).

## Tratamento global de erros

Um `GlobalExceptionHandler` registrado via `IExceptionHandler` intercepta exceções não tratadas e devolve um `ProblemDetails` consistente — traduz erros de banco e de chamadas externas em status HTTP semanticamente corretos, em vez de devolver 500 cru com stack trace exposta.

| Origem | Cenário | Status HTTP | Título |
|---|---|---|---|
| Oracle `ORA-00001` | Constraint UNIQUE violada (ex.: email duplicado) | 409 Conflict | Registro duplicado |
| Oracle `ORA-02291` | FK insert: ID referenciado não existe | 400 Bad Request | Referência inválida |
| Oracle `ORA-02292` | FK delete: registro tem filhos | 409 Conflict | Registro com dependências |
| Oracle `ORA-12541 / 12545 / 12170` | TNS: banco inacessível | 503 Service Unavailable | Banco indisponível |
| Outro `DbUpdateException` | Demais erros de gravação | 500 | Erro ao salvar no banco |
| `HttpRequestException` | Chamada à API Java falhou (Java fora do ar) | 503 Service Unavailable | API externa indisponível |
| `TaskCanceledException` | Timeout (>10s) em chamada externa | 504 Gateway Timeout | Timeout em API externa |
| Demais exceções | Bugs ou casos não previstos | 500 | Erro interno |

Os códigos Oracle são detectados via varredura da cadeia de `InnerException` — erros de conexão (12541/12545) podem aparecer encapsulados em wrappers diferentes do `DbUpdateException` típico de violações de integridade.

Exemplo de resposta para um `DELETE /api/brigadas/1` quando há brigadistas vinculados à brigada:

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Registro com dependências",
  "status": 409,
  "detail": "Esse registro está sendo referenciado por outros e não pode ser removido.",
  "instance": "/api/brigadas/1"
}
```

## Health check

`GET /health` é público (sem auth) e retorna um JSON com o status geral e o resultado do check do banco. Útil para load balancers, ferramentas de monitoring e para confirmar rapidamente se a API e a conexão Oracle estão de pé.

```bash
curl -s http://localhost:5215/health | jq
```

Resposta típica:

```json
{
  "status": "Healthy",
  "totalDurationMs": 28.4,
  "checks": [
    { "name": "oracle-db", "status": "Healthy", "durationMs": 28.1, "error": null }
  ]
}
```

Quando o Oracle está fora, `status` vira `Unhealthy`, o HTTP é 503 e o campo `error` traz o motivo retornado pelo provider.

## Integração com a API Java

O Argus opera com dois backends em domínios distintos: o **Java** ([argus-intelligence-api](https://github.com/alanerochaa/argus-intelligence-api)), responsável pela detecção de focos de incêndio via satélite (geração de alertas), e o **.NET** (este projeto), responsável pelas operações de resposta em campo. A integração entre eles acontece em três níveis.

**Nível 1 — referência por id (cross-schema).** Cada microserviço opera contra **seu próprio schema** no Oracle FIAP (a equipe usa contas distintas — Java na conta da integrante de Intelligence, .NET nesta conta). A entidade `Ocorrencia` carrega um campo `AlertaId` (nullable) com o identificador do alerta do lado do Java, sem qualquer constraint física entre os bancos — é uma "soft foreign key" resolvida em tempo de aplicação. A motivação prática dessa separação foi evitar o limite de `SESSIONS_PER_USER = 10` do servidor FIAP, que era recorrentemente saturado quando Java + .NET + SQL Developer + IDE competiam pela mesma conta.

**Nível 2 — chamada HTTP via client tipado.** Um `HttpClient` tipado (`IAlertaJavaClient` na Application, `AlertaJavaClient` na Infrastructure) faz requisições à API Java pra buscar os detalhes de um alerta específico. O endpoint `GET /api/alertas/{id}` deste projeto serve como proxy pra API Java, o que permite ao cliente mobile consultar alertas pelo mesmo backend que já está autenticando. O cliente também é tolerante ao envelope HATEOAS adotado pela API Java na listagem (`_embedded.alertaResponseDTOList`) sem precisar tipar todo o wrapper.

**Nível 3 — mensageria assíncrona via fila AMQP.** A integração mais recente é via broker AMQP gerenciado (CloudAMQP/LavinMQ): a API Java publica alertas críticos na fila `argus.alertas` e o `AlertaConsumerService` deste projeto consome em background, criando ocorrências operacionais sem ação manual. Detalhes na seção [Mensageria assíncrona (RabbitMQ)](#mensageria-assíncrona-rabbitmq).

**Configuração:** a URL base da API Java é configurável em `appsettings.json`:

```json
"JavaApi": {
  "BaseUrl": "http://localhost:8080"
}
```

Pode ser sobrescrita via user-secrets (`JavaApi:BaseUrl`) ou variável de ambiente (`JavaApi__BaseUrl`). O timeout default é 10 segundos.

**Tratamento de erros da chamada externa.** `HttpRequestException` (Java fora) vira 503 e `TaskCanceledException` (timeout >10s) vira 504, ambos via `GlobalExceptionHandler` — ver [Tratamento global de erros](#tratamento-global-de-erros) para a tabela consolidada.

**Contrato real do retorno da API Java**, espelhado do `AlertaResponseDTO`:

```csharp
record AlertaDto(
    long Id,
    string Titulo,
    string? Descricao,
    string Nivel,                 // BAIXO | MEDIO | ALTO | CRITICO
    string Status,                // ABERTO | EM_ANALISE | ENCAMINHADO | ENCERRADO
    double? ScoreRisco,
    string? RecomendacaoOperacional,
    DateTime DataGeracao,
    DateTime? DataAtualizacao,
    long FocoCalorId              // FK pro foco de calor que originou o alerta
);
```

Coordenadas (latitude/longitude) não estão no alerta — vivem na entidade `FocoCalor` referenciada por `FocoCalorId` e podem ser obtidas via `GET /api/focos`. O `FocoCalorDto` (em `Argus.Operations.Application/Integration/`) traz lat/long, FRP (Fire Radiative Power), temperatura estimada, satélite e sensor.

## Mensageria assíncrona (RabbitMQ)

Além do proxy HTTP, a API Java também publica eventos de alerta em uma fila AMQP gerenciada (CloudAMQP/LavinMQ). Quando um alerta de nível **ALTO** ou **CRITICO** é gerado, o publisher da API Java envia uma mensagem enriquecida (alerta + foco + região) para a fila `argus.alertas`. Esta API .NET consome a fila em background e converte cada mensagem em uma **ocorrência operacional**, sem qualquer ação manual do coordenador.

O fluxo end-to-end:

```mermaid
flowchart LR
    SAT["Satélite NASA FIRMS"] --> JV["API Java<br/>(Spring Boot)"]
    JV -->|trigger PL/SQL| ORACLE[("Oracle")]
    JV -->|"publica ALTO/CRITICO"| Q[("fila argus.alertas<br/>CloudAMQP")]
    Q -->|consome| NET["API .NET<br/>AlertaConsumerService"]
    NET -->|"cria Ocorrência (status=Aberta)"| ORACLE
    NET -->|notifica| MOB["App Mobile<br/>(brigadista)"]
```

**`AlertaConsumerService`** é um `BackgroundService` registrado no `Program.cs` via `AddHostedService`. Roda em paralelo ao pipeline HTTP normal — o consumer conecta uma vez no startup, fica escutando a fila pelo tempo de vida da aplicação, e processa cada mensagem em scope próprio (cria um `IServiceScope` por mensagem para resolver o `ArgusDbContext`).

**Garantias implementadas:**

| Cenário | Comportamento |
|---|---|
| Alerta novo chega | Cria ocorrência com brigada/brigadista default (primeiros ativos do banco) + lat/long do foco |
| Mesmo alerta entregue 2x (replay) | **Ack sem criar duplicata** — checa `db.Ocorrencias.AnyAsync(o => o.AlertaId == X)` antes de inserir |
| Banco caiu durante processamento | Nack com **requeue=true** — mensagem volta pra fila pra retry automático |
| Mensagem com JSON malformado | Nack **requeue=false** — vai pro DLQ, não fica em loop infinito |
| Conexão AMQP cai | Auto-recovery do driver (10s) + loop externo reconnect (mais 10s) |
| API restartando | `StopAsync` fecha channel + connection limpos |

**Idempotência via `AlertaId`.** O Java garante que cada alerta tem um id único (auto-increment do Oracle). Quando o consumer recebe uma mensagem, primeiro verifica se já existe ocorrência com aquele `AlertaId`; se sim, apenas ack a mensagem sem criar nova. Isso protege contra:

- Mensagem entregue mais de uma vez por race no broker (AMQP é at-least-once por design, não exactly-once)
- Re-publish da Alane durante testes
- Restart do consumer no meio do processamento

O detalhe que torna o consumer seguro é o `SELECT` de checagem **antes** do `INSERT` — uma reentrega da mesma mensagem é simplesmente reconhecida (ack) sem virar uma segunda ocorrência:

```mermaid
sequenceDiagram
    participant Q as fila argus.alertas<br/>(CloudAMQP)
    participant C as AlertaConsumerService<br/>(BackgroundService)
    participant DB as Oracle FIAP

    Q->>C: entrega AlertaQueueDto (alertaId=X)
    C->>DB: SELECT Ocorrencias WHERE AlertaId == X
    alt Não existe
        DB-->>C: vazio
        C->>DB: INSERT Ocorrencia (status=Aberta)
        C->>Q: BasicAck
    else Já existe (replay / re-publish)
        DB-->>C: ocorrência encontrada
        C->>Q: BasicAck (sem duplicar)
    end
    Note over C,Q: JSON malformado → Nack requeue=false (DLQ)<br/>Banco fora → Nack requeue=true (retry)
```

**Por que `BasicQos(prefetchCount: 1)`.** Configurar prefetch baixo é deliberado — processa uma mensagem por vez. Em troca de menos throughput, ganha previsibilidade na ordem de criação das ocorrências e simplifica a defesa de idempotência (não tem mensagem "em voo" enquanto outra é processada).

**Compatibilidade Java ↔ .NET no JSON.** O Spring Boot serializa `LocalDateTime` por padrão como array de inteiros `[year, month, day, hour, minute, second, nano]` quando `JavaTimeModule` não está configurado. Em vez de depender da config do lado Java, o consumer tem um `JavaLocalDateTimeConverter` (`JsonConverter<DateTime>`) que aceita tanto array Java quanto string ISO 8601 — torna a integração robusta mesmo se a Alane mudar a config do Jackson dela depois.

**Configuração:**

```json
"RabbitMq": {
  "QueueName": "argus.alertas"
}
```

A `ConnectionString` (com credencial do broker) **nunca** é commitada — fica em user-secrets em dev e em Azure Application Settings em prod:

```bash
dotnet user-secrets set "RabbitMq:ConnectionString" "amqps://..."
```

**Por que mensageria além do proxy HTTP.** O proxy HTTP (`GET /api/alertas`) atende o caso "mobile abre a tela de alertas e quer ver a lista atual". A mensageria atende o caso "satélite acabou de detectar um foco crítico, brigada precisa saber AGORA". Os dois canais coexistem porque resolvem problemas diferentes — leitura sob demanda vs. push assíncrono.

## Landing page institucional

A API serve uma página HTML estática em `/` (`wwwroot/index.html`) com a mesma identidade visual da API Java (cores, logo, tipografia, layout de cards). Isso reforça o pitch de "**ecossistema único de dois microserviços**" em vez de "duas APIs independentes que conversam por acidente".

A página apresenta:

- Hero com logo do Argus e descrição da responsabilidade desta API (operações de campo)
- Tags da stack (.NET 9, ASP.NET Core, EF Core, Oracle, JWT, BCrypt, Serilog, xUnit, Azure)
- Hub de atalhos: Health Check, Swagger, GitHub, link pra API Java, identidade do token (`/api/auth/me`)
- Cards de documentação técnica: Swagger UI, OpenAPI JSON, Health Check
- Cards dos domínios de negócio: Brigadas, Brigadistas, Ocorrências, Recursos, Alertas (proxy), Focos (proxy)

A camada de arquivos estáticos é habilitada por `app.UseDefaultFiles()` + `app.UseStaticFiles()` no `Program.cs`, posicionada depois do exception handler e antes do middleware de autenticação — assim a landing page é pública (qualquer um vê), mas as APIs sob `/api/...` continuam protegidas por JWT.

## Testes automatizados

O projeto `Argus.Operations.Tests` reúne testes unitários e integração-leve com xUnit, todos seguindo o padrão Arrange/Act/Assert com comentários explícitos para tornar a estrutura legível.

```bash
dotnet test
```

Cobertura atual (38 testes verdes em torno de 1 segundo):

- **`BcryptPasswordHasherTests`**: garante que `Hash` produz valores não vazios e diferentes a cada chamada (salt aleatório do BCrypt), e que `Verify` aceita a senha correta e rejeita a errada.
- **`TokenServiceTests`**: valida o formato do JWT gerado (header.payload.signature), a presença das claims básicas (`sub`, `email`, `name`), o claim de role correspondente ao `PerfilUsuario`, e a unicidade do `jti` em chamadas consecutivas (prevenção de replay).
- **`AuthControllerTests`**: usa `ArgusDbContext` com provider InMemory para exercitar o fluxo completo de `/api/auth/login` (credenciais válidas, senha errada, email inexistente, usuário inativo) e `/api/auth/register` (código de convite correto, código errado, email já existente).
- **`AlertasControllerTests`**: cobre o proxy `/api/alertas` (listagem e busca por id) e o endpoint `/api/alertas/{id}/criar-ocorrencia` que promove um alerta do Java a uma ocorrência operacional — testando caminho feliz, alerta inexistente no Java (404), brigada inexistente (400 com mensagem específica), brigadista inexistente (400) e herança automática da descrição quando não fornecida.
- **`FocosControllerTests`**: valida o proxy `/api/focos` (mapa de calor), tanto com dados quanto com resposta vazia do Java.
- **`RegistrosCampoControllerTests`**: cobre a autorização granular por brigada nos endpoints de escrita (Create/Update/Delete). 12 casos: caminho feliz pra brigadista na mesma brigada (201/204), bloqueio (`Forbid`) pra brigadista tentando escrever em ocorrência de outra brigada, bloqueio pra brigadista sem vínculo operacional, passe-livre pra Admin e Coordenador em qualquer brigada, `NotFound` quando ocorrência referenciada não existe, e **caso de anti-bypass** (brigadista tentando trocar `OcorrenciaId` no body do PUT pra forjar acesso — regra valida contra o registro ORIGINAL no banco).

A camada de integração com Java usa **fakes manuais** (sem Moq) que implementam `IAlertaJavaClient` e `IFocoCalorJavaClient` — segue o padrão "sem mocks externos" já estabelecido no resto da suíte. A escolha de cobrir profundamente autenticação + integração é deliberada: o CRUD dos demais controllers é predominantemente código que delega ao EF Core, e seu comportamento é mais bem demonstrado via Swagger do que via testes que essencialmente testariam o próprio framework.

## Estrutura de pastas

```
argus-operations/
├── Argus.Operations.API/                  # Camada de apresentação (ASP.NET Core)
│   ├── Auth/                              # Roles (constantes), helpers de autorização
│   ├── Controllers/                       # AuthController, AlertasController, FocosController
│   │                                      # + CRUDs (Brigadas, Brigadistas, Recursos,
│   │                                      # Ocorrencias, RegistrosCampo, Usuarios)
│   ├── DTOs/Auth/                         # LoginRequest, RegisterRequest, AuthResponse,
│   │                                      # AtualizarPerfilRequest
│   ├── DTOs/Ocorrencias/                  # CriarOcorrenciaDeAlertaRequest
│   ├── DTOs/Usuarios/                     # UsuarioDtos (CRUD admin)
│   ├── Exceptions/GlobalExceptionHandler  # IExceptionHandler + mapa Oracle/HTTP→ProblemDetails
│   ├── Properties/launchSettings.json
│   ├── appsettings.json                   # Jwt, Auth:CodigoConvite, JavaApi, RabbitMq, Serilog
│   ├── Program.cs                         # Composição: DI, JWT, Swagger, Serilog, CORS,
│   │                                      # CloudAMQP consumer, static files, pipeline
│   └── wwwroot/                           # Landing page institucional (index.html + css + logo)
│
├── Argus.Operations.Application/          # Contratos de aplicação (sem dependências de infra)
│   ├── Auth/                              # ITokenService, IPasswordHasher
│   ├── Integration/                       # IAlertaJavaClient, IFocoCalorJavaClient,
│   │                                      # AlertaDto, FocoCalorDto
│   └── Messaging/                         # AlertaQueueDto (espelha a mensagem do RabbitMQ)
│
├── Argus.Operations.Infrastructure/       # Implementações de infra
│   ├── Auth/                              # TokenService (JWT), BcryptPasswordHasher,
│   │                                      # JwtSettings, AdminSeeder
│   ├── Data/ArgusDbContext.cs             # Mapeamento EF Core ↔ Oracle
│   ├── Integration/                       # AlertaJavaClient, FocoCalorJavaClient (HttpClient tipado)
│   ├── Messaging/                         # AlertaConsumerService (BackgroundService) +
│   │                                      # JavaLocalDateTimeConverter (compat Jackson)
│   └── Migrations/                        # Migrations versionadas (EF Core)
│
├── Argus.Operations.Domain/               # Entidades puras
│   ├── Entities/                          # Brigada, Brigadista, Recurso,
│   │                                      # Ocorrencia, RegistroCampo, Usuario
│   └── Enum/                              # PerfilUsuario, TipoRecurso, StatusOcorrencia
│
├── Argus.Operations.Tests/                # xUnit (38 testes)
│   ├── Auth/                              # BcryptPasswordHasherTests, TokenServiceTests
│   ├── Controllers/                       # AuthControllerTests, AlertasControllerTests,
│   │                                      # FocosControllerTests, RegistrosCampoControllerTests
│   └── Integration/                       # Fakes: FakeAlertaJavaClient, FakeFocoCalorJavaClient
│
├── argus-tabelas-dotnet.sql               # DDL consolidado (alternativa às migrations)
├── seed-dados-teste.sql                   # Carga inicial (brigadas, brigadistas, recursos,
│                                          # ocorrências de exemplo)
└── Argus.Operations.sln
```

## Decisões técnicas relevantes

Algumas escolhas de projeto que merecem nota:

**Oracle pré-23ai e `UseOracleSQLCompatibility(DatabaseVersion19)`**. O Oracle da FIAP não suporta literais `TRUE`/`FALSE` em SQL (suporte só existe a partir do 23ai). Sem a configuração de compatibilidade, queries do EF Core que envolvem `bool` (como `AnyAsync(x => x.Ativo)`) emitem `CASE WHEN ... THEN TRUE ELSE FALSE END` e disparam `ORA-00904: "FALSE": invalid identifier`. Forçar a compatibilidade com a versão 19 faz o provider emitir `1`/`0`. Em paralelo, no `ArgusDbContext`, todas as colunas `bool` são mapeadas explicitamente como `NUMBER(1)` para evitar surpresa no schema gerado.

**Swashbuckle 6.9 em vez do 10.x mais recente**. O Swashbuckle 10.x depende do Microsoft.OpenApi 2.x, que introduziu um bug na serialização de `OpenApiSecuritySchemeReference`: o requirement de Bearer JWT é gerado como `[{}]` no `swagger.json`, o que faz o Swagger UI não enviar o header `Authorization` mesmo após o usuário clicar em Authorize. O downgrade para 6.9 (que usa Microsoft.OpenApi 1.x e o padrão clássico de `OpenApiReference`) resolveu o problema sem precisar de filtros customizados.

**`MapInboundClaims = false` e `RoleClaimType` explícito**. No .NET 8+ o handler default de JWT (`JsonWebTokenHandler`) popula o `ClaimsIdentity` de forma sutilmente diferente do antigo. Sem definir `RoleClaimType = ClaimTypes.Role` no `TokenValidationParameters`, `[Authorize(Roles = "...")]` falha **silenciosamente** — o claim de role chega no token mas `IsInRole` retorna `false` e o framework libera o acesso como se fosse `[Authorize]` sem roles. O `GET /api/auth/me` foi adicionado pra diagnosticar esse tipo de problema rapidamente.

**Defense in depth no mobile**. Embora o app mobile aplique UI condicional (esconde botões que o usuário não pode usar), o backend não confia nisso: todo endpoint mantém seu `[Authorize(Roles = "...")]` ativo. A UI evita confusão e cliques sem efeito; o backend garante segurança real. Um aplicativo modificado ou uma chamada feita por fora ainda recebe 403 do servidor.

**Separação `Usuario` x `Brigadista` com auto-vinculação por email (padrão SSO)**. Modelamos os dois como aggregates distintos: `Usuario` é o **contexto de autenticação** (quem tem credencial pra entrar no sistema), `Brigadista` é o **contexto operacional** (membro despachado pra ocorrências em campo). A ligação acontece via FK opcional `Usuario.BrigadistaId`. No `POST /api/auth/register`, se o email do novo usuário bater com algum `Brigadista` já cadastrado, o vínculo é estabelecido automaticamente — mesmo padrão que SSO como Slack/GitHub usam (você cria a conta, e fica vinculado aos workspaces/orgs onde seu email tá registrado). Caso o email não bata, o usuário fica como "voluntário pré-vinculação" e pode ser linkado depois pelo coordenador via `PUT /api/usuarios/{id}`. Essa separação reflete cenários reais: brigadistas voluntários cadastrados sem login, e admins/coordenadores administrativos sem necessariamente atuar em campo.

**Serilog substituindo o `ILogger` padrão, posicionado ANTES do `UseExceptionHandler`**. O `ILogger` default do ASP.NET Core emite logs em texto simples e quebra cada request em 3-4 linhas separadas (start, action selecting, executing, completed). O Serilog com `UseSerilogRequestLogging()` consolida em uma linha estruturada por request (método, path, status, duração em ms) e permite que filtros por `SourceContext` sejam definidos sem recompilar — útil pra silenciar verbosidade do EF Core sem perder logs da aplicação. O detalhe sutil é o **posicionamento no pipeline**: `UseSerilogRequestLogging()` precisa vir antes de `UseExceptionHandler()`. Se vier depois, ele loga o status HTTP no momento em que a exception passa por ele (geralmente 500 default), antes do handler converter pra 503/504 com mensagem amigável — o log "mente" sobre o que o cliente realmente recebeu. Com a ordem correta, o exception handler "termina" o request primeiro (escrevendo 503 no response), e o request logging registra o status verdadeiro quando o pipeline retorna.

**CORS aberto em dev (`AllowAnyOrigin`)**. A política `ArgusCors` libera qualquer origem, header e método. Isso é deliberado pro ambiente acadêmico: o app mobile (React Native em emulador ou device físico em rede local) bate na API a partir de IPs imprevisíveis, e o objetivo da disciplina não é demonstrar configuração fina de CORS — é demonstrar que o problema foi reconhecido e tratado. Em produção, a política seria restrita ao domínio publicado do app mobile via Application Settings. O posicionamento de `UseCors()` antes de `UseAuthentication()` é essencial pra que o preflight `OPTIONS` (que vai sem token) não retorne 401 antes de a policy ser avaliada.

## Deploy na Azure

A API é publicada no **Azure App Service**, que hospeda o runtime .NET 9 e expõe o Swagger publicamente. A conexão com o Oracle FIAP e o JWT secret são injetados como **Application Settings** no portal — nunca commitados no repositório (segue o padrão "User Secrets em dev, variável de ambiente em prod" cobrado pela disciplina).

### Variáveis configuradas no App Service

| Nome | Onde aparece no código | Propósito |
|---|---|---|
| `ConnectionStrings__OracleDb` | `appsettings.json` → seção `ConnectionStrings` | String de conexão com Oracle FIAP (usuário, senha, host, pool) |
| `Jwt__Key` | `JwtSettings.Key` | Chave HMAC SHA-256 usada pra assinar e validar o JWT |
| `Auth__CodigoConvite` | `AuthController` (registro de novo usuário) | Código fixo que o brigadista digita ao se cadastrar |
| `JavaApi__BaseUrl` | `HttpClient` registrado no `Program.cs` | URL pública da API Java (alertas + focos) |
| `RabbitMq__ConnectionString` | `AlertaConsumerService` | URL AMQP do CloudAMQP (formato `amqps://user:pass@host/vhost`) — consumida pelo BackgroundService |

> Observação: o duplo underscore (`__`) é a forma do ASP.NET Core ler chaves hierárquicas a partir de variáveis de ambiente — equivale ao `:` usado no `appsettings.json` (`ConnectionStrings:OracleDb`).

### URL do ambiente publicado

A API está deployada em **Azure App Service (Linux, B1)** na região South Africa North:

- 🌐 **Landing page institucional:** https://argus-operations-rm559561.azurewebsites.net
- 📘 **Swagger UI:** https://argus-operations-rm559561.azurewebsites.net/swagger
- ❤️ **Health check:** https://argus-operations-rm559561.azurewebsites.net/health
- 📄 **OpenAPI JSON:** https://argus-operations-rm559561.azurewebsites.net/swagger/v1/swagger.json

### Pipeline CI/CD

Workflow GitHub Actions gerado pelo **Azure Deployment Center** com autenticação federada **OIDC** (User-assigned Managed Identity) — nenhum publish profile ou senha vai pro repositório, apenas IDs públicos das credenciais federadas. A cada `git push` na `main`, o pipeline executa: `dotnet restore` → `dotnet build` → `dotnet publish` no GitHub Runner → upload de artefato → deploy no App Service.

Arquivo do workflow: [`.github/workflows/main_argus-operations-rm559561.yml`](.github/workflows/main_argus-operations-rm559561.yml).

## Evidências em produção

A API está publicada na Azure e pode ser inspecionada ao vivo — deixamos os endpoints abertos para a banca verificar diretamente, e complementamos com prints das duas features que um clique no Swagger **não** revela (autorização granular por brigada e ocorrência criada pela fila):

| Evidência | Link |
|---|---|
| 🌐 Landing page institucional | https://argus-operations-rm559561.azurewebsites.net |
| 📘 Swagger UI (lista completa de endpoints, botão **Authorize** com JWT) | https://argus-operations-rm559561.azurewebsites.net/swagger |
| ❤️ Health check do Oracle (`AddDbContextCheck` + duração) | https://argus-operations-rm559561.azurewebsites.net/health |
| 📄 OpenAPI 3.0 JSON (consumível por Postman/Insomnia) | https://argus-operations-rm559561.azurewebsites.net/swagger/v1/swagger.json |

**Fluxo sugerido para a banca avaliar:**

1. Abrir o Swagger e fazer `POST /api/auth/login` com `admin@argus.com` / `Admin@123` → recebe `token` (sem `senhaHash` no payload).
2. Clicar em **Authorize** e colar `Bearer <token>`.
3. Testar `GET /api/brigadas` → 200; sair do Authorize → 401; logar como Brigadista (`brig@argus.com` / `Brig@123`) → `DELETE /api/brigadas/1` → 403.
4. `GET /api/alertas` → devolve a lista vinda da API Java (proxy) — comprova a integração service-to-service entre as duas APIs do grupo.
5. `GET /health` → confirma que a API está conectada ao Oracle da FIAP em tempo real.

### Galeria de prints

As duas primeiras provam comportamentos que só aparecem em runtime (não dá pra ver clicando no Swagger): o `403` da regra granular por brigada e a ocorrência nascida de uma mensagem da fila.

| Evidência | Print |
|---|---|
| ⭐ Autorização granular por brigada — brigadista de outra brigada recebe `403` ao tentar escrever registro | ![Autorização granular 403](docs/autorizacao-granular-403.png) |
| ⭐ Mensageria — ocorrência criada pelo consumer a partir de alerta da fila (IDs 22, 25, 26 com `ID_ALERTA` preenchido) | ![Ocorrência criada via fila no banco](docs/mensageria-ocorrencia-no-banco.png) |
| Swagger UI autenticado (botão **Authorize** com Bearer JWT ativo) | ![Swagger UI autorizado](docs/swagger-ui-autorizado.png) |
| Health check do Oracle FIAP respondendo `Healthy` | ![Health check Oracle](docs/health-check-oracle.png) |
| Deploy no Azure App Service (Linux B1, South Africa North) | ![Deploy Azure App Service](docs/deploy-azure-app-service.png) |
| Suíte xUnit — 38 testes verdes | ![38 testes verdes](docs/dotnet-test-38-verdes.png) |

**Suíte local:** rodar `dotnet test` reproduz os 38 testes em xUnit cobrindo `AuthController`, `AlertasController`, `FocosController`, `RegistrosCampoController` (incluindo autorização granular por brigada e teste de bypass), `PasswordHasher` e `TokenService`.

## Integrantes

| Nome | RM | Responsabilidade no Argus |
|---|---|---|
| Anna Beatriz de Araujo Bonfim | 559561 | .NET (esta API) + [Mobile (React Native)](https://github.com/annabonfim/argus-app) + parte de Compliance/TOGAF |
| Alane Rocha da Silva | 561052 | Java Advanced ([Intelligence API](https://github.com/alanerochaa/argus-intelligence-api) + RabbitMQ) + PL/SQL + Compliance |
| Maria Eduarda Araujo Penas | 560944 | DevOps & Cloud (Azure Pipelines) + Disruptive Architectures (IoT) + Compliance |

**Turma:** TDS — FIAP
**Projeto:** Global Solution 2026/1 — Argus
**Data de entrega:** 09/06/2026
