using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// Lo que la base de datos tiene que guardar tal cual. Estos tests se escribieron
/// al pasar de SQLite a SQL Server (octubre de 2026), para los dos riesgos propios
/// de SQL Server que los demás tests no ven:
///
/// - SQL Server guarda cada decimal con una cantidad fija de decimales y recorta
///   EN SILENCIO lo que no entre. SQLite no recortaba nada.
/// - En SQL Server un índice único admite un solo NULL, salvo que tenga filtro.
///
/// Todos leen lo guardado con un contexto NUEVO: un contexto que ya tiene la
/// entidad en memoria la devuelve como la tiene, sin mirar la base, y así no se
/// vería ningún recorte. Corren contra SQLite por defecto y contra SQL Server con
/// la variable SIGERE_PRUEBAS_SQLSERVER (ver BaseDePrueba); sólo contra SQL Server
/// prueban de verdad lo que dicen. Los marcados [HechoEnSqlServer] miran el
/// catálogo del servidor y sin la variable figuran como omitidos.
/// </summary>
public class PersistenciaTests : IDisposable
{
    private readonly BaseDePrueba _base = new();

    private int _clienteId;
    private int _proveedorId;
    private int _regimenId;

    public PersistenciaTests()
    {
        using var db = _base.NuevoContexto();

        var cliente = new Cliente { Cuit = "30-61234567-4", RazonSocial = "Metalúrgica del Valle S.A." };
        // El régimen 78 de la planilla: 2 %, $ 224.000 no sujetos, mínimo $ 240.
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

        _clienteId = cliente.Id;
        _proveedorId = proveedor.Id;
        _regimenId = regimen.Id;
    }

    public void Dispose() => _base.Dispose();

    private Operacion NuevaOperacion(decimal gravado, DateOnly fecha, long numero) => new()
    {
        ClienteId = _clienteId,
        ProveedorId = _proveedorId,
        RegimenId = _regimenId,
        TipoComprobante = 1,
        PuntoVenta = 1,
        NumeroComprobante = numero,
        FechaComprobante = fecha,
        FechaRetencion = fecha,
        ImporteComprobante = gravado,
        ImporteGravado = gravado,
        CreadoPorUsuarioId = 1
    };

    /// <summary>Registra la operación con su propio contexto, como un pedido web.</summary>
    private async Task<Operacion> RegistrarEnUnPedidoNuevo(Operacion operacion)
    {
        using var db = _base.NuevoContexto();
        return await new ServicioRegistroOperaciones(db).RegistrarAsync(operacion);
    }

    /// <summary>
    /// El caso de Gómez (los dos comprobantes reales de agosto), pero cada operación
    /// en su propio contexto, como en producción. La segunda descuenta lo ya retenido
    /// LEYÉNDOLO DE LA BASE: si la columna recortara la primera retención a dos
    /// decimales ($ 3.800,99 en lugar de $ 3.800,9918), la segunda daría $ 1.743,8036
    /// en lugar de $ 1.743,8018.
    ///
    /// Es el test que detecta un decimal(18,2) en MontoRetenido. El golden test de
    /// ServicioRegistroOperacionesTests no lo detecta, porque comparte un contexto
    /// entre las dos operaciones.
    /// </summary>
    [Fact]
    public async Task Gomez_con_un_contexto_nuevo_por_operacion()
    {
        var primera = await RegistrarEnUnPedidoNuevo(NuevaOperacion(414049.59m, new DateOnly(2026, 8, 4), 2514));
        var segunda = await RegistrarEnUnPedidoNuevo(NuevaOperacion(87190.09m, new DateOnly(2026, 8, 12), 2515));

        using var db = _base.NuevoContexto();
        var primeraGuardada = await db.Operaciones.SingleAsync(o => o.Id == primera.Id);

        Assert.Equal(3800.9918m, primeraGuardada.MontoRetenido);
        Assert.Equal(1743.8018m, segunda.MontoRetenido);
        Assert.Equal(1743.8018m, (await db.Operaciones.SingleAsync(o => o.Id == segunda.Id)).MontoRetenido);
    }

