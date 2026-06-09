namespace Argus.Operations.Domain.Entities;

public class RegistroCampo
{
    public long Id { get; set; }
    public string Observacao { get; set; } = string.Empty;
    public string? UrlFoto { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime DataRegistro { get; set; }

    // Relacionamento: a qual ocorrência este registro pertence
    public long OcorrenciaId { get; set; }
    public Ocorrencia? Ocorrencia { get; set; }
}