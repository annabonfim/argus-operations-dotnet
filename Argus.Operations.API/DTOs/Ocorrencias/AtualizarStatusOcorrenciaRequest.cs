using System.ComponentModel.DataAnnotations;
using Argus.Operations.Domain.Enums;

namespace Argus.Operations.API.DTOs.Ocorrencias;

// Payload do PATCH /api/ocorrencias/{id}/status.
// Avança o status operacional da ocorrência (Aberta → EmAtendimento →
// Controlada → Finalizada). É a ação que o brigadista em campo faz na
// ocorrência da própria brigada — separada da edição da ocorrência em si,
// que continua restrita a Admin/Coordenador.
public record AtualizarStatusOcorrenciaRequest(
    [Required][EnumDataType(typeof(StatusOcorrencia))] StatusOcorrencia Status
);