    /// <summary>
    /// Las 14 propiedades decimales, cada una con la mayor cantidad de decimales que
    /// le corresponde, se leen idénticas con un contexto nuevo.
    /// </summary>
    [Fact]
    public async Task Las_columnas_decimales_guardan_todos_sus_decimales()
    {
        int regimenId, operacionId;
        using (var db = _base.NuevoContexto())
        {
            var regimen = new Regimen
            {
                Codigo = 116,
                Descripcion = "Régimen de prueba con escala.",
                TipoCalculo = TipoCalculoRegimen.EscalaProgresiva,
                TasaInscripto = 0.012345m,
                TasaNoInscriptoHumana = 0.283456m,
                TasaNoInscriptoResto = 0.254321m,
                MontoNoSujetoARetencion = 67170.55m,
                MinimoRetencion = 240.15m,
                MinimoRetencionNoInscripto = 1020.25m,
                VigenciaDesde = new DateOnly(2026, 1, 1),
                Tramos =
                {
                    new TramoEscala { Desde = 8000.01m, Hasta = 16000.99m, MontoFijo = 400.45m, Porcentaje = 0.091234m, VigenciaDesde = new DateOnly(2026, 1, 1) }
                }
            };
            db.Regimenes.Add(regimen);
            db.SaveChanges();
            regimenId = regimen.Id;

            var operacion = NuevaOperacion(1m, new DateOnly(2026, 9, 1), 1);
            operacion.RegimenId = regimen.Id;
            operacion.ImporteComprobante = 1234567890123456.78m;
            operacion.ImporteGravado = 1000000000000000.01m;
            operacion.BaseCalculoAcumulada = 9999999999999999.99m;
            // Alícuota de seis decimales por importe de dos: ocho decimales.
            operacion.MontoRetenido = 123456789012.12345678m;
            db.Operaciones.Add(operacion);
            db.SaveChanges();
            operacionId = operacion.Id;
        }

        using var lectura = _base.NuevoContexto();
        var r = await lectura.Regimenes.Include(x => x.Tramos).SingleAsync(x => x.Id == regimenId);
        var t = r.Tramos.Single();
        var o = await lectura.Operaciones.SingleAsync(x => x.Id == operacionId);

        Assert.Equal(0.012345m, r.TasaInscripto);
        Assert.Equal(0.283456m, r.TasaNoInscriptoHumana);
        Assert.Equal(0.254321m, r.TasaNoInscriptoResto);
        Assert.Equal(67170.55m, r.MontoNoSujetoARetencion);
        Assert.Equal(240.15m, r.MinimoRetencion);
        Assert.Equal(1020.25m, r.MinimoRetencionNoInscripto);
        Assert.Equal(8000.01m, t.Desde);
        Assert.Equal(16000.99m, t.Hasta);
        Assert.Equal(400.45m, t.MontoFijo);
        Assert.Equal(0.091234m, t.Porcentaje);
        Assert.Equal(1234567890123456.78m, o.ImporteComprobante);
        Assert.Equal(1000000000000000.01m, o.ImporteGravado);
        Assert.Equal(9999999999999999.99m, o.BaseCalculoAcumulada);
        Assert.Equal(123456789012.12345678m, o.MontoRetenido);
    }

    /// <summary>
    /// La regla escrita en RetencionesRG830DbContext: el monto retenido es alícuota
    /// por importe, así que necesita por lo menos los decimales de las dos juntos. Si
    /// alguien sube la escala de las alícuotas y se olvida del monto retenido, falla
    /// acá. Mira el modelo, no la base: corre igual en cualquier motor.
    /// </summary>
    [Fact]
    public void La_escala_del_monto_retenido_alcanza_para_alicuota_por_importe()
    {
        using var db = _base.NuevoContexto();
        int Escala<T>(string propiedad) =>
            db.Model.FindEntityType(typeof(T))!.FindProperty(propiedad)!.GetScale()!.Value;

        var alicuota = new[]
        {
            Escala<Regimen>(nameof(Regimen.TasaInscripto)),
            Escala<Regimen>(nameof(Regimen.TasaNoInscriptoHumana)),
            Escala<Regimen>(nameof(Regimen.TasaNoInscriptoResto)),
            Escala<TramoEscala>(nameof(TramoEscala.Porcentaje))
        }.Max();
        var importe = new[]
        {
            Escala<Operacion>(nameof(Operacion.ImporteGravado)),
            Escala<Regimen>(nameof(Regimen.MontoNoSujetoARetencion)),
            Escala<TramoEscala>(nameof(TramoEscala.Desde)),
            Escala<TramoEscala>(nameof(TramoEscala.MontoFijo))
        }.Max();

        Assert.True(Escala<Operacion>(nameof(Operacion.MontoRetenido)) >= alicuota + importe,
            $"MontoRetenido tiene escala {Escala<Operacion>(nameof(Operacion.MontoRetenido))} y "
            + $"necesita al menos {alicuota} (alícuota) + {importe} (importe).");
    }

