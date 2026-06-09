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
    private readonly OcorrenciaAuthorizationService _auth;

    // Injeção de dependência: o .NET nos entrega o DbContext e o serviço de
    // autorização granular via construtor.
    public RegistrosCampoController(ArgusDbContext context, OcorrenciaAuthorizationService auth)
    {
        _context = context;
        _auth = auth;
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
        var bloqueio = MapearBloqueio(
            await _auth.ChecarPorOcorrenciaIdAsync(User, registro.OcorrenciaId));
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
        var bloqueio = MapearBloqueio(
            await _auth.ChecarPorOcorrenciaIdAsync(User, existente.OcorrenciaId));
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

        var bloqueio = MapearBloqueio(
            await _auth.ChecarPorOcorrenciaIdAsync(User, registro.OcorrenciaId));
        if (bloqueio != null)
            return bloqueio;

        _context.RegistrosCampo.Remove(registro);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Traduz o resultado da regra de permissão em ActionResult de bloqueio.
    // Retorna null quando a operação pode prosseguir.
    private ActionResult? MapearBloqueio(ResultadoPermissao resultado) => resultado switch
    {
        ResultadoPermissao.Permitido => null,
        ResultadoPermissao.OcorrenciaNaoEncontrada => NotFound(),
        _ => Forbid()
    };
}
