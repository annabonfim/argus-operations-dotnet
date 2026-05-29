using Argus.Operations.Domain.Entities;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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

    // POST /api/registroscampo → cria um registro de campo novo
    [HttpPost]
    public async Task<ActionResult<RegistroCampo>> Create(RegistroCampo registro)
    {
        _context.RegistrosCampo.Add(registro);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = registro.Id },
            registro
        );
    }

    // PUT /api/registroscampo/7 → atualiza um registro de campo existente
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, RegistroCampo registro)
    {
        if (id != registro.Id)
            return BadRequest("O Id da URL não bate com o Id do corpo da requisição.");

        var existe = await _context.RegistrosCampo.AnyAsync(r => r.Id == id);
        if (!existe)
            return NotFound();

        _context.Entry(registro).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE /api/registroscampo/7 → remove um registro de campo
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var registro = await _context.RegistrosCampo.FindAsync(id);
        if (registro == null)
            return NotFound();

        _context.RegistrosCampo.Remove(registro);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
