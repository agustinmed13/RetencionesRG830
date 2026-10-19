using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// El mismo CUIT escrito de distintas formas es el mismo proveedor (regla 8).
///
/// Hasta octubre de 2026 ServicioProveedores guardaba el CUIT tal como llegaba, y
/// el índice único (ClienteId, Cuit) comparaba ese texto: "30-70000000-8" y
/// "30700000008" son el mismo CUIT pero dos textos distintos, así que el mismo
/// proveedor quedaba cargado dos veces en el mismo cliente. La consecuencia
/// fiscal: el acumulado del mes se arma por ProveedorId (reglas 1 a 3), así que
/// cada ficha tenía el suyo, el monto no sujeto se descontaba una vez por ficha y
/// se retenía de menos. El primer test de esta clase lo medía en rojo: $ 31.040 en
/// lugar de $ 35.520.
///
/// Ahora Cliente y Proveedor guardan el CUIT sólo con sus dígitos
/// (NormalizadorCuit, en el setter de la entidad), y RetencionesRG830DbContext
/// rechaza al guardar un CUIT que no sea válido según ValidadorCuit.
///
/// Los CUIT son inventados y matemáticamente válidos. Los valores del régimen 78
/// salen del seed (DbInitializer), no se copian acá.
/// </summary>
public class CuitConDistintoFormatoTests : IDisposable
{
    private readonly BaseDePrueba _base = new();

    // El mismo CUIT ficticio, escrito de las dos formas, y su forma canónica.
    private const string CuitConGuiones = "30-70000000-8";
    private const string CuitSinGuiones = "30700000008";
    private const string CuitCanonico = "30700000008";

    private const decimal Importe = 1_000_000m;

    public void Dispose() => _base.Dispose();

    /// <summary>Un cliente propio, para que nada del seed se mezcle.</summary>
    private int CrearCliente()
    {
        using var db = _base.NuevoContexto();
        var cliente = new Cliente { Cuit = "30-71111111-1", RazonSocial = "Distribuidora del Sur S.A." };
        db.Clientes.Add(cliente);
        db.SaveChanges();
        return cliente.Id;
    }

    private async Task<Proveedor> CrearProveedor(int clienteId, string cuit)
    {
        using var db = _base.NuevoContexto();
        return await new ServicioProveedores(db).CrearAsync(new Proveedor
        {
            ClienteId = clienteId,
            Cuit = cuit,
            RazonSocial = "Ferretería La Tuerca S.R.L.",
            Domicilio = "Belgrano 1500",
            Localidad = "Córdoba - CÓRDOBA",
            InscriptoEnGanancias = true,
            TipoPersona = TipoPersona.Resto
        });
    }

    /// <summary>
    /// Busca la ficha del proveedor por el CUIT tal como lo escribió el operador.
    /// La búsqueda tiene que normalizar igual que la entidad: lo guardado son sólo
    /// los dígitos.
    /// </summary>
    private async Task<Proveedor> BuscarProveedor(int clienteId, string cuit)
    {
        var normalizado = NormalizadorCuit.Normalizar(cuit);
        using var db = _base.NuevoContexto();
        return await db.Proveedores.SingleAsync(p => p.ClienteId == clienteId && p.Cuit == normalizado);
    }

    /// <summary>Registra la operación con su propio contexto, como un pedido web.</summary>
    private async Task<Operacion> Registrar(int clienteId, int proveedorId, int regimenId, DateOnly fecha, long numero)
    {
        using var db = _base.NuevoContexto();
        return await new ServicioRegistroOperaciones(db).RegistrarAsync(new Operacion
        {
            ClienteId = clienteId,
            ProveedorId = proveedorId,
            RegimenId = regimenId,
            TipoComprobante = 1,
            PuntoVenta = 1,
            NumeroComprobante = numero,
            FechaComprobante = fecha,
            FechaRetencion = fecha,
            ImporteComprobante = Importe,
            ImporteGravado = Importe,
            CreadoPorUsuarioId = 1
        });
    }

