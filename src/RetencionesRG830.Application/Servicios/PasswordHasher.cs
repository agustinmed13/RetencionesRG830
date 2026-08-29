using System.Security.Cryptography;

namespace RetencionesRG830.Application.Servicios;

/// <summary>
/// Convierte una contraseña en un "hash" seguro para guardar en la base de
/// datos, y permite comprobar después si una contraseña ingresada coincide
/// -sin necesitar guardar nunca la contraseña real. Usa PBKDF2, un algoritmo
/// pensado específicamente para contraseñas: a diferencia de un hash común,
/// es lento a propósito, para que probar contraseñas por fuerza bruta salga
/// caro.
/// </summary>
public static class PasswordHasher
{
    private const int TamanioSalt = 16;
    private const int TamanioHash = 32;
    private const int Iteraciones = 100_000;

    public static string Hash(string password)
    {
        // La "sal" es un valor aleatorio distinto para cada contraseña, para
        // que dos usuarios con la misma contraseña no queden con el mismo
        // hash guardado.
        byte[] salt = RandomNumberGenerator.GetBytes(TamanioSalt);

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iteraciones, HashAlgorithmName.SHA256, TamanioHash);

        return $"{Iteraciones}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string password, string hashGuardado)
    {
        var partes = hashGuardado.Split('.');
        if (partes.Length != 3)
        {
            return false;
        }

        int iteraciones = int.Parse(partes[0]);
        byte[] salt = Convert.FromBase64String(partes[1]);
        byte[] hashEsperado = Convert.FromBase64String(partes[2]);

        byte[] hashIngresado = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iteraciones, HashAlgorithmName.SHA256, hashEsperado.Length);

        // Comparamos byte por byte "en tiempo constante": evita una técnica
        // de ataque que mide cuánto tarda la comparación para adivinar la
        // contraseña de a poco.
        return CryptographicOperations.FixedTimeEquals(hashIngresado, hashEsperado);
    }
}