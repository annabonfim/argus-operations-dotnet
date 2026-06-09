using Argus.Operations.API.Auth;
using Argus.Operations.API.DTOs.Ocorrencias;
using Argus.Operations.Domain.Entities;
using Argus.Operations.Domain.Enums;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OcorrenciasController : ControllerBase
{
    private readonly ArgusDbContext _context;
    private readonly OcorrenciaAuthorizationService _auth;

    // Injeção de dependência: DbContext + serviço de autorização granular.
    public OcorrenciasController(ArgusDbContext context, OcorrenciaAuthorizationService auth)
    {
        _context = context;
        _auth = auth;
    }

    // GET /api/ocorrencias → lista todas as ocorrências
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Ocorrencia>>> GetAll()
    {
        var ocorrencias = await _context.Ocorrencias.ToListAsync();
        return Ok(ocorrencias);
    }

    // GET /api/ocorrencias/7 → busca uma ocorrência pelo Id
    [HttpGet("{id}")]
    public async Task<ActionResult<Ocorrencia>> GetById(long id)
    {
        var ocorrencia = await _context.Ocorrencias.FindAsync(id);

        if (ocorrencia == null)
            return NotFound();

        return Ok(ocorrencia);
    }

    // POST /api/ocorrencias → cria uma ocorrência nova
    [HttpPost]
    [Authorize(Roles = Roles.AdminECoordenador)]
    public async Task<ActionResult<Ocorrencia>> Create(Ocorrencia ocorrencia)
    {
        _context.Ocorrencias.Add(ocorrencia);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = ocorrencia.Id },
            ocorrencia
        );
    }

    // PUT /api/ocorrencias/7 → atualiza uma ocorrência existente.
    // Só Admin/Coordenador editam a ocorrência em si; brigadista apenas
    // adiciona registros de campo a ela (ver RegistrosCampoController).
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.AdminECoordenador)]
    public async Task<IActionResult> Update(long id, Ocorrencia ocorrencia)
    {
        if (id != ocorrencia.Id)
            return BadRequest("O Id da URL não bate com o Id do corpo da requisição.");

        var existe = await _context.Ocorrencias.AnyAsync(o => o.Id == id);
        if (!existe)
            return NotFound();

        _context.Entry(ocorrencia).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // PATCH /api/ocorrencias/7/status → avança o status operacional.
    // Diferente do PUT: o brigadista PODE mexer aqui, desde que a ocorrência
    // seja da PRÓPRIA brigada (Admin/Coordenador mexem em qualquer uma). É a
    // ação de campo — marcar a ocorrência como EmAtendimento/Controlada/etc.
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> AtualizarStatus(long id, AtualizarStatusOcorrenciaRequest request)
    {
        var ocorrencia = await _context.Ocorrencias.FirstOrDefaultAsync(o => o.Id == id);
        if (ocorrencia == null)
            return NotFound();

        // Regra granular por brigada (mesma de RegistrosCampo).
        if (!await _auth.PodeEscreverNaBrigadaAsync(User, ocorrencia.BrigadaId))
            return Forbid();

        ocorrencia.Status = request.Status;

        // Ao finalizar, carimba a data de finalização; se reabrir, limpa.
        ocorrencia.DataFinalizacao = request.Status == StatusOcorrencia.Finalizada
            ? DateTime.UtcNow
            : null;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/ocorrencias/7 → remove uma ocorrência
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.AdminECoordenador)]
    public async Task<IActionResult> Delete(long id)
    {
        var ocorrencia = await _context.Ocorrencias.FindAsync(id);
        if (ocorrencia == null)
            return NotFound();

        _context.Ocorrencias.Remove(ocorrencia);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
