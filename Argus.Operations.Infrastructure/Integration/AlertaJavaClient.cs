using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Argus.Operations.Application.Integration;

namespace Argus.Operations.Infrastructure.Integration;

// Client HTTP tipado pra API Java externa. A BaseAddress e o timeout são
// configurados na registração do HttpClient no Program.cs (vindos de JavaApi:BaseUrl).
public class AlertaJavaClient : IAlertaJavaClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
        // O detalhe vem como objeto plano + um campo "_links" do HATEOAS;
        // System.Text.Json ignora campos extras por padrão, então o AlertaDto
        // desserializa normalmente.
        return await response.Content.ReadFromJsonAsync<AlertaDto>(JsonOpts, ct);
    }

    public async Task<IReadOnlyList<AlertaDto>> ListarAsync(CancellationToken ct = default)
    {
        // A API Java passou a responder com HATEOAS (Spring HATEOAS):
        //   { "_embedded": { "alertaResponseDTOList": [ ... ] }, "_links": { ... } }
        // Em vez de tipar o wrapper inteiro, fazemos parse parcial: navega até
        // a coleção embutida e desserializa só o array. Robusto a futuras
        // mudanças no envelope HATEOAS (rels adicionados, etc.).
        using var stream = await _http.GetStreamAsync("/api/alertas", ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        // Compat com 2 formatos: legado (array direto) e HATEOAS (envelope).
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
            return doc.RootElement.Deserialize<List<AlertaDto>>(JsonOpts) ?? [];

        if (doc.RootElement.TryGetProperty("_embedded", out var embedded)
            && embedded.TryGetProperty("alertaResponseDTOList", out var lista)
            && lista.ValueKind == JsonValueKind.Array)
        {
            return lista.Deserialize<List<AlertaDto>>(JsonOpts) ?? [];
        }

        // Java retornou algo inesperado — devolve lista vazia em vez de explodir.
        return [];
    }
}
