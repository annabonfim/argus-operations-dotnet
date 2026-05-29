using Argus.Operations.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;


var builder = WebApplication.CreateBuilder(args);

// ===== Serviços =====
builder.Services.AddControllers();

// ===== Swagger/OpenAPI =====
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

var app = builder.Build();

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
app.UseAuthorization();
app.MapControllers();

app.Run();