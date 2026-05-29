using Argus.Operations.Domain.Entities;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly ArgusDbContext _context;

    // Injeção de dependência: o .NET nos entrega o DbContext via construtor
    public UsuariosController(ArgusDbContext context)
    {
        _context = context;
    }

    // GET /api/usuarios → lista todos os usuários
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Usuario>>> GetAll()
    {
        var usuarios = await _context.Usuarios.ToListAsync();
        return Ok(usuarios);
    }

    // GET /api/usuarios/7 → busca um usuário pelo Id
    [HttpGet("{id}")]
    public async Task<ActionResult<Usuario>> GetById(long id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);

        if (usuario == null)
            return NotFound();

        return Ok(usuario);
    }

    // POST /api/usuarios → cria um usuário novo (senha vira hash futuramente, quando entrar JWT)
    [HttpPost]
    public async Task<ActionResult<Usuario>> Create(Usuario usuario)
    {
        var emailJaExiste = await _context.Usuarios.AnyAsync(u => u.Email == usuario.Email);
        if (emailJaExiste)
            return Conflict("Já existe um usuário com este email.");

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = usuario.Id },
            usuario
        );
    }

    // PUT /api/usuarios/7 → atualiza um usuário existente
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, Usuario usuario)
    {
        if (id != usuario.Id)
            return BadRequest("O Id da URL não bate com o Id do corpo da requisição.");

        var existe = await _context.Usuarios.AnyAsync(u => u.Id == id);
        if (!existe)
            return NotFound();

        _context.Entry(usuario).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE /api/usuarios/7 → remove um usuário
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return NotFound();

        _context.Usuarios.Remove(usuario);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
