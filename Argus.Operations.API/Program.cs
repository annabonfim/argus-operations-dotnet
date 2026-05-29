using System.Security.Claims;
using System.Text;
using Argus.Operations.Application.Auth;
using Argus.Operations.Infrastructure.Auth;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ===== Serviços =====
builder.Services.AddControllers();

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

app.Run();
