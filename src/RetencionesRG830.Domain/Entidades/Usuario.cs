using RetencionesRG830.Domain.Enums;

namespace RetencionesRG830.Domain.Entidades;

/// <summary>
/// Una persona que puede iniciar sesión en el sistema.
///
/// Si Rol es Estudio, ClienteId queda en null: esa persona ve todos los
/// clientes. Si Rol es Cliente, ClienteId indica a qué Cliente pertenece, y
/// el sistema sólo le muestra los proveedores y operaciones de ese Cliente.
///
/// Nota para más adelante: cuando conectemos ASP.NET Core Identity (el
/// sistema de login de .NET), esta clase probablemente se combine con la
/// clase IdentityUser en vez de quedar totalmente separada. Se modela así
/// por ahora para dejar clara la relación en el diagrama.
/// </summary>
public class Usuario
{
    public int Id { get; set; }

    private string _email = string.Empty;

    /// <summary>
    /// Se guarda siempre en minúsculas y sin espacios alrededor, y se busca igual
    /// (ServicioUsuarios). Así el inicio de sesión no distingue mayúsculas en
    /// ningún motor de base de datos: no depende de la intercalación del servidor,
    /// que puede cambiar al desplegar en otro.
    ///
    /// La regla está acá, en el setter, para que la cumpla cualquier forma de crear
    /// un usuario. Al leer de la base, EF Core carga el campo _email directamente,
    /// sin pasar por el setter: lo guardado ya está normalizado.
    /// </summary>
    public string Email
    {
        get => _email;
        set => _email = NormalizarEmail(value);
    }

    /// <summary>Un correo tal como se guarda y se compara: "Ana@X.com " → "ana@x.com".</summary>
    public static string NormalizarEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();
    public string PasswordHash { get; set; } = string.Empty;

    public RolUsuario Rol { get; set; }

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
}
