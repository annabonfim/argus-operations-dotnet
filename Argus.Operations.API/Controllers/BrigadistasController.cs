using Argus.Operations.Domain.Entities;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrigadistasController : ControllerBase
{
    private readonly ArgusDbContext _context;

    // Injeção de dependência: o .NET nos entrega o DbContext via construtor
    public BrigadistasController(ArgusDbContext context)
    {
        _context = context;
    }

    // GET /api/brigadistas → lista todos os brigadistas
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Brigadista>>> GetAll()
    {
        var brigadistas = await _context.Brigadistas.ToListAsync();
        return Ok(brigadistas);
    }

    // GET /api/brigadistas/7 → busca um brigadista pelo Id
    [HttpGet("{id}")]
    public async Task<ActionResult<Brigadista>> GetById(long id)
    {
        var brigadista = await _context.Brigadistas.FindAsync(id);

        if (brigadista == null)
            return NotFound();

        return Ok(brigadista);
    }

    // POST /api/brigadistas → cria um brigadista novo
    [HttpPost]
    public async Task<ActionResult<Brigadista>> Create(Brigadista brigadista)
    {
        _context.Brigadistas.Add(brigadista);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = brigadista.Id },
            brigadista
        );
    }

    // PUT /api/brigadistas/7 → atualiza um brigadista existente
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, Brigadista brigadista)
    {
        if (id != brigadista.Id)
            return BadRequest("O Id da URL não bate com o Id do corpo da requisição.");

        var existe = await _context.Brigadistas.AnyAsync(b => b.Id == id);
        if (!existe)
            return NotFound();

        _context.Entry(brigadista).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE /api/brigadistas/7 → remove um brigadista
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var brigadista = await _context.Brigadistas.FindAsync(id);
        if (brigadista == null)
            return NotFound();

        _context.Brigadistas.Remove(brigadista);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
