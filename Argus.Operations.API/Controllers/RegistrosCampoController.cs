using System.Security.Claims;
using Argus.Operations.API.Auth;
using Argus.Operations.Domain.Entities;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RegistrosCampoController : ControllerBase
{
    private readonly ArgusDbContext _context;

    // Injeção de dependência: o .NET nos entrega o DbContext via construtor
    public RegistrosCampoController(ArgusDbContext context)
    {
        _context = context;
    }

    // GET /api/registroscampo → lista todos os registros de campo
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RegistroCampo>>> GetAll()
    {
        var registros = await _context.RegistrosCampo.ToListAsync();
        return Ok(registros);
    }

    // GET /api/registroscampo/7 → busca um registro de campo pelo Id
    [HttpGet("{id}")]
    public async Task<ActionResult<RegistroCampo>> GetById(long id)
    {
        var registro = await _context.RegistrosCampo.FindAsync(id);

        if (registro == null)
            return NotFound();

        return Ok(registro);
    }

    // POST /api/registroscampo → cria um registro de campo novo.
    // Brigadista só pode criar registro em ocorrência da própria brigada.
    [HttpPost]
    public async Task<ActionResult<RegistroCampo>> Create(RegistroCampo registro)
    {
        var bloqueio = await ChecarPermissaoPorOcorrenciaAsync(registro.OcorrenciaId);
        if (bloqueio != null)
            return bloqueio;

        _context.RegistrosCampo.Add(registro);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = registro.Id },
            registro
        );
    }

    // PUT /api/registroscampo/7 → atualiza um registro de campo existente.
    // Brigadista só pode atualizar registro de ocorrência da própria brigada.
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, RegistroCampo registro)
    {
        if (id != registro.Id)
            return BadRequest("O Id da URL não bate com o Id do corpo da requisição.");

        var existente = await _context.RegistrosCampo.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
        if (existente == null)
            return NotFound();

        // Checa contra a ocorrência ORIGINAL do registro (não a do body) pra
        // evitar bypass: brigadista da brigada X mudando OcorrenciaId pra
        // forjar acesso a registro de outra brigada.
        var bloqueio = await ChecarPermissaoPorOcorrenciaAsync(existente.OcorrenciaId);
        if (bloqueio != null)
            return bloqueio;

        _context.Entry(registro).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE /api/registroscampo/7 → remove um registro de campo.
    // Brigadista só pode deletar registro de ocorrência da própria brigada.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var registro = await _context.RegistrosCampo.FindAsync(id);
        if (registro == null)
            return NotFound();

        var bloqueio = await ChecarPermissaoPorOcorrenciaAsync(registro.OcorrenciaId);
        if (bloqueio != null)
            return bloqueio;

        _context.RegistrosCampo.Remove(registro);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Regra: Admin/Coordenador escrevem qualquer registro. Brigadista só
    // escreve em ocorrências da PRÓPRIA brigada (Ocorrencia.BrigadaId ==
    // brigada do Brigadista vinculado ao Usuario logado).
    // Retorna null quando OK; retorna ActionResult de bloqueio (404 ou Forbid)
    // quando a operação não pode prosseguir.
    private async Task<ActionResult?> ChecarPermissaoPorOcorrenciaAsync(long ocorrenciaId)
    {
        // Admin/Coordenador passa direto — não filtra por brigada.
        if (User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Coordenador))
            return null;

        // Brigadista precisa estar vinculado a um Brigadista (entidade) pra
        // ter "brigada de atuação". Sem vínculo → não tem responsabilidade
        // operacional, só observa.
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(userIdClaim, out var userId))
            return Forbid();

        var usuario = await _context.Usuarios
            .Include(u => u.Brigadista)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (usuario?.Brigadista == null)
            return Forbid();

        var ocorrencia = await _context.Ocorrencias.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ocorrenciaId);
        if (ocorrencia == null)
            return NotFound();

        if (ocorrencia.BrigadaId != usuario.Brigadista.BrigadaId)
            return Forbid();

        return null;
    }
}