    /// <summary>
    /// Regla 10: varias operaciones sin certificado del mismo cliente conviven. En
    /// SQL Server falla si al índice único le falta el filtro, y recién con la
    /// SEGUNDA: por eso son dos.
    /// </summary>
    [Fact]
    public async Task Dos_operaciones_sin_certificado_del_mismo_cliente_se_guardan()
    {
        // $ 50.000 cada una: no llegan al monto no sujeto, no retienen.
        var primera = await RegistrarEnUnPedidoNuevo(NuevaOperacion(50000m, new DateOnly(2026, 11, 3), 1));
        var segunda = await RegistrarEnUnPedidoNuevo(NuevaOperacion(50000m, new DateOnly(2026, 11, 5), 2));

        using var db = _base.NuevoContexto();
        Assert.Null(primera.NumeroCertificado);
        Assert.Null(segunda.NumeroCertificado);
        Assert.Equal(2, await db.Operaciones.CountAsync(o => o.NumeroCertificado == null));
    }

    // --- Sólo contra SQL Server: el catálogo del servidor ----------------------

    private class ColumnaDecimal
    {
        public string Tabla { get; set; } = "";
        public string Columna { get; set; } = "";
        public byte Precision { get; set; }
        public int Escala { get; set; }
    }

    /// <summary>
    /// Las columnas de la base creada con la migración real tienen la precisión
    /// declarada en el modelo. Compara contra la lista aprobada al migrar, columna
    /// por columna, para que una diferencia nombre la columna.
    /// </summary>
    [HechoEnSqlServer]
    public async Task Las_columnas_decimales_tienen_en_la_base_la_precision_declarada()
    {
        var esperadas = new Dictionary<string, (int Precision, int Escala)>
        {
            ["Operaciones.ImporteComprobante"] = (18, 2),
            ["Operaciones.ImporteGravado"] = (18, 2),
            ["Operaciones.BaseCalculoAcumulada"] = (18, 2),
            ["Operaciones.MontoRetenido"] = (20, 8),
            ["Regimenes.TasaInscripto"] = (9, 6),
            ["Regimenes.TasaNoInscriptoHumana"] = (9, 6),
            ["Regimenes.TasaNoInscriptoResto"] = (9, 6),
            ["Regimenes.MontoNoSujetoARetencion"] = (18, 2),
            ["Regimenes.MinimoRetencion"] = (18, 2),
            ["Regimenes.MinimoRetencionNoInscripto"] = (18, 2),
            ["TramosEscala.Desde"] = (18, 2),
            ["TramosEscala.Hasta"] = (18, 2),
            ["TramosEscala.MontoFijo"] = (18, 2),
            ["TramosEscala.Porcentaje"] = (9, 6),
        };

        using var db = _base.NuevoContexto();
        Assert.True(db.Database.IsSqlServer());
        var reales = await db.Database.SqlQueryRaw<ColumnaDecimal>(
            "SELECT TABLE_NAME AS Tabla, COLUMN_NAME AS Columna, NUMERIC_PRECISION AS Precision, NUMERIC_SCALE AS Escala "
            + "FROM INFORMATION_SCHEMA.COLUMNS WHERE DATA_TYPE = 'decimal'").ToListAsync();

        var encontradas = reales.ToDictionary(c => $"{c.Tabla}.{c.Columna}", c => ((int)c.Precision, c.Escala));
        Assert.Equal(esperadas.OrderBy(x => x.Key), encontradas.OrderBy(x => x.Key));
    }

    [HechoEnSqlServer]
    public async Task El_indice_de_certificados_deja_afuera_las_operaciones_sin_numero()
    {
        using var db = _base.NuevoContexto();
        var filtro = await db.Database.SqlQueryRaw<string>(
            "SELECT filter_definition AS Value FROM sys.indexes "
            + "WHERE name = 'IX_Operaciones_ClienteId_NumeroCertificado'").SingleAsync();

        Assert.Equal("([NumeroCertificado] IS NOT NULL)", filtro);
    }

    [HechoEnSqlServer]
    public async Task La_base_usa_la_intercalacion_del_modelo()
    {
        using var db = _base.NuevoContexto();
        var intercalacion = await db.Database.SqlQueryRaw<string>(
            "SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation')) AS Value").SingleAsync();

        Assert.Equal("Modern_Spanish_CI_AS", intercalacion);
    }
}
