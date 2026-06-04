using System.Security.Claims;
using System.Text;
using Argus.Operations.API.Exceptions;
using Argus.Operations.Application.Auth;
using Argus.Operations.Application.Integration;
using Argus.Operations.Infrastructure.Auth;
using Argus.Operations.Infrastructure.Integration;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ===== Serviços =====
builder.Services.AddControllers();

// ===== Tratamento global de exceções =====
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ===== Health Checks (app + Oracle via DbContext) =====
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ArgusDbContext>(name: "oracle-db");

// ===== Cliente HTTP tipado pra API Java (alertas via satélite) =====
// URL base configurável em appsettings → JavaApi:BaseUrl (override por user-secrets ou env).
builder.Services.AddHttpClient<IAlertaJavaClient, AlertaJavaClient>(client =>
{
    var baseUrl = builder.Configuration["JavaApi:BaseUrl"] ?? "http://localhost:8080";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

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

var app = builder.Build();

// ===== Tratamento global de exceções (precisa vir antes de tudo no pipeline) =====
app.UseExceptionHandler();

// ===== Seed do admin (executa uma vez no startup) =====
await AdminSeeder.SeedAsync(app.Services);

// ===== Pipeline HTTP =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Argus API v1");
        options.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

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
