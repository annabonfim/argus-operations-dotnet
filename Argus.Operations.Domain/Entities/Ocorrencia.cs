using Argus.Operations.Domain.Enums;

namespace Argus.Operations.Domain.Entities;

public class Ocorrencia
{
    public long Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public StatusOcorrencia Status { get; set; } = StatusOcorrencia.Aberta;
    public DateTime DataAbertura { get; set; }
    public DateTime? DataFinalizacao { get; set; }

    // Relacionamento: qual brigadista registrou esta ocorrência
    public long BrigadistaId { get; set; }
    public Brigadista? Brigadista { get; set; }
}