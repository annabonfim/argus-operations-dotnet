using Argus.Operations.API.Auth;
using Argus.Operations.API.DTOs.Ocorrencias;
using Argus.Operations.Application.Integration;
using Argus.Operations.Domain.Entities;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertasController : ControllerBase
{
    private readonly IAlertaJavaClient _javaClient;
    private readonly ArgusDbContext _context;

    public AlertasController(IAlertaJavaClient javaClient, ArgusDbContext context)
    {
        _javaClient = javaClient;
        _context = context;
    }

    // GET /api/alertas → lista os focos detectados (proxy pra API Java/FIRMS).
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertaDto>>> GetAll(CancellationToken ct)
    {
        var alertas = await _javaClient.ListarAsync(ct);
        return Ok(alertas);
    }

    // GET /api/alertas/7 → faz proxy pra API Java externa e devolve os dados do alerta.
    // O alerta original vive no domínio Java (detecção via satélite); aqui só lemos.
    [HttpGet("{id}")]
    public async Task<ActionResult<AlertaDto>> GetById(long id, CancellationToken ct)
    {
        var alerta = await _javaClient.BuscarPorIdAsync(id, ct);

        if (alerta == null)
            return NotFound();

        return Ok(alerta);
    }

    // POST /api/alertas/7/criar-ocorrencia → "promove" um alerta do Java a uma
    // ocorrência operacional aqui no .NET. O título/descrição são herdados do
    // alerta original; brigada/brigadista/lat/long vêm do mobile (GPS do
    // celular = mais preciso que o ponto de satélite). Mesma restrição de role
    // do POST /api/ocorrencias: só Admin/Coordenador despacha equipe.
    [HttpPost("{id}/criar-ocorrencia")]
    [Authorize(Roles = Roles.AdminECoordenador)]
    public async Task<ActionResult<Ocorrencia>> CriarOcorrencia(
        long id,
        CriarOcorrenciaDeAlertaRequest request,
        CancellationToken ct)
    {
        var alerta = await _javaClient.BuscarPorIdAsync(id, ct);
        if (alerta == null)
            return NotFound($"Alerta {id} não encontrado na API Java.");

        // Valida brigada/brigadista ANTES de mandar pro banco. Sem isso, FK
        // inválida vira ORA-02291 → 400 "referência inválida" (mensagem genérica
        // que não diz qual ID quebrou). Aqui devolvemos erro específico, melhor
        // pro mobile mostrar pro coordenador qual campo corrigir.
        var brigadaExiste = await _context.Brigadas.AnyAsync(b => b.Id == request.BrigadaId, ct);
        if (!brigadaExiste)
            return BadRequest($"Brigada {request.BrigadaId} não encontrada.");

        var brigadistaExiste = await _context.Brigadistas.AnyAsync(b => b.Id == request.BrigadistaId, ct);
        if (!brigadistaExiste)
            return BadRequest($"Brigadista {request.BrigadistaId} não encontrado.");

        var descricao = string.IsNullOrWhiteSpace(request.Descricao)
            ? MontarDescricaoPadrao(alerta)
            : request.Descricao;

        var ocorrencia = new Ocorrencia
        {
            Descricao = descricao,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            BrigadaId = request.BrigadaId,
            BrigadistaId = request.BrigadistaId,
            AlertaId = alerta.Id,
            DataAbertura = DateTime.UtcNow
        };

        _context.Ocorrencias.Add(ocorrencia);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(
            controllerName: "Ocorrencias",
            actionName: "GetById",
            routeValues: new { id = ocorrencia.Id },
            value: ocorrencia
        );
    }

    private static string MontarDescricaoPadrao(AlertaDto alerta)
    {
        var partes = new List<string> { alerta.Titulo };
        if (!string.IsNullOrWhiteSpace(alerta.Descricao))
            partes.Add(alerta.Descricao);
        if (!string.IsNullOrWhiteSpace(alerta.RecomendacaoOperacional))
            partes.Add($"Recomendação: {alerta.RecomendacaoOperacional}");
        return string.Join(" — ", partes);
    }
}
