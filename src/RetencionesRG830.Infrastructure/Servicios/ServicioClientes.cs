using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;

namespace RetencionesRG830.Infrastructure.Servicios;

/// <summary>
/// Modificación de clientes. Mismo patrón que ServicioProveedores: separado del
/// controlador para poder probarlo automáticamente.
/// </summary>
public class ServicioClientes
{
    private readonly RetencionesRG830DbContext _db;

    public ServicioClientes(RetencionesRG830DbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Da de alta un cliente y lo devuelve ya guardado, con su Id.
    ///
    /// Arma un Cliente nuevo copiando uno por uno los campos que ofrece el
    /// formulario. No se guarda el objeto recibido: ese lo arma ASP.NET con todo
    /// lo que venga en el POST, aunque el formulario no lo tenga. Con
    /// _db.Clientes.Add(datos) se colaban:
    /// - Id y Activo: se podía forzar el número del registro o crearlo de baja.
    /// - Las colecciones (Usuarios, Proveedores, Operaciones): Add() guarda también
    ///   los objetos relacionados, así que un POST con Usuarios[0].Email,
    ///   Usuarios[0].Rol... creaba un usuario con el rol que se eligiera.
    /// </summary>
    public async Task<Cliente> CrearAsync(Cliente datos)
    {
        var cliente = new Cliente
        {
            Cuit = datos.Cuit,
            RazonSocial = datos.RazonSocial,
            Domicilio = datos.Domicilio,
            Localidad = datos.Localidad,
            NombreFirmante = datos.NombreFirmante,
            CargoFirmante = datos.CargoFirmante
        };

        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        return cliente;
    }

    /// <summary>
    /// Guarda los cambios de un cliente existente y lo devuelve tal como quedó en
    /// la base, o null si no existe.
    ///
    /// Copia uno por uno sólo los campos que se pueden editar. Activo no se toma
    /// nunca de lo recibido: se cambia sólo con la baja o la reactivación, que
    /// piden confirmación. El formulario de edición no lo manda, y como en la
    /// entidad vale true por defecto, con _db.Clientes.Update(datos), que guarda
    /// TODAS las propiedades del objeto recibido, editar un cliente de baja lo
    /// reactivaba.
    /// </summary>
    public async Task<Cliente?> ActualizarAsync(Cliente datos)
    {
        var cliente = await _db.Clientes.FindAsync(datos.Id);
        if (cliente is null) return null;

        cliente.Cuit = datos.Cuit;
        cliente.RazonSocial = datos.RazonSocial;
        cliente.Domicilio = datos.Domicilio;
        cliente.Localidad = datos.Localidad;
        cliente.NombreFirmante = datos.NombreFirmante;
        cliente.CargoFirmante = datos.CargoFirmante;

        await _db.SaveChangesAsync();
        return cliente;
    }
}
