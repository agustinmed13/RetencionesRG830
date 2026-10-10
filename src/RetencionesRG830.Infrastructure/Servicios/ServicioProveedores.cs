using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;

namespace RetencionesRG830.Infrastructure.Servicios;

/// <summary>
/// Modificación de proveedores. Está separado del controlador por la misma
/// razón que ServicioRegistroOperaciones: así se puede probar automáticamente.
/// </summary>
public class ServicioProveedores
{
    private readonly RetencionesRG830DbContext _db;

    public ServicioProveedores(RetencionesRG830DbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Guarda los cambios de un proveedor existente y lo devuelve tal como quedó
    /// en la base, o null si no existe.
    ///
    /// Copia uno por uno sólo los campos que se pueden editar. Hay dos que no se
    /// toman nunca de lo recibido, aunque vengan cargados:
    /// - ClienteId: un proveedor pertenece a un único cliente (regla 8), y es
    ///   parte de su identidad. Si se aceptara, un POST armado a mano (o un
    ///   desplegable que no muestra al cliente de baja y queda en otro) mudaría
    ///   al proveedor, con sus operaciones, a otro agente de retención.
    /// - Activo: se cambia sólo con la baja o la reactivación, que piden
    ///   confirmación. El formulario de edición no lo manda, y como en la entidad
    ///   vale true por defecto, editar un proveedor de baja lo reactivaba.
    ///
    /// Antes se hacía _db.Proveedores.Update(datos), que guarda TODAS las
    /// propiedades del objeto recibido: esos dos campos incluidos.
    /// </summary>
    public async Task<Proveedor?> ActualizarAsync(Proveedor datos)
    {
        var proveedor = await _db.Proveedores.FindAsync(datos.Id);
        if (proveedor is null) return null;

        proveedor.Cuit = datos.Cuit;
        proveedor.RazonSocial = datos.RazonSocial;
        proveedor.Domicilio = datos.Domicilio;
        proveedor.Localidad = datos.Localidad;
        proveedor.InscriptoEnGanancias = datos.InscriptoEnGanancias;
        proveedor.TipoPersona = datos.TipoPersona;

        await _db.SaveChangesAsync();
        return proveedor;
    }
}
