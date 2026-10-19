using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RetencionesRG830.Tests;

/// <summary>
/// La migración NormalizaCuit sobre datos cargados ANTES de que la entidad
/// normalizara el CUIT. Sólo contra SQL Server: la migración es SQL de SQL Server,
/// y con SQLite los tests no corren migraciones.
///
/// Cada test baja la base a la migración anterior (InicialSqlServer), inserta
/// filas con SQL directo (con la entidad no se puede: su setter ya normaliza, y
/// justamente se quiere el CUIT como quedó guardado antes) y vuelve a migrar.
/// </summary>
public class MigracionNormalizaCuitTests : IDisposable
{
    private const string MigracionAnterior = "20261014123134_InicialSqlServer";

    private readonly BaseDePrueba _base = new();

    public void Dispose() => _base.Dispose();

    private void BajarALaMigracionAnterior()
    {
        using var db = _base.NuevoContexto();
        db.GetService<IMigrator>().Migrate(MigracionAnterior);
    }

    private void Migrar()
    {
        using var db = _base.NuevoContexto();
        db.Database.Migrate();
    }

    private int InsertarCliente(string cuit, string razonSocial)
    {
        using var db = _base.NuevoContexto();
        return db.Database.SqlQuery<int>($@"
            INSERT INTO Clientes (Cuit, RazonSocial, Domicilio, Localidad, NombreFirmante, CargoFirmante, Activo)
            OUTPUT INSERTED.Id AS Value
            VALUES ({cuit}, {razonSocial}, N'', N'', N'', N'', 1)").AsEnumerable().Single();
    }

    private int InsertarProveedor(int clienteId, string cuit, string razonSocial)
    {
        using var db = _base.NuevoContexto();
        return db.Database.SqlQuery<int>($@"
            INSERT INTO Proveedores (ClienteId, Cuit, RazonSocial, Domicilio, Localidad, InscriptoEnGanancias, TipoPersona, Activo)
            OUTPUT INSERTED.Id AS Value
            VALUES ({clienteId}, {cuit}, {razonSocial}, N'', N'', 1, 1, 1)").AsEnumerable().Single();
    }

    /// <summary>Lo que quedó guardado, leído con SQL: sin pasar por la entidad.</summary>
    private List<string> CuitsGuardados(string tabla)
    {
        using var db = _base.NuevoContexto();
        // Una consulta fija por tabla: el nombre de una tabla no puede ir como
        // parámetro, e interpolarlo en el SQL es lo que EF Core advierte (EF1002).
        var consulta = tabla switch
        {
            "Clientes" => db.Database.SqlQuery<string>($"SELECT Cuit AS Value FROM Clientes ORDER BY Id"),
            "Proveedores" => db.Database.SqlQuery<string>($"SELECT Cuit AS Value FROM Proveedores ORDER BY Id"),
            _ => throw new ArgumentException($"Tabla sin CUIT: {tabla}", nameof(tabla))
        };
        return consulta.AsEnumerable().ToList();
    }

    [HechoEnSqlServer]
    public void Normaliza_los_CUIT_guardados_con_cualquier_separador()
    {
        BajarALaMigracionAnterior();
        var cliente = InsertarCliente("30711111111\t", "Distribuidora del Sur S.A.");
        InsertarProveedor(cliente, "30-70000000-8", "Ferretería La Tuerca S.R.L.");
        InsertarProveedor(cliente, "20.31456789.8", "Gómez, Carlos Alberto");
        InsertarProveedor(cliente, "27325678947 ", "Ramírez, Laura Beatriz");
        InsertarProveedor(cliente, "27 33678912 0 ", "Sosa, Marta Elena");

        Migrar();

        Assert.Equal(new[] { "30711111111" }, CuitsGuardados("Clientes"));
        Assert.Equal(new[] { "30700000008", "20314567898", "27325678947", "27336789120" }, CuitsGuardados("Proveedores"));
    }

    /// <summary>
    /// El mismo proveedor dos veces en un cliente, con distinta escritura: la
    /// migración falla nombrando las dos filas, y no modifica nada (ni siquiera
    /// las filas que no chocan).
    /// </summary>
    [HechoEnSqlServer]
    public void Si_dos_proveedores_quedarian_iguales_falla_nombrando_las_filas_y_no_toca_nada()
    {
        BajarALaMigracionAnterior();
        var cliente = InsertarCliente("30-71111111-1", "Distribuidora del Sur S.A.");
        var uno = InsertarProveedor(cliente, "30-70000000-8", "Ferretería La Tuerca S.R.L.");
        var otro = InsertarProveedor(cliente, "30700000008\t", "Ferreteria La Tuerca SRL");
        InsertarProveedor(cliente, "20-31456789-8", "Gómez, Carlos Alberto");

        var error = Assert.Throws<SqlException>(Migrar);

        Assert.Contains("30700000008", error.Message);
        Assert.Contains($"Id {uno} (Ferretería La Tuerca S.R.L.", error.Message);
        Assert.Contains($"Id {otro} (Ferreteria La Tuerca SRL", error.Message);
        Assert.Equal(new[] { "30-70000000-8", "30700000008\t", "20-31456789-8" }, CuitsGuardados("Proveedores"));
        Assert.Equal(new[] { "30-71111111-1" }, CuitsGuardados("Clientes"));
    }

    [HechoEnSqlServer]
    public void Si_dos_clientes_quedarian_iguales_falla_nombrando_las_filas()
    {
        BajarALaMigracionAnterior();
        var uno = InsertarCliente("30-71111111-1", "Distribuidora del Sur S.A.");
        var otro = InsertarCliente("30711111111 ", "Distribuidora del Sur");

        var error = Assert.Throws<SqlException>(Migrar);

        Assert.Contains("Clientes con el CUIT 30711111111", error.Message);
        Assert.Contains($"Id {uno} (Distribuidora del Sur S.A.", error.Message);
        Assert.Contains($"Id {otro} (Distribuidora del Sur,", error.Message);
        Assert.Equal(new[] { "30-71111111-1", "30711111111 " }, CuitsGuardados("Clientes"));
    }

    /// <summary>
    /// El mismo CUIT en dos clientes distintos no es una colisión: es el mismo
    /// proveedor de dos empresas, lo que la regla 8 permite.
    /// </summary>
    [HechoEnSqlServer]
    public void El_mismo_proveedor_en_dos_clientes_no_es_una_colision()
    {
        BajarALaMigracionAnterior();
        var uno = InsertarCliente("30-71111111-1", "Distribuidora del Sur S.A.");
        var otro = InsertarCliente("30-61234567-4", "Metalúrgica del Valle S.A.");
        InsertarProveedor(uno, "30-70000000-8", "Ferretería La Tuerca S.R.L.");
        InsertarProveedor(otro, "30700000008", "Ferretería La Tuerca S.R.L.");

        Migrar();

        Assert.Equal(new[] { "30700000008", "30700000008" }, CuitsGuardados("Proveedores"));
    }

    [HechoEnSqlServer]
    public void Si_un_CUIT_no_queda_con_11_digitos_falla_nombrando_la_fila()
    {
        BajarALaMigracionAnterior();
        var cliente = InsertarCliente("30-71111111-1", "Distribuidora del Sur S.A.");
        var corto = InsertarProveedor(cliente, "30-7000000-8", "Ferretería La Tuerca S.R.L.");

        var error = Assert.Throws<SqlException>(Migrar);

        Assert.Contains($"Proveedor Id {corto} (Ferretería La Tuerca S.R.L.)", error.Message);
        Assert.Contains("no tiene 11 dígitos", error.Message);
        Assert.Equal(new[] { "30-71111111-1" }, CuitsGuardados("Clientes"));
    }
}
