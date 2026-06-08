using System.Security.Claims;
using Argus.Operations.API.Auth;
using Argus.Operations.API.Controllers;
using Argus.Operations.Domain.Entities;
using Argus.Operations.Domain.Enums;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.Tests.Controllers;

// Cobre a regra de autorização granular por brigada nos endpoints de escrita
// (Create/Update/Delete). Admin/Coord passam livres; Brigadista só escreve em
// ocorrências da própria brigada — senão Forbid.
public class RegistrosCampoControllerTests
{
    private static ArgusDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ArgusDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static RegistrosCampoController BuildController(
        ArgusDbContext db, long? userId, PerfilUsuario perfil)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, perfil.ToString())
        };
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var controller = new RegistrosCampoController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
        return controller;
    }

    private record Seed(
        long BrigadaXId,
        long BrigadaYId,
        long BrigadistaXId,
        long OcorrenciaXId,
        long OcorrenciaYId,
        long UsuarioBrigadistaXId
    );

    // Monta cenário: 2 brigadas (X e Y), 1 brigadista lotado em X, 1 ocorrência
    // em cada brigada, 1 usuário com perfil Brigadista vinculado ao brigadista da X.
    private static async Task<Seed> SeedAsync(ArgusDbContext db)
    {
        var brigadaX = new Brigada { Nome = "Brigada X", BaseOperacional = "SP", Telefone = "1111", Ativa = true };
        var brigadaY = new Brigada { Nome = "Brigada Y", BaseOperacional = "RJ", Telefone = "2222", Ativa = true };
        db.Brigadas.AddRange(brigadaX, brigadaY);
        await db.SaveChangesAsync();

        var brigadistaX = new Brigadista
        {
            Nome = "Brigadista X",
            Matricula = "X-01",
            Email = "x@argus.com",
            Telefone = "3333",
            Funcao = "Combatente",
            Ativo = true,
            DataAdmissao = new DateTime(2024, 1, 1),
            BrigadaId = brigadaX.Id
        };
        db.Brigadistas.Add(brigadistaX);
        await db.SaveChangesAsync();

        var ocoX = new Ocorrencia
        {
            Descricao = "Foco brigada X",
            Latitude = -23.5,
            Longitude = -46.6,
            Status = StatusOcorrencia.Aberta,
            DataAbertura = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            BrigadaId = brigadaX.Id,
            BrigadistaId = brigadistaX.Id
        };
        var ocoY = new Ocorrencia
        {
            Descricao = "Foco brigada Y",
            Latitude = -22.9,
            Longitude = -43.2,
            Status = StatusOcorrencia.Aberta,
            DataAbertura = new DateTime(2026, 6, 1, 11, 0, 0, DateTimeKind.Utc),
            BrigadaId = brigadaY.Id,
            BrigadistaId = brigadistaX.Id  // detalhe operacional irrelevante pro teste
        };
        db.Ocorrencias.AddRange(ocoX, ocoY);
        await db.SaveChangesAsync();

        var usuarioBrigX = new Usuario
        {
            Nome = "User Brigadista X",
            Email = "userx@argus.com",
            Telefone = "9999",
            SenhaHash = "hash",
            Perfil = PerfilUsuario.Brigadista,
            Ativo = true,
            DataCriacao = DateTime.UtcNow,
            BrigadistaId = brigadistaX.Id
        };
        db.Usuarios.Add(usuarioBrigX);
        await db.SaveChangesAsync();

        return new Seed(
            brigadaX.Id, brigadaY.Id, brigadistaX.Id,
            ocoX.Id, ocoY.Id, usuarioBrigX.Id
        );
    }

    private static RegistroCampo NewRegistro(long ocorrenciaId, long? id = null) => new()
    {
        Id = id ?? 0,
        Observacao = "Teste",
        UrlFoto = "https://x.com/f.jpg",
        Latitude = -23.5,
        Longitude = -46.6,
        DataRegistro = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc),
        OcorrenciaId = ocorrenciaId
    };

    // ===================== CREATE =====================

    [Fact]
    public async Task Create_BrigadistaNaMesmaBrigadaDaOcorrencia_RetornaCreated()
    {
        // Arrange
        await using var db = NewDb();
        var seed = await SeedAsync(db);
        var controller = BuildController(db, seed.UsuarioBrigadistaXId, PerfilUsuario.Brigadista);

        // Act
        var result = await controller.Create(NewRegistro(seed.OcorrenciaXId));

        // Assert
        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(1, await db.RegistrosCampo.CountAsync());
    }

    [Fact]
    public async Task Create_BrigadistaEmOcorrenciaDeOutraBrigada_RetornaForbid()
    {
        // Arrange
        await using var db = NewDb();
        var seed = await SeedAsync(db);
        var controller = BuildController(db, seed.UsuarioBrigadistaXId, PerfilUsuario.Brigadista);

        // Act — tenta criar registro de ocorrência da Brigada Y (não dele)
        var result = await controller.Create(NewRegistro(seed.OcorrenciaYId));

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        Assert.Equal(0, await db.RegistrosCampo.CountAsync());
    }

    [Fact]
    public async Task Create_AdminEmQualquerBrigada_RetornaCreated()
    {
        // Arrange
        await using var db = NewDb();
        var seed = await SeedAsync(db);
        // Admin: passa userId=null porque a regra nem consulta o usuário pra Admin/Coord
        var controller = BuildController(db, userId: null, perfil: PerfilUsuario.Admin);

        // Act
        var result = await controller.Create(NewRegistro(seed.OcorrenciaYId));

        // Assert
        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(1, await db.RegistrosCampo.CountAsync());
    }

    [Fact]
    public async Task Create_CoordenadorEmQualquerBrigada_RetornaCreated()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);
        var controller = BuildController(db, userId: null, perfil: PerfilUsuario.Coordenador);

        var result = await controller.Create(NewRegistro(seed.OcorrenciaYId));

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task Create_BrigadistaSemVinculo_RetornaForbid()
    {
        // Arrange — cria um Usuario brigadista SEM BrigadistaId (auto-cadastro
        // que não conseguiu vincular por email). Operacionalmente "voluntário
        // pré-vinculação" — pode logar mas não pode escrever registros.
        await using var db = NewDb();
        var seed = await SeedAsync(db);

        var usuarioSemVinculo = new Usuario
        {
            Nome = "Voluntário Sem Vínculo",
            Email = "sv@argus.com",
            Telefone = "0",
            SenhaHash = "hash",
            Perfil = PerfilUsuario.Brigadista,
            Ativo = true,
            DataCriacao = DateTime.UtcNow,
            BrigadistaId = null  // <-- ponto chave
        };
        db.Usuarios.Add(usuarioSemVinculo);
        await db.SaveChangesAsync();

        var controller = BuildController(db, usuarioSemVinculo.Id, PerfilUsuario.Brigadista);

        // Act
        var result = await controller.Create(NewRegistro(seed.OcorrenciaXId));

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    // ===================== UPDATE =====================

    [Fact]
    public async Task Update_BrigadistaNaMesmaBrigadaDaOcorrencia_RetornaNoContent()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);

        // Cria registro existente vinculado à ocorrência X
        var existente = NewRegistro(seed.OcorrenciaXId);
        db.RegistrosCampo.Add(existente);
        await db.SaveChangesAsync();
        // EF Core InMemory rastreia a entidade; o controller dá Entry().State = Modified
        // num objeto novo com o mesmo Id, então precisamos destacar a entidade.
        db.Entry(existente).State = EntityState.Detached;

        var controller = BuildController(db, seed.UsuarioBrigadistaXId, PerfilUsuario.Brigadista);

        var atualizado = NewRegistro(seed.OcorrenciaXId, id: existente.Id);
        atualizado.Observacao = "Atualizado";

        var result = await controller.Update(existente.Id, atualizado);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Update_BrigadistaEmRegistroDeOutraBrigada_RetornaForbid()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);

        // Registro existente VINCULADO À OCORRÊNCIA DE OUTRA BRIGADA (Y)
        var existente = NewRegistro(seed.OcorrenciaYId);
        db.RegistrosCampo.Add(existente);
        await db.SaveChangesAsync();
        db.Entry(existente).State = EntityState.Detached;

        var controller = BuildController(db, seed.UsuarioBrigadistaXId, PerfilUsuario.Brigadista);

        // Tenta atualizar (mesmo que o body tente colocar OcorrenciaId da X,
        // a regra checa contra o REGISTRO EXISTENTE no banco — anti-bypass)
        var tentativa = NewRegistro(seed.OcorrenciaXId, id: existente.Id);
        var result = await controller.Update(existente.Id, tentativa);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Update_AdminEmQualquerBrigada_RetornaNoContent()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);

        var existente = NewRegistro(seed.OcorrenciaYId);
        db.RegistrosCampo.Add(existente);
        await db.SaveChangesAsync();
        db.Entry(existente).State = EntityState.Detached;

        var controller = BuildController(db, userId: null, perfil: PerfilUsuario.Admin);

        var atualizado = NewRegistro(seed.OcorrenciaYId, id: existente.Id);
        var result = await controller.Update(existente.Id, atualizado);

        Assert.IsType<NoContentResult>(result);
    }

    // ===================== DELETE =====================

    [Fact]
    public async Task Delete_BrigadistaNaMesmaBrigadaDaOcorrencia_RetornaNoContent()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);

        var registro = NewRegistro(seed.OcorrenciaXId);
        db.RegistrosCampo.Add(registro);
        await db.SaveChangesAsync();

        var controller = BuildController(db, seed.UsuarioBrigadistaXId, PerfilUsuario.Brigadista);

        var result = await controller.Delete(registro.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, await db.RegistrosCampo.CountAsync());
    }

    [Fact]
    public async Task Delete_BrigadistaEmRegistroDeOutraBrigada_RetornaForbid()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);

        var registro = NewRegistro(seed.OcorrenciaYId);
        db.RegistrosCampo.Add(registro);
        await db.SaveChangesAsync();

        var controller = BuildController(db, seed.UsuarioBrigadistaXId, PerfilUsuario.Brigadista);

        var result = await controller.Delete(registro.Id);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(1, await db.RegistrosCampo.CountAsync());  // permaneceu
    }

    [Fact]
    public async Task Delete_AdminEmQualquerBrigada_RetornaNoContent()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);

        var registro = NewRegistro(seed.OcorrenciaYId);
        db.RegistrosCampo.Add(registro);
        await db.SaveChangesAsync();

        var controller = BuildController(db, userId: null, perfil: PerfilUsuario.Admin);

        var result = await controller.Delete(registro.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, await db.RegistrosCampo.CountAsync());
    }

    // ===================== NÃO ENCONTRADOS =====================

    [Fact]
    public async Task Create_OcorrenciaInexistente_BrigadistaRetornaNotFound()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);
        var controller = BuildController(db, seed.UsuarioBrigadistaXId, PerfilUsuario.Brigadista);

        var result = await controller.Create(NewRegistro(ocorrenciaId: 99999));

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
