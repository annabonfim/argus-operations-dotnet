using System.Security.Claims;
using System.Text;
using Argus.Operations.API.Exceptions;
using Argus.Operations.Application.Auth;
using Argus.Operations.Application.Integration;
using Argus.Operations.Infrastructure.Auth;
using Argus.Operations.Infrastructure.Integration;
using Argus.Operations.Infrastructure.Data;
using Argus.Operations.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Oracle.ManagedDataAccess.Client;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ===== Logging estruturado com Serilog =====
// Substitui o ILogger padrão (texto plano) por Serilog (JSON ou texto rico,
// com properties tipadas tipo {RequestId}, {SourceContext} etc). Lê config
// de Serilog:* no appsettings.json — facilita ajustar nível de log por
// namespace sem recompilar. As injeções de ILogger<T> nos controllers
// continuam funcionando, Serilog roteia tudo pela mesma interface.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ===== Serviços =====
builder.Services.AddControllers();

// ===== Tratamento global de exceções =====
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ===== Health Checks (app + Oracle via DbContext) =====
// "self" = liveness da própria aplicação (se responde, o processo está de pé);
// "oracle-db" = conectividade com o banco. Os dois aparecem separados no JSON de /health.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API no ar"), tags: ["live"])
    .AddDbContextCheck<ArgusDbContext>(name: "oracle-db");

// ===== Clientes HTTP tipados pra API Java (alertas e focos via satélite) =====
// URL base configurável em appsettings → JavaApi:BaseUrl (override por user-secrets ou env).
// Os clients de domínio (Alerta, FocoCalor) passam pelo JavaAuthHandler que
// injeta um Bearer token obtido via POST /api/auth/login no Java — defesa em
// profundidade, nenhum endpoint do Java fica aberto.
var javaBaseUrl = builder.Configuration["JavaApi:BaseUrl"] ?? "http://localhost:8080";

// JavaAuthClient é HttpClient separado e SEM o handler — pra evitar
// recursão (precisaria de token pra pedir token).
builder.Services.AddHttpClient<IJavaAuthClient, JavaAuthClient>(client =>
{
    client.BaseAddress = new Uri(javaBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<JavaTokenProvider>();
builder.Services.AddTransient<JavaAuthHandler>();

builder.Services.AddHttpClient<IAlertaJavaClient, AlertaJavaClient>(client =>
{
    client.BaseAddress = new Uri(javaBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<JavaAuthHandler>();

builder.Services.AddHttpClient<IFocoCalorJavaClient, FocoCalorJavaClient>(client =>
{
    client.BaseAddress = new Uri(javaBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<JavaAuthHandler>();

// ===== Swagger/OpenAPI com suporte a Bearer JWT =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Argus Operations API",
        Version = "v1",
        Description = "API operacional do Argus — sistema de combate a incêndios florestais. " +
                      "Gerencia brigadas, brigadistas, ocorrências, recursos e registros de campo.",
        Contact = new OpenApiContact
        {
            Name = "Equipe Argus",
            Email = "contato@argus.com"
        }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Digite: Bearer {seu_token_jwt}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ===== DbContext com Oracle =====
// UseOracleSQLCompatibility("11") força SQL compatível com Oracle pré-23ai
// (emite 1/0 em vez de TRUE/FALSE — evita ORA-00904 em queries com bool)
builder.Services.AddDbContext<ArgusDbContext>(options =>
    options.UseOracle(
        builder.Configuration.GetConnectionString("OracleDb"),
        oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)
    )
);

// ===== Auth: JwtSettings + serviços =====
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

// ===== Autenticação JWT =====
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Desativa o mapeamento automático de claim names curtas pras URIs longas,
        // mantendo no ClaimsPrincipal os mesmos types que estão no JWT.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
            // Diz pro ClaimsIdentity qual claim type representa role/nome — sem isso o
            // [Authorize(Roles = "...")] não acha o claim e libera tudo pra qualquer logado.
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };
    });
builder.Services.AddAuthorization();

// ===== Consumer RabbitMQ (CloudAMQP) =====
// BackgroundService que escuta a fila 'argus.alertas' e cria ocorrência
// automática pra cada alerta ALTO/CRITICO publicado pelo Java. Connection
// string vem de RabbitMq:ConnectionString (user-secrets em dev, Application
// Settings em prod) — nunca commitada.
builder.Services.AddHostedService<AlertaConsumerService>();

// ===== CORS =====
// Mobile (React Native em emulador/device) e qualquer ferramenta de teste
// precisam consumir essa API de origens variadas. Em dev/demo liberamos tudo;
// em prod a banca espera ver a política travada por origem específica.
const string CorsPolicyArgus = "ArgusCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyArgus, policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Loga cada HTTP request com método, path, status e duração em uma linha
// estruturada. Precisa vir ANTES do UseExceptionHandler pra capturar o status
// final (503/504 traduzido pelo handler) em vez do 500 cru que o pipeline
// emitiria se o exception bubblasse direto pra cá.
app.UseSerilogRequestLogging();

// ===== Tratamento global de exceções =====
app.UseExceptionHandler();

// Limpa qualquer pool herdado antes do primeiro acesso ao Oracle.
// Sem isso, em alguns cenários de restart o driver tenta reusar conexões
// "fantasma" e estoura ORA-02391 antes do retry do seeder rodar.
OracleConnection.ClearAllPools();

// ===== Seed do admin (executa uma vez no startup) =====
await AdminSeeder.SeedAsync(app.Services);

// ===== Pipeline HTTP =====
// Swagger habilitado em TODAS as envs (inclusive Production) intencionalmente
// pra que a banca da GS consiga validar contratos REST e testar endpoints
// diretamente na URL pública. Em produto real isso ficaria atrás de auth.
// Precisa vir ANTES dos static files (UseDefaultFiles) pra evitar que o
// rewriter de defaults intercepte /swagger/ e retorne 404 antes da
// SwaggerUI middleware tratar a request.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Argus API v1");
    options.RoutePrefix = "swagger";
});

// ===== Landing page estática =====
// Serve wwwroot/index.html em "/" e libera /css/*, /images/* sem auth.
// UseDefaultFiles precisa vir ANTES de UseStaticFiles pra rewriting de "/"
// pra "/index.html" funcionar.
app.UseDefaultFiles();
app.UseStaticFiles();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// CORS precisa vir antes de UseAuthentication/UseAuthorization pra que o
// preflight OPTIONS não bata em 401 antes de a policy ser avaliada.
app.UseCors(CorsPolicyArgus);

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ===== Endpoint de Health Check com resposta JSON detalhada =====
// Público (sem auth) pra ferramentas de monitoring/load balancer conseguirem pingar.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = e.Value.Duration.TotalMilliseconds,
                error = e.Value.Exception?.Message
            })
        };
        await context.Response.WriteAsJsonAsync(payload);
    }
});

app.Run();
