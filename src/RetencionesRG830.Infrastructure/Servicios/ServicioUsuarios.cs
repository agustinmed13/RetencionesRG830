using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;

namespace RetencionesRG830.Infrastructure.Servicios;

/// <summary>
/// Búsqueda de usuarios para el inicio de sesión. Está separada de
/// CuentaController para poder probarla automáticamente, igual que los demás
/// servicios.
/// </summary>
public class ServicioUsuarios
{
    private readonly RetencionesRG830DbContext _db;

    public ServicioUsuarios(RetencionesRG830DbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// El usuario con ese correo, o null si no existe. El correo se normaliza igual
    /// que al guardarlo (Usuario.NormalizarEmail): "Estudio@Ejemplo.com" encuentra a
    /// "estudio@ejemplo.com" en cualquier motor, sin depender de cómo compare textos
    /// la base.
    /// </summary>
    public Task<Usuario?> BuscarPorEmailAsync(string email)
    {
        var normalizado = Usuario.NormalizarEmail(email);
        return _db.Usuarios.FirstOrDefaultAsync(u => u.Email == normalizado);
    }
}
