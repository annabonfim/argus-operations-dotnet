using Argus.Operations.Application.Integration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Operations.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertasController : ControllerBase
{
    private readonly IAlertaJavaClient _javaClient;

    public AlertasController(IAlertaJavaClient javaClient)
    {
        _javaClient = javaClient;
    }

    // GET /api/alertas → lista os focos detectados (proxy pra API Java/FIRMS).
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertaDto>>> GetAll(CancellationToken ct)
    {
        var alertas = await _javaClient.ListarAsync(ct);
        return Ok(alertas);
    }

    // GET /api/alertas/7 → faz proxy pra API Java externa e devolve os dados do alerta.
    // O alerta original vive no domínio Java (detecção via satélite); aqui só lemos.
    [HttpGet("{id}")]
    public async Task<ActionResult<AlertaDto>> GetById(long id, CancellationToken ct)
    {
        var alerta = await _javaClient.BuscarPorIdAsync(id, ct);

        if (alerta == null)
            return NotFound();

        return Ok(alerta);
    }
}
