namespace Argus.Operations.API.Auth;

// Constantes string dos roles, pra usar em [Authorize(Roles = ...)] sem espalhar
// strings mágicas. Os valores precisam bater com PerfilUsuario.ToString() — é o
// que o TokenService injeta no claim ClaimTypes.Role do JWT.
public static class Roles
{
    public const string Admin = "Admin";
    public const string Coordenador = "Coordenador";
    public const string Brigadista = "Brigadista";

    // Combinações usadas em mais de um lugar
    public const string AdminECoordenador = Admin + "," + Coordenador;
}
