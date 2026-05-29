using Argus.Operations.Domain.Entities;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecursosController : ControllerBase
{
    private readonly ArgusDbContext _context;

    // Injeção de dependência: o .NET nos entrega o DbContext via construtor
    public RecursosController(ArgusDbContext context)
    {
        _context = context;
    }

    // GET /api/recursos → lista todos os recursos
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Recurso>>> GetAll()
    {
        var recursos = await _context.Recursos.ToListAsync();
        return Ok(recursos);
    }

    // GET /api/recursos/7 → busca um recurso pelo Id
    [HttpGet("{id}")]
    public async Task<ActionResult<Recurso>> GetById(long id)
    {
        var recurso = await _context.Recursos.FindAsync(id);

        if (recurso == null)
            return NotFound();

        return Ok(recurso);
    }

    // POST /api/recursos → cria um recurso novo
    [HttpPost]
    public async Task<ActionResult<Recurso>> Create(Recurso recurso)
    {
        _context.Recursos.Add(recurso);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = recurso.Id },
            recurso
        );
    }

    // PUT /api/recursos/7 → atualiza um recurso existente
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, Recurso recurso)
    {
        if (id != recurso.Id)
            return BadRequest("O Id da URL não bate com o Id do corpo da requisição.");

        var existe = await _context.Recursos.AnyAsync(r => r.Id == id);
        if (!existe)
            return NotFound();

        _context.Entry(recurso).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE /api/recursos/7 → remove um recurso
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var recurso = await _context.Recursos.FindAsync(id);
        if (recurso == null)
            return NotFound();

        _context.Recursos.Remove(recurso);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
