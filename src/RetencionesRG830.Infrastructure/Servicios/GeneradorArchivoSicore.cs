using System.Globalization;
using System.Text;
using RetencionesRG830.Application.Servicios;
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
///
/// El otro motivo de rechazo es un registro que no mide exactamente 145 caracteres.
/// Rellenar no alcanza: PadRight y PadLeft completan lo que falta, pero no recortan lo
/// que sobra, y un valor demasiado largo corría todos los campos siguientes sin ningún
/// error. Por eso cada campo declara sus posiciones y se exige que el valor las ocupe
/// exactamente. Si uno no entra, el archivo no se genera: nunca se recorta, porque un
/// número recortado es otro número, y nunca se omite la operación, porque un archivo
/// incompleto parece bueno. Un archivo que no sale se nota; uno mal medido se descubre
/// cuando AFIP lo rechaza.
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

    /// <summary>El largo exacto de un registro, según la especificación.</summary>
    public const int LargoRegistro = 145;

    /// <summary>Un campo del registro: sus posiciones oficiales y cómo se obtiene su valor.</summary>
    public sealed record Campo(string Nombre, int Desde, int Hasta, Func<Operacion, string> Valor)
    {
        public int Ancho => Hasta - Desde + 1;
    }

    /// <summary>
    /// La tabla de campos del registro. Los nombres y las posiciones son los de la
    /// especificación oficial (ver la tabla en CLAUDE.md, "Archivo de SICORE"). Es fija
    /// y pública para poder controlar la tabla sin generar ninguna línea: que no tenga
    /// huecos ni superposiciones lo verifica GeneradorArchivoSicoreTests.
    ///
    /// Proveedor, Regimen y NumeroCertificado llevan "!" porque GenerarLinea ya
    /// controló que estén antes de leer los valores.
    /// </summary>
    public static readonly IReadOnlyList<Campo> Campos = new Campo[]
    {
        new("Código de comprobante", 1, 2,
            o => o.TipoComprobante.ToString("D2", CultureInfo.InvariantCulture)),
        new("Fecha de emisión del comprobante", 3, 12, o => Fecha(o.FechaComprobante)),
        new("Número del comprobante", 13, 28,
            o => $"{o.PuntoVenta:D5}{o.NumeroComprobante:D8}".PadRight(16)),
        new("Importe del comprobante", 29, 44, o => Importe(o.ImporteComprobante, 16)),
        new("Código de impuesto", 45, 48, _ => CodigoImpuestoGanancias),
        new("Código de régimen", 49, 51,
            o => o.Regimen!.Codigo.ToString("D3", CultureInfo.InvariantCulture)),
        new("Código de operación", 52, 52, _ => CodigoOperacionRetencion),
        // El acumulado del mes, no el importe suelto.
        new("Base de cálculo", 53, 66, o => Importe(o.BaseCalculoAcumulada, 14)),
        new("Fecha de emisión de la retención", 67, 76, o => Fecha(o.FechaRetencion)),
        new("Código de condición", 77, 78, _ => CodigoCondicion),
        new("Retención a sujetos suspendidos", 79, 79, _ => SujetoNoSuspendido),
        new("Importe de la retención", 80, 93, o => Importe(o.MontoRetenido, 14)),
        new("Porcentaje de exclusión", 94, 99, _ => SinPorcentajeExclusion),
        new("Fecha publicación o fin de vigencia", 100, 109, _ => SinFechaExclusion),
        new("Tipo de documento del retenido", 110, 111, _ => TipoDocumentoCuit),
        // Los 11 dígitos del CUIT con ceros a la izquierda. El CUIT ya se guarda sin
        // guiones (NormalizadorCuit), así que va tal cual.
        new("Número de documento del retenido", 112, 131, o => o.Proveedor!.Cuit.PadLeft(20, '0')),
        new("Número certificado original", 132, 145,
            o => o.NumeroCertificado!.Value.ToString("D14", CultureInfo.InvariantCulture))
    };

    /// <summary>
    /// Genera el archivo completo, una línea por operación. Si algún valor no entra,
    /// no devuelve nada y lanza ErrorArchivoSicore con todos los problemas juntos.
    /// </summary>
    public string Generar(IEnumerable<Operacion> operaciones)
    {
        var contenido = new StringBuilder();
        var problemas = new List<string>();

        foreach (var operacion in operaciones
            .OrderBy(o => o.FechaRetencion)
            .ThenBy(o => o.NumeroCertificado))
        {
            try
            {
                contenido.Append(GenerarLinea(operacion)).Append("\r\n");
            }
            catch (ErrorArchivoSicore error)
            {
                problemas.AddRange(error.Problemas);
            }
        }

        if (problemas.Count > 0)
            throw new ErrorArchivoSicore(problemas);

        return contenido.ToString();
    }

    /// <summary>Genera la línea de 145 posiciones de una operación.</summary>
    public string GenerarLinea(Operacion operacion) => GenerarLinea(operacion, Campos);

    /// <summary>
    /// Arma la línea con la tabla dada. El sistema siempre usa Campos: recibirla por
    /// parámetro es lo que permite probar el control final, que con la tabla correcta
    /// nunca salta.
    /// </summary>
    public string GenerarLinea(Operacion operacion, IReadOnlyList<Campo> campos)
    {
        var proveedor = operacion.Proveedor
            ?? throw new InvalidOperationException("La operación no trae cargado el Proveedor.");
        if (operacion.Regimen is null)
            throw new InvalidOperationException("La operación no trae cargado el Régimen.");
        // Una operación que no retuvo no emite certificado y no se informa (regla 10).
        // Si llega hasta acá es un error de quien la seleccionó: se falla en vez de
        // escribir un certificado en ceros, que AFIP recibiría como una retención más.
        var numeroCertificado = operacion.NumeroCertificado
            ?? throw new InvalidOperationException(
                "La operación no emitió certificado (no retuvo): no se informa en SICORE.");

        var operacionNombrada = Nombrar(operacion, proveedor, numeroCertificado);
        var valores = campos.Select(campo => (Campo: campo, Texto: campo.Valor(operacion))).ToList();

        var problemas = valores
            .Where(v => v.Texto.Length != v.Campo.Ancho)
            .Select(v => $"{operacionNombrada}: el campo \"{v.Campo.Nombre}\" (posiciones "
                + $"{v.Campo.Desde}-{v.Campo.Hasta}) admite {v.Campo.Ancho} caracteres y el valor "
                + $"\"{v.Texto}\" tiene {v.Texto.Length}.")
            .ToList();

        if (problemas.Count > 0)
            throw new ErrorArchivoSicore(problemas);

        var linea = string.Concat(valores.Select(v => v.Texto));

        // El control de arriba mira cada campo contra su ancho declarado: da 145 sólo si
        // la tabla encaja. Éste mira lo que de verdad se va a escribir, sin suponer nada.
        if (linea.Length != LargoRegistro)
            throw new ErrorArchivoSicore(new[]
            {
                $"{operacionNombrada}: la línea armada mide {linea.Length} caracteres "
                + $"y tiene que medir {LargoRegistro}."
            });

        return linea;
    }

    /// <summary>La operación, con lo que el operador ve en el listado.</summary>
    private static string Nombrar(Operacion operacion, Proveedor proveedor, int numeroCertificado) =>
        $"Operación {operacion.Id}, certificado N° {FormatoComprobante.NumeroCertificado(numeroCertificado)}, "
        + $"proveedor {proveedor.RazonSocial}, comprobante {FormatoComprobante.Describir(operacion)}";

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
}