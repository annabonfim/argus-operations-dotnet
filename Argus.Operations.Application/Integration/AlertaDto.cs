namespace Argus.Operations.Application.Integration;

// Representa um alerta de incêndio originado da API Java (detecção via satélite).
// Os nomes dos campos são suposições do contrato — ajustar quando o time do Java
// confirmar o formato real.
public record AlertaDto(
    long Id,
    double Latitude,
    double Longitude,
    string? Severidade,
    DateTime DataDeteccao,
    string? Status
);
