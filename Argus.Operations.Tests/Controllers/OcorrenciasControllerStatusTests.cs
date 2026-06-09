using System.Security.Claims;
using Argus.Operations.API.Auth;
using Argus.Operations.API.Controllers;
using Argus.Operations.API.DTOs.Ocorrencias;
using Argus.Operations.Domain.Entities;
using Argus.Operations.Domain.Enums;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.Tests.Controllers;

// Cobre a regra granular por brigada no PATCH /api/ocorrencias/{id}/status.
// Brigadista avança o status SÓ da própria brigada; Admin/Coord qualquer uma.
// (A edição da ocorrência em si — PUT — continua Admin/Coord, coberta pelo
// atributo [Authorize(Roles=...)] no nível de framework.)
public class OcorrenciasControllerStatusTests
{
    private static ArgusDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ArgusDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static OcorrenciasController BuildController(
        ArgusDbContext db, long? userId, PerfilUsuario perfil)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, perfil.ToString()) };
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        return new OcorrenciasController(db, new OcorrenciaAuthorizationService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private record Seed(long OcorrenciaXId, long OcorrenciaYId, long UsuarioBrigadistaXId);

    // 2 brigadas (X e Y), 1 brigadista lotado em X, 1 ocorrência em cada,
    // 1 usuário Brigadista vinculado ao brigadista da X.
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
            BrigadistaId = brigadistaX.Id
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

        return new Seed(ocoX.Id, ocoY.Id, usuarioBrigX.Id);
    }

    [Fact]
    public async Task AtualizarStatus_BrigadistaNaPropriaBrigada_RetornaNoContentEPersiste()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);
        var controller = BuildController(db, seed.UsuarioBrigadistaXId, PerfilUsuario.Brigadista);

        var result = await controller.AtualizarStatus(
            seed.OcorrenciaXId, new AtualizarStatusOcorrenciaRequest(StatusOcorrencia.EmAtendimento));

        Assert.IsType<NoContentResult>(result);
        var oco = await db.Ocorrencias.FindAsync(seed.OcorrenciaXId);
        Assert.Equal(StatusOcorrencia.EmAtendimento, oco!.Status);
    }

    [Fact]
    public async Task AtualizarStatus_BrigadistaEmOcorrenciaDeOutraBrigada_RetornaForbid()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);
        var controller = BuildController(db, seed.UsuarioBrigadistaXId, PerfilUsuario.Brigadista);

        var result = await controller.AtualizarStatus(
            seed.OcorrenciaYId, new AtualizarStatusOcorrenciaRequest(StatusOcorrencia.EmAtendimento));

        Assert.IsType<ForbidResult>(result);
        var oco = await db.Ocorrencias.FindAsync(seed.OcorrenciaYId);
        Assert.Equal(StatusOcorrencia.Aberta, oco!.Status);  // intacta
    }

    [Fact]
    public async Task AtualizarStatus_AdminEmQualquerBrigada_RetornaNoContent()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);
        var controller = BuildController(db, userId: null, perfil: PerfilUsuario.Admin);

        var result = await controller.AtualizarStatus(
            seed.OcorrenciaYId, new AtualizarStatusOcorrenciaRequest(StatusOcorrencia.Controlada));

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task AtualizarStatus_Finalizada_CarimbaDataFinalizacao()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);
        var controller = BuildController(db, seed.UsuarioBrigadistaXId, PerfilUsuario.Brigadista);

        var result = await controller.AtualizarStatus(
            seed.OcorrenciaXId, new AtualizarStatusOcorrenciaRequest(StatusOcorrencia.Finalizada));

        Assert.IsType<NoContentResult>(result);
        var oco = await db.Ocorrencias.FindAsync(seed.OcorrenciaXId);
        Assert.Equal(StatusOcorrencia.Finalizada, oco!.Status);
        Assert.NotNull(oco.DataFinalizacao);
    }

    [Fact]
    public async Task AtualizarStatus_BrigadistaSemVinculo_RetornaForbid()
    {
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
            BrigadistaId = null
        };
        db.Usuarios.Add(usuarioSemVinculo);
        await db.SaveChangesAsync();

        var controller = BuildController(db, usuarioSemVinculo.Id, PerfilUsuario.Brigadista);

        var result = await controller.AtualizarStatus(
            seed.OcorrenciaXId, new AtualizarStatusOcorrenciaRequest(StatusOcorrencia.EmAtendimento));

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task AtualizarStatus_OcorrenciaInexistente_RetornaNotFound()
    {
        await using var db = NewDb();
        var seed = await SeedAsync(db);
        var controller = BuildController(db, seed.UsuarioBrigadistaXId, PerfilUsuario.Brigadista);

        var result = await controller.AtualizarStatus(
            99999, new AtualizarStatusOcorrenciaRequest(StatusOcorrencia.EmAtendimento));

        Assert.IsType<NotFoundResult>(result);
    }
}
