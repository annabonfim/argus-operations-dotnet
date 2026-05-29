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
public class OcorrenciasController : ControllerBase
{
    private readonly ArgusDbContext _context;

    // Injeção de dependência: o .NET nos entrega o DbContext via construtor
    public OcorrenciasController(ArgusDbContext context)
    {
        _context = context;
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

    // PUT /api/ocorrencias/7 → atualiza uma ocorrência existente
    [HttpPut("{id}")]
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
