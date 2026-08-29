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

    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public RolUsuario Rol { get; set; }

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
}
