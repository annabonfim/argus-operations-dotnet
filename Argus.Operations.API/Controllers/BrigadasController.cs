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
public class BrigadasController : ControllerBase
{
    private readonly ArgusDbContext _context;

    // Injeção de dependência: o .NET nos entrega o DbContext via construtor
    public BrigadasController(ArgusDbContext context)
    {
        _context = context;
    }

    // GET /api/brigadas → lista todas as brigadas
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Brigada>>> GetAll()
    {
        var brigadas = await _context.Brigadas.ToListAsync();
        return Ok(brigadas);
    }

    // GET /api/brigadas/7 → busca uma brigada pelo Id
    [HttpGet("{id}")]
    public async Task<ActionResult<Brigada>> GetById(long id)
    {
        var brigada = await _context.Brigadas.FindAsync(id);

        if (brigada == null)
            return NotFound();

        return Ok(brigada);
    }

    // POST /api/brigadas → cria uma brigada nova
    [HttpPost]
    [Authorize(Roles = Roles.AdminECoordenador)]
    public async Task<ActionResult<Brigada>> Create(Brigada brigada)
    {
        _context.Brigadas.Add(brigada);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = brigada.Id },
            brigada
        );
    }

    // PUT /api/brigadas/7 → atualiza uma brigada existente
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.AdminECoordenador)]
    public async Task<IActionResult> Update(long id, Brigada brigada)
    {
        if (id != brigada.Id)
            return BadRequest("O Id da URL não bate com o Id do corpo da requisição.");

        var existe = await _context.Brigadas.AnyAsync(b => b.Id == id);
        if (!existe)
            return NotFound();

        _context.Entry(brigada).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE /api/brigadas/7 → remove uma brigada
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.AdminECoordenador)]
    public async Task<IActionResult> Delete(long id)
    {
        var brigada = await _context.Brigadas.FindAsync(id);
        if (brigada == null)
            return NotFound();

        _context.Brigadas.Remove(brigada);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}