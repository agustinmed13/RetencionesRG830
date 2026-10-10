using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;

namespace RetencionesRG830.Tests;

/// <summary>
/// Prueba qué versión de un régimen rige en una fecha (regla 9).
///
/// Hoy el seed tiene una sola versión por régimen, pero en cuanto AFIP actualice
/// un valor habrá dos. Estos tests fijan qué pasa entonces, antes de que pase.
/// </summary>
public class VigenciaRegimenTests
{
    private static Regimen Version(DateOnly desde, DateOnly? hasta = null, decimal tasa = 0.02m) => new()
    {
        Codigo = 78,
        Descripcion = "Enajenación de bienes muebles y bienes de cambio",
        TasaInscripto = tasa,
        VigenciaDesde = desde,
        VigenciaHasta = hasta
    };

    private static readonly DateOnly Enero2025 = new(2025, 1, 1);
    private static readonly DateOnly Julio2026 = new(2026, 7, 1);

    [Fact]
    public void Una_sola_version_rige_desde_su_fecha()
    {
        var unica = Version(Enero2025);

        Assert.Same(unica, VigenciaRegimen.VersionVigente(new[] { unica }, new DateOnly(2026, 9, 22)));
        Assert.Same(unica, VigenciaRegimen.VersionVigente(new[] { unica }, Enero2025)); // el primer día ya rige
    }

    [Fact]
    public void Con_dos_versiones_y_la_fecha_en_la_segunda_rige_la_segunda()
    {
        // La primera no tiene VigenciaHasta: la reemplaza la nueva igual.
        var vieja = Version(Enero2025, tasa: 0.02m);
        var nueva = Version(Julio2026, tasa: 0.03m);

        Assert.Same(nueva, VigenciaRegimen.VersionVigente(new[] { vieja, nueva }, new DateOnly(2026, 9, 22)));
    }

    [Fact]
    public void Con_dos_versiones_y_la_fecha_antes_de_la_segunda_rige_la_primera()
    {
        // Es lo que permite calcular una operación atrasada con los valores de su
        // fecha de retención y no con los de hoy.
        var vieja = Version(Enero2025, tasa: 0.02m);
        var nueva = Version(Julio2026, tasa: 0.03m);

        Assert.Same(vieja, VigenciaRegimen.VersionVigente(new[] { vieja, nueva }, new DateOnly(2026, 6, 30)));
    }

    [Fact]
    public void Una_fecha_anterior_a_toda_version_no_tiene_version_vigente()
    {
        var unica = Version(Enero2025);

        Assert.Null(VigenciaRegimen.VersionVigente(new[] { unica }, new DateOnly(2024, 12, 31)));
    }

    [Fact]
    public void Una_version_futura_todavia_no_rige()
    {
        var actual = Version(Enero2025);
        var futura = Version(Julio2026);
        var versiones = new[] { actual, futura };
        var hoy = new DateOnly(2026, 3, 15);

        Assert.Same(actual, VigenciaRegimen.VersionVigente(versiones, hoy));
        Assert.Equal(EstadoVigencia.Proxima, VigenciaRegimen.Estado(futura, versiones, hoy));
        Assert.Equal(EstadoVigencia.Vigente, VigenciaRegimen.Estado(actual, versiones, hoy));
    }

    [Fact]
    public void Una_version_reemplazada_es_historica()
    {
        var vieja = Version(Enero2025);
        var nueva = Version(Julio2026);

        Assert.Equal(EstadoVigencia.Historica,
            VigenciaRegimen.Estado(vieja, new[] { vieja, nueva }, new DateOnly(2026, 9, 22)));
    }

    [Fact]
    public void Empate_de_VigenciaDesde_lanza_excepcion_en_lugar_de_elegir_una()
    {
        // En la base no puede pasar: el índice único (Codigo, VigenciaDesde) lo
        // impide. Si pasa, los datos están mal, y calcular con una versión elegida
        // al azar sería peor que no calcular.
        var una = Version(Enero2025, tasa: 0.02m);
        var otra = Version(Enero2025, tasa: 0.03m);

        Assert.Throws<InvalidOperationException>(
            () => VigenciaRegimen.VersionVigente(new[] { una, otra }, new DateOnly(2026, 9, 22)));
    }

    [Fact]
    public void VigenciaHasta_incluye_el_ultimo_dia()
    {
        var conFin = Version(Enero2025, hasta: new DateOnly(2026, 6, 30));

        Assert.Same(conFin, VigenciaRegimen.VersionVigente(new[] { conFin }, new DateOnly(2026, 6, 30)));
        Assert.Null(VigenciaRegimen.VersionVigente(new[] { conFin }, new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public void Versiones_de_regimenes_distintos_lanzan_excepcion()
    {
        var del78 = Version(Enero2025);
        var del94 = Version(Julio2026);
        del94.Codigo = 94;

        Assert.Throws<ArgumentException>(
            () => VigenciaRegimen.VersionVigente(new[] { del78, del94 }, new DateOnly(2026, 9, 22)));
    }
}
