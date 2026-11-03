namespace RetencionesRG830.Infrastructure.Servicios;

/// <summary>
/// Qué va a contener el archivo de SICORE de un cliente y un período, antes de
/// generarlo. Lo arma ServicioRegistroOperaciones.ResumenPeriodoSicoreAsync.
///
/// Las tres cantidades suman todas las operaciones del período: cada operación
/// cae en una sola.
/// </summary>
/// <param name="Informadas">Las que entran en el archivo: vigentes y con certificado.</param>
/// <param name="Anuladas">Las anuladas, que no se informan (regla 6).</param>
/// <param name="SinCertificado">Vigentes que no retuvieron nada, que tampoco se informan (regla 10).</param>
public record ResumenPeriodoSicore(int Informadas, int Anuladas, int SinCertificado);
