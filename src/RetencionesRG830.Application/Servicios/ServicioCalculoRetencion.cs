using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;

namespace RetencionesRG830.Application.Servicios;

/// <summary>
/// El motor de cálculo de la retención de la RG 830: el corazón del sistema.
///
/// A propósito no toca la base de datos ni sabe nada de pantallas: recibe
/// todos los datos como parámetros y devuelve un resultado. Eso es lo que
/// permite probarlo con casos reales sin levantar la aplicación web.
///
/// Cada paso del método reproduce una fila de la hoja "Cálculo" de la
/// planilla Excel original; el número de celda está indicado en cada
/// comentario para poder compararlos lado a lado.
/// </summary>
public static class ServicioCalculoRetencion
{
    /// <param name="netoGravadoAcumuladoMensual">
    /// El total gravado del mes para ese proveedor y régimen, INCLUYENDO la
    /// operación que se está cargando ahora. No es el importe suelto.
    /// </param>
    /// <param name="retencionesAcumuladasDelMes">
    /// Lo que ya se le retuvo antes a ese proveedor en el mismo mes.
    /// </param>
    public static ResultadoCalculoRetencion Calcular(
        Regimen regimen,
        bool proveedorInscriptoEnGanancias,
        TipoPersona tipoPersona,
        decimal netoGravadoAcumuladoMensual,
        decimal retencionesAcumuladasDelMes)
    {
        var resultado = new ResultadoCalculoRetencion
        {
            // Los datos de entrada se devuelven tal cual, para que el desglose
            // de la pantalla salga completo de este resultado.
            BaseAcumuladaDelMes = netoGravadoAcumuladoMensual,
            RetencionesAnterioresDelMes = retencionesAcumuladasDelMes
        };

        // --- C21: mínimo no sujeto a retención ---
        // Si el proveedor NO está inscripto en Ganancias, no corresponde
        // considerar ningún monto no sujeto a retención (nota (a) de la
        // hoja Tablas). Es decir: se le retiene desde el primer peso.
        resultado.MinimoNoSujetoARetencion = proveedorInscriptoEnGanancias
            ? regimen.MontoNoSujetoARetencion
            : 0m;

        // --- C22: tasa aplicable ---
        // Si está inscripto se usa la tasa de inscriptos. Si no, la tasa
        // depende de si es persona humana o del "resto" (sociedades, etc.).
        resultado.TasaAplicada = TasaAplicable(regimen, proveedorInscriptoEnGanancias, tipoPersona);

        // --- C24: neto sujeto a retención ---
        // Al acumulado del mes se le resta el piso. Si el acumulado todavía
        // no llegó al piso, la base es cero (no se retiene nada).
        resultado.NetoSujetoARetencion =
            resultado.MinimoNoSujetoARetencion > netoGravadoAcumuladoMensual
                ? 0m
                : netoGravadoAcumuladoMensual - resultado.MinimoNoSujetoARetencion;

         // --- C25: retención acumulada determinada ---
        // Detalle fino de la norma: en la hoja "Tablas", el "s/escala"
        // figura ÚNICAMENTE en la columna de INSCRIPTOS. A un proveedor no
        // inscripto se le aplica siempre una tasa fija (28%), aunque el
        // régimen sea de los que van por escala. Por eso la bifurcación
        // depende de las dos cosas: el régimen Y la condición del proveedor.
        var correspondeEscala = CorrespondeEscala(regimen, proveedorInscriptoEnGanancias);
        resultado.SeAplicoEscala = correspondeEscala;

        resultado.RetencionAcumuladaDeterminada = correspondeEscala
            ? CalcularPorEscala(regimen, resultado.NetoSujetoARetencion)
            : resultado.NetoSujetoARetencion * resultado.TasaAplicada;

        // --- C26: retención a practicar ---
        // Se descuenta lo ya retenido antes en el mismo mes. Esta resta es
        // la clave de todo el régimen: sin ella, el segundo pago del mes a
        // un mismo proveedor se retiene de más.
        var aRetener = resultado.RetencionAcumuladaDeterminada - retencionesAcumuladasDelMes;

        // El mínimo de retención también depende de la condición del
        // proveedor: es $240 en general, pero $1.020 para no inscriptos en
        // alquileres de inmuebles urbanos. En la planilla esa excepción está
        // escrita a mano dentro de la fórmula ("si el régimen es el 31...");
        // acá sale de los datos del régimen.
        var minimoAplicable = proveedorInscriptoEnGanancias
            ? regimen.MinimoRetencion
            : regimen.MinimoRetencionNoInscripto;

        if (aRetener < minimoAplicable)
        {
            resultado.RetencionAPracticar = 0m;
            resultado.Observacion = $"Retención mínima ${minimoAplicable:N2}";
        }
        else
        {
            resultado.RetencionAPracticar = aRetener;
        }

        return resultado;
    }

    /// <summary>
    /// C22 de la planilla: la tasa que corresponde según la condición del proveedor.
    /// Si está inscripto, la de inscriptos. Si no, depende de si es persona humana
    /// o del "resto" (sociedades, etc.).
    ///
    /// Es pública para que la pantalla de carga pueda decirle al operador qué
    /// alícuota se va a aplicar y por qué, con la misma regla que usa el cálculo.
    /// </summary>
    public static decimal TasaAplicable(Regimen regimen, bool proveedorInscriptoEnGanancias, TipoPersona tipoPersona) =>
        proveedorInscriptoEnGanancias
            ? regimen.TasaInscripto
            : tipoPersona == TipoPersona.HumanaYSucesionIndivisa
                ? regimen.TasaNoInscriptoHumana
                : regimen.TasaNoInscriptoResto;

    /// <summary>
    /// true si la retención se determina por la escala progresiva: régimen por
    /// escala Y proveedor inscripto. En la hoja Tablas el "s/escala" figura sólo
    /// en la columna de inscriptos; a un no inscripto se le aplica tasa fija.
    /// Pública por la misma razón que TasaAplicable.
    /// </summary>
    public static bool CorrespondeEscala(Regimen regimen, bool proveedorInscriptoEnGanancias) =>
        regimen.TipoCalculo == TipoCalculoRegimen.EscalaProgresiva && proveedorInscriptoEnGanancias;

    /// <summary>
    /// Busca en qué tramo de la escala cae el neto sujeto a retención y
    /// aplica "monto fijo del tramo + porcentaje sobre el excedente".
    /// Es el equivalente al VLOOKUP con coincidencia aproximada del Excel.
    /// </summary>
    private static decimal CalcularPorEscala(Regimen regimen, decimal netoSujetoARetencion)
    {
        var tramo = regimen.Tramos
            .Where(t => netoSujetoARetencion >= t.Desde)
            .OrderByDescending(t => t.Desde)
            .FirstOrDefault();

        if (tramo is null)
        {
            throw new InvalidOperationException(
                $"El régimen {regimen.Codigo} es de escala progresiva pero no tiene tramos cargados.");
        }

        return tramo.MontoFijo + tramo.Porcentaje * (netoSujetoARetencion - tramo.Desde);
    }
}