    /// <summary>
    /// Dos pagos de $ 1.000.000 en septiembre, régimen 78, proveedor inscripto
    /// (seed: 2 %, $ 224.000 no sujetos, mínimo $ 240).
    ///
    /// Como un solo proveedor, la base del mes es $ 2.000.000:
    ///   (2.000.000 - 224.000) x 2 % = $ 35.520 en el mes
    ///   (1.º pago: $ 15.520; 2.º pago: 35.520 - 15.520 = $ 20.000).
    /// Repartido en dos fichas, cada pago era el primero del mes de su ficha:
    ///   (1.000.000 - 224.000) x 2 % = $ 15.520, dos veces: $ 31.040.
    /// Diferencia: el monto no sujeto descontado de más, 224.000 x 2 % = $ 4.480.
    ///
    /// El segundo pago llega con el CUIT escrito de la otra forma. Ahora el alta con
    /// esa escritura se rechaza, y el pago se registra contra la ficha que ya existe.
    /// </summary>
    [Fact]
    public async Task El_mismo_CUIT_con_otro_formato_no_parte_el_acumulado_del_mes()
    {
        Regimen regimen78;
        using (var db = _base.NuevoContexto())
        {
            DbInitializer.Seed(db);
            regimen78 = await db.Regimenes.SingleAsync(r => r.Codigo == 78);
        }
        var clienteId = CrearCliente();

        // Precondición: las dos escrituras son el mismo CUIT válido.
        Assert.True(ValidadorCuit.EsValido(CuitConGuiones));
        Assert.True(ValidadorCuit.EsValido(CuitSinGuiones));

        await CrearProveedor(clienteId, CuitConGuiones);
        // La segunda alta ya no crea otra ficha: el índice único la rechaza.
        await Assert.ThrowsAsync<DbUpdateException>(() => CrearProveedor(clienteId, CuitSinGuiones));

        var fichaDelPrimerPago = await BuscarProveedor(clienteId, CuitConGuiones);
        var fichaDelSegundoPago = await BuscarProveedor(clienteId, CuitSinGuiones);

        var primera = await Registrar(clienteId, fichaDelPrimerPago.Id, regimen78.Id, new DateOnly(2026, 9, 10), 101);
        var segunda = await Registrar(clienteId, fichaDelSegundoPago.Id, regimen78.Id, new DateOnly(2026, 9, 20), 102);

        // Lo que corresponde a un solo proveedor: el monto no sujeto, una sola vez
        // sobre el acumulado del mes (reglas 1 y 2).
        var deberiaRetenerse = (2 * Importe - regimen78.MontoNoSujetoARetencion) * regimen78.TasaInscripto;
        var retieneHoy = primera.MontoRetenido + segunda.MontoRetenido;
        var diferencia = deberiaRetenerse - retieneHoy;

        Assert.True(retieneHoy == deberiaRetenerse,
            $"El mismo proveedor ({CuitConGuiones} y {CuitSinGuiones}) quedó en dos fichas. " +
            $"Debería retenerse $ {deberiaRetenerse:N2} en el mes; el sistema retiene $ {retieneHoy:N2} " +
            $"({primera.MontoRetenido:N2} + {segunda.MontoRetenido:N2}). Retiene $ {diferencia:N2} de menos.");

        // Las cifras de la regresión, escritas: si cambian los valores del 78 en el
        // seed, este test avisa y hay que volver a hacer la cuenta del resumen.
        Assert.Equal(35_520m, deberiaRetenerse);
        Assert.Equal(15_520m, primera.MontoRetenido);
        Assert.Equal(20_000m, segunda.MontoRetenido);
        Assert.Equal(fichaDelPrimerPago.Id, fichaDelSegundoPago.Id);
    }

    /// <summary>
    /// Las escrituras del mismo CUIT que llegan de verdad: con puntos, y pegadas
    /// desde Excel o desde un mensaje, con un espacio, un espacio duro (NBSP) o una
    /// tabulación al final. Todas son la ficha que ya existe: el alta se rechaza,
    /// y buscar con esa escritura encuentra la ficha original.
    /// </summary>
    [Theory]
    [InlineData("30.70000000.8")]
    [InlineData("30700000008 ")]
    [InlineData("30700000008 ")]
    [InlineData("30700000008\t")]
    [InlineData("30-70000000-8")]
    [InlineData("30 70000000 8")]
    public async Task Las_variantes_del_mismo_CUIT_terminan_en_la_misma_ficha(string variante)
    {
        var clienteId = CrearCliente();
        var original = await CrearProveedor(clienteId, CuitSinGuiones);

        await Assert.ThrowsAsync<DbUpdateException>(() => CrearProveedor(clienteId, variante));

        Assert.Equal(original.Id, (await BuscarProveedor(clienteId, variante)).Id);
        using var db = _base.NuevoContexto();
        var fichas = await db.Proveedores.Where(p => p.ClienteId == clienteId).ToListAsync();
        Assert.Equal(CuitCanonico, Assert.Single(fichas).Cuit);
    }

    /// <summary>
    /// La regla está en la entidad, no en el servicio: un CUIT asignado a mano y
    /// guardado sin pasar por ServicioProveedores ni ServicioClientes queda igual
    /// normalizado. Se lee con un contexto nuevo, para mirar lo que quedó en la base.
    /// </summary>
    [Fact]
    public void Asignado_directo_en_la_entidad_el_CUIT_queda_normalizado()
    {
        var cliente = new Cliente { Cuit = "30.71111111.1 ", RazonSocial = "Distribuidora del Sur S.A." };
        var proveedor = new Proveedor { Cuit = " 30-70000000-8\t", RazonSocial = "Ferretería La Tuerca S.R.L." };

        // En memoria, apenas asignado.
        Assert.Equal("30711111111", cliente.Cuit);
        Assert.Equal(CuitCanonico, proveedor.Cuit);

        using (var db = _base.NuevoContexto())
        {
            db.Clientes.Add(cliente);
            db.SaveChanges();
            proveedor.ClienteId = cliente.Id;
            db.Proveedores.Add(proveedor);
            db.SaveChanges();
        }

        using var lectura = _base.NuevoContexto();
        Assert.Equal("30711111111", lectura.Clientes.Single().Cuit);
        Assert.Equal(CuitCanonico, lectura.Proveedores.Single().Cuit);
    }

    /// <summary>
    /// Normalizar no es validar: lo que no son 11 dígitos con el verificador
    /// correcto no se guarda, aunque se asigne directo en la entidad.
    /// </summary>
    [Theory]
    [InlineData("30-70000000-1")]   // dígito verificador incorrecto
    [InlineData("30-7000000-8")]    // diez dígitos
    [InlineData("30-70000000-88")]  // doce dígitos
    [InlineData("sin CUIT")]        // ningún dígito
    public void Un_CUIT_invalido_no_se_guarda(string cuit)
    {
        var clienteId = CrearCliente();

        using (var db = _base.NuevoContexto())
        {
            db.Proveedores.Add(new Proveedor { ClienteId = clienteId, Cuit = cuit, RazonSocial = "Ferretería La Tuerca S.R.L." });
            var error = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
            Assert.Contains("CUIT", error.Message);
        }

        using var lectura = _base.NuevoContexto();
        Assert.Empty(lectura.Proveedores);
    }
}
