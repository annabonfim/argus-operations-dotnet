using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.API.Exceptions;

// Captura qualquer exceção não tratada e devolve um ProblemDetails consistente.
// Mapeia os erros Oracle mais comuns pra status HTTP semânticos — sem isso,
// FK violation ou unique constraint vinham como 500 cru com stack trace exposta.
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = MapException(exception);

        _logger.LogError(exception, "{Title} em {Path}", title, httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.io/{status}",
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static (int status, string title, string detail) MapException(Exception ex)
    {
        // Chamadas a APIs externas via HttpClient (ex.: API Java de alertas) —
        // 503 quando o serviço está fora, 504 quando excede o timeout configurado.
        if (ex is HttpRequestException)
            return (StatusCodes.Status503ServiceUnavailable,
                "API externa indisponível",
                "Não foi possível consultar a API externa (Java). Tente novamente em instantes.");

        if (ex is TaskCanceledException)
            return (StatusCodes.Status504GatewayTimeout,
                "Timeout em API externa",
                "Excedido o tempo limite ao consultar a API externa (Java).");

        // Erros de conexão Oracle podem vir em qualquer tipo de exception (não só
        // DbUpdateException) — checa toda a cadeia de InnerException antes de
        // tudo. 503 sinaliza "tente de novo", diferente de 500 (bug nosso).
        if (ContemCodigoOracle(ex, "ORA-12541", "ORA-12545", "ORA-12170"))
            return (StatusCodes.Status503ServiceUnavailable,
                "Banco indisponível",
                "Não foi possível conectar ao banco Oracle. Tente novamente em instantes.");

        // Erros de integridade vêm encapsulados em DbUpdateException → InnerException.Message
        // tem o código ORA-xxxxx. String matching porque o tipo Oracle vive na
        // Infrastructure e não queremos referência circular aqui.
        if (ex is DbUpdateException dbEx)
        {
            var msg = dbEx.InnerException?.Message ?? string.Empty;

            if (msg.Contains("ORA-00001"))
                return (StatusCodes.Status409Conflict,
                    "Registro duplicado",
                    "Já existe um registro com algum dos campos únicos (ex.: email).");

            if (msg.Contains("ORA-02291"))
                return (StatusCodes.Status400BadRequest,
                    "Referência inválida",
                    "Um dos IDs informados aponta pra um registro que não existe.");

            if (msg.Contains("ORA-02292"))
                return (StatusCodes.Status409Conflict,
                    "Registro com dependências",
                    "Esse registro está sendo referenciado por outros e não pode ser removido.");

            return (StatusCodes.Status500InternalServerError,
                "Erro ao salvar no banco",
                dbEx.InnerException?.Message ?? dbEx.Message);
        }

        return (StatusCodes.Status500InternalServerError,
            "Erro interno",
            ex.Message);
    }

    private static bool ContemCodigoOracle(Exception ex, params string[] codigos)
    {
        var atual = ex;
        while (atual != null)
        {
            if (codigos.Any(c => atual.Message.Contains(c, StringComparison.Ordinal)))
                return true;
            atual = atual.InnerException;
        }
        return false;
    }
}
