using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;

namespace RetencionesRG830.Infrastructure.Servicios;

/// <summary>
/// Registra una operación aplicando toda la regla del régimen: busca el
/// acumulado del mes, calcula la retención y asigna el número de certificado.
///
/// Vive acá y no en Application porque necesita consultar la base de datos.
/// Está separado del controlador para poder probarlo automáticamente: un
/// controlador necesita un servidor web corriendo para ejecutarse, un
/// servicio como este no.
/// </summary>
public class ServicioRegistroOperaciones
{
    private readonly RetencionesRG830DbContext _db;

    public ServicioRegistroOperaciones(RetencionesRG830DbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Completa la operación con la retención y el número de certificado,
    /// la guarda, y la devuelve ya registrada.
    /// </summary>
      /// <summary>
    /// Calcula la retención SIN guardar nada, para poder mostrarla antes de
    /// confirmar. Deja cargada la base acumulada en la operación recibida.
    /// </summary>
    public async Task<ResultadoCalculoRetencion> SimularAsync(Operacion operacion)
    {
        var proveedor = await _db.Proveedores.FindAsync(operacion.ProveedorId)
            ?? throw new InvalidOperationException("El proveedor indicado no existe.");

        // Un proveedor pertenece a un único cliente (regla 8). Si se aceptara uno
        // de otro cliente, el acumulado del mes se buscaría sobre las operaciones de
        // otro agente de retención y la operación ensuciaría el acumulado de las dos
        // empresas. Se controla acá, que es por donde pasan la simulación y el
        // registro, aunque la pantalla ya lo valide antes con un mensaje amable.
        if (proveedor.ClienteId != operacion.ClienteId)
            throw new InvalidOperationException("El proveedor indicado no pertenece a este cliente.");

        var regimen = await _db.Regimenes
            .Include(r => r.Tramos)
            .FirstOrDefaultAsync(r => r.Id == operacion.RegimenId)
            ?? throw new InvalidOperationException("El régimen indicado no existe.");

        var acumulado = await CalcularAcumuladoDelMesAsync(operacion);

        operacion.BaseCalculoAcumulada = acumulado.Gravado + operacion.ImporteGravado;

        return ServicioCalculoRetencion.Calcular(
            regimen,
            proveedor.InscriptoEnGanancias,
            proveedor.TipoPersona,
            operacion.BaseCalculoAcumulada,
            acumulado.Retenido);
    }

    /// <summary>
    /// Confirma la operación: calcula, le asigna el número de certificado y
    /// la guarda. Reutiliza SimularAsync para que el número que se le mostró
    /// al usuario y el que se guarda salgan del mismo cálculo.
    /// </summary>
    public async Task<Operacion> RegistrarAsync(Operacion operacion)
    {
        var resultado = await SimularAsync(operacion);

        operacion.MontoRetenido = resultado.RetencionAPracticar;
        operacion.NumeroCertificado = await SiguienteNumeroCertificadoAsync(operacion.ClienteId);
        operacion.FechaCarga = DateTime.Now;

        _db.Operaciones.Add(operacion);
        await _db.SaveChangesAsync();

        return operacion;
    }

    /// <summary>
    /// Suma lo gravado y lo ya retenido a ese proveedor, en ese régimen,
    /// dentro del mes de la fecha de retención. NO incluye la operación
    /// que se está por registrar.
    /// </summary>
    public async Task<(decimal Gravado, decimal Retenido)> CalcularAcumuladoDelMesAsync(Operacion operacion)
    {
        var primerDia = new DateOnly(operacion.FechaRetencion.Year, operacion.FechaRetencion.Month, 1);
        var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

        var anteriores = await _db.Operaciones
            .Where(o => o.ClienteId == operacion.ClienteId
                     && o.ProveedorId == operacion.ProveedorId
                     && o.RegimenId == operacion.RegimenId
                     && o.Id != operacion.Id
                     && !o.Anulada
                     && o.FechaRetencion >= primerDia
                     && o.FechaRetencion <= ultimoDia)    
            .ToListAsync();

        return (anteriores.Sum(o => o.ImporteGravado), anteriores.Sum(o => o.MontoRetenido));
    }

    /// <summary>
    /// true si el proveedor existe y es de ese cliente. Lo usan la validación del
    /// formulario y el cálculo en vivo, para avisar antes de llegar al error de
    /// SimularAsync.
    /// </summary>
    public Task<bool> ProveedorPerteneceAlClienteAsync(int proveedorId, int clienteId) =>
        _db.Proveedores.AnyAsync(p => p.Id == proveedorId && p.ClienteId == clienteId);

    /// <summary>
    /// Las operaciones vigentes del mismo cliente y proveedor con el mismo comprobante
    /// (tipo, punto de venta y número). NO es un error: la retención se practica al
    /// pagar, no al facturar, así que una factura pagada en dos cuotas da dos
    /// retenciones legítimas con el mismo comprobante. Sirve para avisarle al
    /// operador, que decide. Las anuladas no cuentan: volver a cargar un comprobante
    /// después de anular su operación es la forma normal de corregir un error.
    /// Pendiente de confirmar con el contador del estudio (ver CLAUDE.md).
    /// </summary>
    public Task<List<Operacion>> OperacionesConMismoComprobanteAsync(Operacion operacion) =>
        _db.Operaciones
            .Where(o => o.ClienteId == operacion.ClienteId
                     && o.ProveedorId == operacion.ProveedorId
                     && o.TipoComprobante == operacion.TipoComprobante
                     && o.PuntoVenta == operacion.PuntoVenta
                     && o.NumeroComprobante == operacion.NumeroComprobante
                     && o.Id != operacion.Id
                     && !o.Anulada)
            .OrderBy(o => o.NumeroCertificado)
            .ToListAsync();

    /// <summary>
    /// El próximo número de certificado correlativo del cliente. Es público para
    /// poder anticiparlo en la pantalla de confirmación; el número definitivo se
    /// vuelve a calcular en RegistrarAsync, en el momento de guardar.
    /// </summary>
    public async Task<int> SiguienteNumeroCertificadoAsync(int clienteId)
    {
        var ultimo = await _db.Operaciones
            .Where(o => o.ClienteId == clienteId)
            .MaxAsync(o => (int?)o.NumeroCertificado) ?? 0;

        return ultimo + 1;
    }
        /// <summary>
    /// Anula una operación. No la borra: queda registrada con el motivo,
    /// la fecha y quién la anuló, pero deja de sumar al acumulado del mes
    /// y no sale en el archivo de SICORE.
    /// </summary>
    public async Task<Operacion> AnularAsync(int operacionId, string motivo, int usuarioId)
    {
        var operacion = await _db.Operaciones.FindAsync(operacionId)
            ?? throw new InvalidOperationException("La operación indicada no existe.");

        if (operacion.Anulada)
        {
            throw new InvalidOperationException("La operación ya estaba anulada.");
        }

        operacion.Anulada = true;
        operacion.MotivoAnulacion = motivo;
        operacion.FechaAnulacion = DateTime.Now;
        operacion.AnuladaPorUsuarioId = usuarioId;

        await _db.SaveChangesAsync();

        return operacion;
    }
}