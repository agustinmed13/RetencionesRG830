using System.Globalization;
using System.Text;
using RetencionesRG830.Domain.Entidades;

namespace RetencionesRG830.Infrastructure.Servicios;

/// <summary>
/// Arma el archivo de texto que el estudio importa en SICORE.
///
/// Cada retención es una línea de 145 posiciones fijas: no hay separadores
/// ni columnas, cada dato ocupa un lugar exacto y se rellena con ceros o
/// espacios hasta completar su ancho.
///
/// El punto crítico de esta clase es el formato de los números. Hoy el
/// estudio arma el archivo copiando celdas de Excel, y a veces AFIP se lo
/// rechaza porque, según la configuración regional de la computadora que se
/// use, los decimales salen con punto en vez de coma. Acá eso no puede
/// pasar: se formatea con CultureInfo.InvariantCulture, que siempre produce
/// punto, y después se reemplaza por coma explícitamente. El resultado es
/// idéntico en cualquier computadora del mundo.
/// </summary>
public class GeneradorArchivoSicore
{
    private const string CodigoImpuestoGanancias = "0217";
    private const string CodigoOperacionRetencion = "1";
    private const string CodigoCondicion = "01";
    private const string SujetoNoSuspendido = "0";
    private const string SinPorcentajeExclusion = "000,00";
    private const string SinFechaExclusion = "00/00/0000";
    private const string TipoDocumentoCuit = "80";

    /// <summary>Genera el archivo completo, una línea por operación.</summary>
    public string Generar(IEnumerable<Operacion> operaciones)
    {
        var contenido = new StringBuilder();

        foreach (var operacion in operaciones
            .OrderBy(o => o.FechaRetencion)
            .ThenBy(o => o.NumeroCertificado))
        {
            contenido.Append(GenerarLinea(operacion)).Append("\r\n");
        }

        return contenido.ToString();
    }

    /// <summary>Genera la línea de 145 posiciones de una operación.</summary>
    public string GenerarLinea(Operacion operacion)
    {
        var proveedor = operacion.Proveedor
            ?? throw new InvalidOperationException("La operación no trae cargado el Proveedor.");
        var regimen = operacion.Regimen
            ?? throw new InvalidOperationException("La operación no trae cargado el Régimen.");

        return string.Concat(
            // 1-2    Código de comprobante
            operacion.TipoComprobante.ToString("D2", CultureInfo.InvariantCulture),
            // 3-12   Fecha del comprobante
            Fecha(operacion.FechaComprobante),
            // 13-28  Punto de venta y número, completado con espacios
            $"{operacion.PuntoVenta:D5}{operacion.NumeroComprobante:D8}".PadRight(16),
            // 29-44  Importe del comprobante
            Importe(operacion.ImporteComprobante, 16),
            // 45-48  Código de impuesto
            CodigoImpuestoGanancias,
            // 49-51  Código de régimen
            regimen.Codigo.ToString("D3", CultureInfo.InvariantCulture),
            // 52     Código de operación
            CodigoOperacionRetencion,
            // 53-66  Base de cálculo: el acumulado del mes, no el importe suelto
            Importe(operacion.BaseCalculoAcumulada, 14),
            // 67-76  Fecha de la retención
            Fecha(operacion.FechaRetencion),
            // 77-78  Código de condición
            CodigoCondicion,
            // 79     Retención a sujeto suspendido
            SujetoNoSuspendido,
            // 80-93  Importe de la retención
            Importe(operacion.MontoRetenido, 14),
            // 94-99  Porcentaje de exclusión
            SinPorcentajeExclusion,
            // 100-109 Fecha del boletín de exclusión
            SinFechaExclusion,
            // 110-111 Tipo de documento del retenido (80 = CUIT)
            TipoDocumentoCuit,
            // 112-131 Número de documento
            SoloDigitos(proveedor.Cuit).PadLeft(20, '0'),
            // 132-145 Número de certificado
            operacion.NumeroCertificado.ToString("D14", CultureInfo.InvariantCulture));
    }

    private static string Fecha(DateOnly fecha)
        => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Importe con dos decimales, coma como separador decimal, sin separador
    /// de miles, y ceros a la izquierda hasta completar el ancho del campo.
    /// </summary>
    private static string Importe(decimal valor, int ancho)
    {
        var texto = valor.ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',');
        return texto.PadLeft(ancho, '0');
    }

    private static string SoloDigitos(string texto)
        => new string(texto.Where(char.IsDigit).ToArray());
}