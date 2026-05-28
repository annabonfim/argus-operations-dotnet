namespace Argus.Operations.Domain.Entities;

public class Brigadista
{
    public long Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Matricula { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Funcao { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime DataAdmissao { get; set; }
}