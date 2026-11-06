using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;
using RetencionesRG830.Infrastructure.Persistencia;

namespace RetencionesRG830.Tests;

/// <summary>
/// Los códigos de comprobante ya guardados en la base, después de corregir la tabla
/// (Recibo y Nota de débito estaban invertidos). La tabla la fija
/// FormatoComprobanteTests; acá se prueba lo que la tabla no puede arreglar sola: el
/// número que quedó guardado.
///
/// Los tests de la migración corren sólo contra SQL Server: con SQLite los tests no
/// corren migraciones. Bajan a la migración anterior e insertan con la entidad, porque
/// el esquema es el mismo: la migración sólo cambia datos.
/// </summary>
public class CodigosComprobanteGuardadosTests : IDisposable
{
    private const string MigracionAnterior = "20261018120000_NormalizaCuit";

    private readonly BaseDePrueba _base = new();

    public void Dispose() => _base.Dispose();

    private void MigrarHasta(string? migracion)
    {
        using var db = _base.NuevoContexto();
        db.GetService<IMigrator>().Migrate(migracion);
    }

    /// <summary>
    /// Carga una operación por cada código, en el orden dado. Devuelve los Id en ese
    /// mismo orden. Las que se indiquen quedan anuladas.
    /// </summary>
    private List<int> CargarOperaciones(int[] tipos, bool[] anuladas)
    {
        using var db = _base.NuevoContexto();

        var cliente = new Cliente { Cuit = "30-61234567-4", RazonSocial = "Metalúrgica del Valle S.A." };
        var regimen = new Regimen
        {
            Codigo = 78,
            Descripcion = "Enajenación de bienes muebles y bienes de cambio.",
            TipoCalculo = TipoCalculoRegimen.TasaFija,
            TasaInscripto = 0.02m,
            TasaNoInscriptoHumana = 0.10m,
            TasaNoInscriptoResto = 0.10m,
            MontoNoSujetoARetencion = 224000m,
            MinimoRetencion = 240m,
            VigenciaDesde = new DateOnly(2026, 1, 1)
        };
        db.Clientes.Add(cliente);
        db.Regimenes.Add(regimen);
        db.SaveChanges();

        var proveedor = new Proveedor
        {
            ClienteId = cliente.Id,
            Cuit = "20-31456789-8",
            RazonSocial = "Gómez, Carlos Alberto",
            InscriptoEnGanancias = true,
            TipoPersona = TipoPersona.HumanaYSucesionIndivisa
        };
        db.Proveedores.Add(proveedor);
        db.SaveChanges();

        var operaciones = tipos.Select((tipo, i) => new Operacion
        {
            ClienteId = cliente.Id,
            ProveedorId = proveedor.Id,
            RegimenId = regimen.Id,
            TipoComprobante = tipo,
            PuntoVenta = 1,
            NumeroComprobante = i + 1,
            NumeroCertificado = i + 1,
            FechaComprobante = new DateOnly(2026, 9, 10),
            FechaRetencion = new DateOnly(2026, 9, 10),
            ImporteComprobante = 1000m,
            ImporteGravado = 1000m,
            CreadoPorUsuarioId = 1,
            Anulada = anuladas[i]
        }).ToList();
        db.Operaciones.AddRange(operaciones);
        db.SaveChanges();

        return operaciones.Select(o => o.Id).ToList();
    }

    private List<int> TiposGuardados(List<int> ids)
    {
        using var db = _base.NuevoContexto();
        var porId = db.Operaciones.AsNoTracking().ToDictionary(o => o.Id, o => o.TipoComprobante);
        return ids.Select(id => porId[id]).ToList();
    }

    /// <summary>
    /// Factura y Nota de crédito quedan como estaban, y las dos anuladas también se
    /// corrigen. Si la migración se hiciera en dos pasos, el 2 y el 4 terminarían
    /// iguales y este test lo vería.
    /// </summary>
    [HechoEnSqlServer]
    public void La_migracion_intercambia_solo_recibos_y_notas_de_debito()
    {
        MigrarHasta(MigracionAnterior);
        var ids = CargarOperaciones(
            new[] { 1, 2, 3, 4, 2, 4 },
            new[] { false, false, false, false, true, true });

        MigrarHasta(null);

        Assert.Equal(new[] { 1, 4, 3, 2, 4, 2 }, TiposGuardados(ids));
    }

    [HechoEnSqlServer]
    public void Bajar_la_migracion_deja_cada_operacion_como_estaba()
    {
        MigrarHasta(MigracionAnterior);
        var ids = CargarOperaciones(new[] { 1, 2, 3, 4 }, new bool[4]);
        MigrarHasta(null);

        MigrarHasta(MigracionAnterior);

        Assert.Equal(new[] { 1, 2, 3, 4 }, TiposGuardados(ids));
    }

    /// <summary>
    /// El seed pasa el código como un número suelto, así que corregir la tabla no lo
    /// corrige: en una base nueva la migración corre sobre tablas vacías, antes del
    /// seed. Sin este test, un 4 olvidado crearía un recibo como Nota de débito.
    /// </summary>
    [Fact]
    public void El_comprobante_del_seed_sigue_siendo_un_recibo()
    {
        using var db = _base.NuevoContexto();
        DbInitializer.Seed(db);

        var operacion = db.Operaciones.Single(o => o.PuntoVenta == 1 && o.NumeroComprobante == 45);

        Assert.Equal("Recibo", FormatoComprobante.NombreTipo(operacion.TipoComprobante));
    }
}
