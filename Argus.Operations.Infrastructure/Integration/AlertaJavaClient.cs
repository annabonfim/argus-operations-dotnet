using System.Net;
using System.Net.Http.Json;
using Argus.Operations.Application.Integration;

namespace Argus.Operations.Infrastructure.Integration;

// Client HTTP tipado pra API Java externa. A BaseAddress e o timeout são
// configurados na registração do HttpClient no Program.cs (vindos de JavaApi:BaseUrl).
public class AlertaJavaClient : IAlertaJavaClient
{
    private readonly HttpClient _http;

    public AlertaJavaClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<AlertaDto?> BuscarPorIdAsync(long id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/alertas/{id}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AlertaDto>(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<AlertaDto>> ListarAsync(CancellationToken ct = default)
    {
        var alertas = await _http.GetFromJsonAsync<List<AlertaDto>>("/api/alertas", ct);
        return alertas ?? [];
    }
}
