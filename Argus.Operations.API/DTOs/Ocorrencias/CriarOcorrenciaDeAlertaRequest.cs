using System.ComponentModel.DataAnnotations;

namespace Argus.Operations.API.DTOs.Ocorrencias;

// Payload do POST /api/alertas/{id}/criar-ocorrencia.
// O mobile envia só o mínimo: quem vai atender + onde (GPS do celular).
// O resto (título/descrição) é herdado do alerta no servidor.
// Descrição é opcional — se vier vazia, o servidor monta uma a partir
// do alerta original (título + descrição + recomendação operacional).
public record CriarOcorrenciaDeAlertaRequest(
    [Required] long BrigadaId,
    [Required] long BrigadistaId,
    [Required, Range(-90, 90)] double Latitude,
    [Required, Range(-180, 180)] double Longitude,
    [MaxLength(500)] string? Descricao
);
