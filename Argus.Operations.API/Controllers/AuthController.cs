using Argus.Operations.API.Auth;
using Argus.Operations.API.DTOs.Auth;
using Argus.Operations.Application.Auth;
using Argus.Operations.Domain.Entities;
using Argus.Operations.Domain.Enums;
using Argus.Operations.Infrastructure.Auth;
using Argus.Operations.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Argus.Operations.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ArgusDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtSettings _jwtSettings;
    private readonly string _codigoConviteEsperado;

    public AuthController(
        ArgusDbContext context,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IOptions<JwtSettings> jwtSettings,
        IConfiguration configuration)
    {
        _context = context;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _jwtSettings = jwtSettings.Value;
        _codigoConviteEsperado = configuration["Auth:CodigoConvite"] ?? string.Empty;
    }

    // POST /api/auth/login → autentica e devolve JWT
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (usuario == null || !_passwordHasher.Verify(request.Senha, usuario.SenhaHash))
            return Unauthorized("Email ou senha inválidos.");

        if (!usuario.Ativo)
            return Unauthorized("Usuário desativado.");

        usuario.UltimoLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(BuildResponse(usuario));
    }

    // POST /api/auth/register → cria novo Brigadista (exige código de convite)
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (!string.Equals(request.CodigoConvite, _codigoConviteEsperado, StringComparison.Ordinal))
            return Forbid();

        var emailJaExiste = await _context.Usuarios.AnyAsync(u => u.Email == request.Email);
        if (emailJaExiste)
            return Conflict("Já existe um usuário com este email.");

        var usuario = new Usuario
        {
            Nome = request.Nome,
            Email = request.Email,
            SenhaHash = _passwordHasher.Hash(request.Senha),
            Perfil = PerfilUsuario.Brigadista,
            Ativo = true,
            DataCriacao = DateTime.UtcNow
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Login), BuildResponse(usuario));
    }

    // GET /api/auth/me → devolve o usuário do token atual (útil pro mobile e pra debug de auth)
    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me()
    {
        return Ok(new
        {
            IsAuthenticated = User.Identity?.IsAuthenticated,
            Name = User.Identity?.Name,
            Role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
            IsInRoleAdmin = User.IsInRole(Roles.Admin),
            IsInRoleCoordenador = User.IsInRole(Roles.Coordenador),
            IsInRoleBrigadista = User.IsInRole(Roles.Brigadista),
            Claims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }

    private AuthResponse BuildResponse(Usuario usuario)
    {
        var token = _tokenService.GenerateToken(usuario);
        var expiraEm = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);
        var usuarioResponse = new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Email, usuario.Perfil);
        return new AuthResponse(token, expiraEm, usuarioResponse);
    }
}
