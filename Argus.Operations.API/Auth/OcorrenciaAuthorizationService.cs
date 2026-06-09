using System.Security.Claims;
using Argus.Operations.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.API.Auth;

// Resultado da checagem de permissão granular por brigada.
public enum ResultadoPermissao
{
    Permitido,
    Negado,
    OcorrenciaNaoEncontrada
}

// Centraliza a regra de autorização "data-dependent" por brigada, reusada por
// quem escreve recursos atrelados a uma ocorrência (registros de campo e o
// avanço de status da própria ocorrência).
//
// Regra: Admin/Coordenador escrevem em qualquer brigada. Brigadista só escreve
// em ocorrências da PRÓPRIA brigada — o vínculo Usuario→Brigadista→Brigada é
// resolvido por SELECT no banco a cada request (o JWT não carrega BrigadistaId).
public class OcorrenciaAuthorizationService
{
    private readonly ArgusDbContext _context;

    public OcorrenciaAuthorizationService(ArgusDbContext context)
    {
        _context = context;
    }

    // Núcleo da regra: o usuário pode escrever em recursos da brigada informada?
    // Usar quando a brigada já é conhecida (ex.: ocorrência já carregada).
    public async Task<bool> PodeEscreverNaBrigadaAsync(ClaimsPrincipal user, long brigadaId)
    {
        // Admin/Coordenador passam direto — não filtra por brigada.
        if (user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Coordenador))
            return true;

        // Brigadista precisa estar vinculado a um Brigadista (entidade) pra ter
        // "brigada de atuação". Sem vínculo → só observa, não escreve.
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(userIdClaim, out var userId))
            return false;

        var usuario = await _context.Usuarios
            .Include(u => u.Brigadista)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (usuario?.Brigadista == null)
            return false;

        return usuario.Brigadista.BrigadaId == brigadaId;
    }

    // Variante por ocorrenciaId (usada onde só se tem o id do recurso). Resolve a
    // brigada da ocorrência antes de aplicar a regra. Mantém a assimetria do
    // comportamento original: Admin/Coord passam SEM checar existência; brigadista
    // tem o vínculo checado antes da existência da ocorrência.
    public async Task<ResultadoPermissao> ChecarPorOcorrenciaIdAsync(ClaimsPrincipal user, long ocorrenciaId)
    {
        if (user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Coordenador))
            return ResultadoPermissao.Permitido;

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(userIdClaim, out var userId))
            return ResultadoPermissao.Negado;

        var usuario = await _context.Usuarios
            .Include(u => u.Brigadista)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (usuario?.Brigadista == null)
            return ResultadoPermissao.Negado;

        var ocorrencia = await _context.Ocorrencias.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ocorrenciaId);
        if (ocorrencia == null)
            return ResultadoPermissao.OcorrenciaNaoEncontrada;

        return ocorrencia.BrigadaId == usuario.Brigadista.BrigadaId
            ? ResultadoPermissao.Permitido
            : ResultadoPermissao.Negado;
    }
}
