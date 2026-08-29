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

    private async Task<int> SiguienteNumeroCertificadoAsync(int clienteId)
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