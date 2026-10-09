using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;

namespace RetencionesRG830.Web.Infraestructura;

/// <summary>
/// El texto de ayuda que aparece debajo del régimen en "Nueva operación".
///
/// Qué alícuota corresponde a cada condición lo decide el motor
/// (ServicioCalculoRetencion.TasaAplicable y CorrespondeEscala): acá sólo se arma
/// la frase. Así la ayuda no puede decir un número distinto del que aplica el panel.
/// </summary>
public static class FormatoRegimen
{
    /// <summary>
    /// Con proveedor elegido: la alícuota que se le va a aplicar y por qué.
    /// "Alícuota 10,00 % — proveedor no inscripto, persona humana · Tasa fija".
    /// Sin proveedor: las alícuotas posibles, aclarando que dependen de su condición.
    /// </summary>
    public static string Ayuda(Regimen regimen, Proveedor? proveedor)
    {
        if (proveedor is null)
        {
            var paraInscriptos = regimen.TipoCalculo == Domain.Enums.TipoCalculoRegimen.EscalaProgresiva
                ? "Escala progresiva para inscriptos"
                : $"Alícuota {regimen.TasaInscripto:P2} para inscriptos";
            var paraNoInscriptos = regimen.TasaNoInscriptoHumana == regimen.TasaNoInscriptoResto
                ? $"{regimen.TasaNoInscriptoHumana:P2} para no inscriptos"
                : $"{regimen.TasaNoInscriptoHumana:P2} para no inscriptos personas humanas, {regimen.TasaNoInscriptoResto:P2} para otros sujetos";
            return $"{paraInscriptos} · {paraNoInscriptos} — depende de la condición del proveedor";
        }

        var condicion = proveedor.InscriptoEnGanancias
            ? "proveedor inscripto"
            : $"proveedor no inscripto, {FormatoProveedor.TipoPersona(proveedor.TipoPersona).ToLowerInvariant()}";

        if (ServicioCalculoRetencion.CorrespondeEscala(regimen, proveedor.InscriptoEnGanancias))
            return $"Según escala — {condicion} · Escala progresiva";

        var tasa = ServicioCalculoRetencion.TasaAplicable(regimen, proveedor.InscriptoEnGanancias, proveedor.TipoPersona);
        var aclaracion = regimen.TipoCalculo == Domain.Enums.TipoCalculoRegimen.EscalaProgresiva
            ? " (la escala es sólo para inscriptos)"
            : "";
        return $"Alícuota {tasa:P2} — {condicion} · Tasa fija{aclaracion}";
    }
}
