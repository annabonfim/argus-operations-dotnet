namespace Argus.Operations.Application.Integration;

public interface IAlertaJavaClient
{
    // Retorna null se a API Java responder 404 (alerta inexistente).
    // Lança HttpRequestException se a API estiver fora, ou TaskCanceledException
    // se exceder o timeout — ambos são traduzidos pra 503/504 pelo GlobalExceptionHandler.
    Task<AlertaDto?> BuscarPorIdAsync(long id, CancellationToken ct = default);

    // Lista os focos recentes detectados (proxy pra API Java/FIRMS).
    Task<IReadOnlyList<AlertaDto>> ListarAsync(CancellationToken ct = default);
}
