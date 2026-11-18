namespace GestionTurnos.Shared.CRYPTOGRAPHY
{
    public interface ICryptography
    {
        string Encriptar(string textoPlano);
        string Desencriptar(string textoCifrado);
    }
}
