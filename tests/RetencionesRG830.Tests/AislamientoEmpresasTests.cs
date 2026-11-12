using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// HU-15 (RF-04, RN-17): el usuario del rol Cliente opera sólo sobre su empresa, y el
/// del rol Estudio, sobre todas. El filtro está en ServicioRegistroOperaciones, con el
/// AlcanceEmpresa que le pasa el controlador: por eso se puede probar acá.
///
/// Un dato de otra empresa no existe para ese usuario: null, lista vacía, o el mismo
/// error que da un Id inexistente. Nunca "no tenés permiso", que confirmaría que existe.
///
/// Dos empresas, A y B, cada una con un proveedor y operaciones de septiembre de 2026
/// registradas por el Estudio. Cada test usa contextos nuevos, como en producción
/// (un contexto por pedido): lo que se lee viene de la base.
/// </summary>
public class AislamientoEmpresasTests : IDisposable
{
    private readonly BaseDePrueba _base = new();

    private int _clienteA, _clienteB;
    private int _proveedorA, _proveedorB;
    private int _regimenId;
    private int _operacionA, _operacionB, _operacionB2;

    private AlcanceEmpresa ClienteA => AlcanceEmpresa.DelCliente(_clienteA);
    private AlcanceEmpresa ClienteB => AlcanceEmpresa.DelCliente(_clienteB);
    private static AlcanceEmpresa Estudio => AlcanceEmpresa.Estudio;

    private const string MotivoValido = "Prueba de aislamiento entre empresas";

    public AislamientoEmpresasTests()
    {
        using (var db = _base.NuevoContexto())
        {
            var a = new Cliente { Cuit = "30612345674", RazonSocial = "Metalúrgica del Valle S.A." };
            var b = new Cliente { Cuit = "30718452364", RazonSocial = "Agroinsumos del Norte S.R.L." };
            var regimen = new Regimen
            {
                Codigo = 78,
                Descripcion = "Enajenación de bienes muebles y bienes de cambio",
                TipoCalculo = TipoCalculoRegimen.TasaFija,
                TasaInscripto = 0.02m,
                TasaNoInscriptoHumana = 0.10m,
                TasaNoInscriptoResto = 0.10m,
                MontoNoSujetoARetencion = 224000m,
                MinimoRetencion = 240m,
                VigenciaDesde = new DateOnly(2026, 1, 1)
            };
            db.Clientes.AddRange(a, b);
            db.Regimenes.Add(regimen);
            db.SaveChanges();

            var gomez = new Proveedor { ClienteId = a.Id, Cuit = "20314567898", RazonSocial = "Gómez, Carlos Alberto", InscriptoEnGanancias = true };
            var ruiz = new Proveedor { ClienteId = b.Id, Cuit = "20287654317", RazonSocial = "Ruiz, Héctor Damián", InscriptoEnGanancias = true };
            db.Proveedores.AddRange(gomez, ruiz);
            db.SaveChanges();

            (_clienteA, _clienteB) = (a.Id, b.Id);
            (_proveedorA, _proveedorB) = (gomez.Id, ruiz.Id);
            _regimenId = regimen.Id;
        }

        _operacionA = Registrar(Estudio, OperacionDeA(500_000m, 101)).Id;
        _operacionB = Registrar(Estudio, OperacionDeB(500_000m, 201)).Id;
        _operacionB2 = Registrar(Estudio, OperacionDeB(300_000m, 202)).Id;
    }

    public void Dispose() => _base.Dispose();

    private Operacion Nueva(int clienteId, int proveedorId, decimal gravado, long numero) => new()
    {
        ClienteId = clienteId,
        ProveedorId = proveedorId,
        RegimenId = _regimenId,
        TipoComprobante = 1,
        PuntoVenta = 1,
        NumeroComprobante = numero,
        FechaComprobante = new DateOnly(2026, 9, 10),
        FechaRetencion = new DateOnly(2026, 9, 10),
        ImporteComprobante = gravado,
        ImporteGravado = gravado,
        CreadoPorUsuarioId = 1
    };

    private Operacion OperacionDeA(decimal gravado, long numero) => Nueva(_clienteA, _proveedorA, gravado, numero);
    private Operacion OperacionDeB(decimal gravado, long numero) => Nueva(_clienteB, _proveedorB, gravado, numero);

    /// <summary>Un servicio con un contexto nuevo, como en cada pedido de la aplicación.</summary>
    private T ConServicio<T>(Func<ServicioRegistroOperaciones, Task<T>> accion)
    {
        using var db = _base.NuevoContexto();
        return accion(new ServicioRegistroOperaciones(db)).GetAwaiter().GetResult();
    }

    private Operacion Registrar(AlcanceEmpresa alcance, Operacion operacion) =>
        ConServicio(s => s.RegistrarAsync(alcance, operacion));

    private Operacion? Leer(int operacionId) =>
        ConServicio(s => s.ObtenerOperacionAsync(Estudio, operacionId));

    private async Task<Operacion> Anular(AlcanceEmpresa alcance, int operacionId)
    {
        using var db = _base.NuevoContexto();
        return await new ServicioRegistroOperaciones(db).AnularAsync(alcance, operacionId, MotivoValido, 1);
    }

    // ---------------------------------------------------------------------
    // El Cliente no ve nada de la otra empresa
    // ---------------------------------------------------------------------

    [Fact]
    public void En_el_listado_cada_cliente_ve_solo_sus_operaciones()
    {
        Assert.Equal(new[] { _operacionA },
            ConServicio(s => s.ListarOperacionesAsync(ClienteA, null)).Select(o => o.Id));

        // En el otro sentido también.
        Assert.Equal(new[] { _operacionB2, _operacionB }.OrderBy(i => i),
            ConServicio(s => s.ListarOperacionesAsync(ClienteB, null)).Select(o => o.Id).OrderBy(i => i));
    }

    [Fact]
    public void El_cliente_que_pide_el_listado_de_otra_empresa_la_ve_como_inexistente()
    {
        Assert.Empty(ConServicio(s => s.ListarOperacionesAsync(ClienteA, _clienteB)));
        Assert.Null(ConServicio(s => s.ObtenerClienteAsync(ClienteA, _clienteB)));
    }

    /// <summary>
    /// ObtenerOperacionAsync es la consulta del Detalle, del certificado y de la
    /// pantalla de Anular: las tres responden "no encontrado" cuando da null.
    /// </summary>
    [Fact]
    public void Una_operacion_de_otra_empresa_no_aparece_en_detalle_certificado_ni_anular()
    {
        Assert.Null(ConServicio(s => s.ObtenerOperacionAsync(ClienteA, _operacionB)));
        Assert.NotNull(ConServicio(s => s.ObtenerOperacionAsync(ClienteA, _operacionA)));
    }

    [Fact]
    public void El_formulario_no_ofrece_ni_encuentra_proveedores_de_otra_empresa()
    {
        Assert.Empty(ConServicio(s => s.ProveedoresActivosAsync(ClienteA, _clienteB)));
        Assert.Null(ConServicio(s => s.ObtenerProveedorAsync(ClienteA, _proveedorB)));
        Assert.Null(ConServicio(s => s.ProveedorDelClienteAsync(ClienteA, _proveedorB, _clienteB)));
        Assert.False(ConServicio(s => s.ProveedorPerteneceAlClienteAsync(ClienteA, _proveedorB, _clienteB)));
    }

    // ---------------------------------------------------------------------
    // Las tres fugas por el formulario de Nueva operación: un Cliente que cambia el
    // cliente oculto del formulario por el de otra empresa.
    // ---------------------------------------------------------------------

    /// <summary>Fuga 1: el cálculo en vivo mostraría el acumulado y lo retenido de B.</summary>
    [Fact]
    public async Task El_calculo_no_muestra_el_acumulado_ni_lo_retenido_de_otra_empresa()
    {
        using var db = _base.NuevoContexto();
        var servicio = new ServicioRegistroOperaciones(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.SimularAsync(ClienteA, OperacionDeB(100_000m, 999)));
        Assert.Equal("El cliente indicado no existe.", error.Message);
    }

    /// <summary>Fuga 2: el aviso de repetido mostraría número de certificado y fecha de B.</summary>
    [Fact]
    public void El_aviso_de_comprobante_repetido_no_muestra_operaciones_de_otra_empresa()
    {
        var mismoComprobanteQueB = OperacionDeB(100_000m, 201);

        Assert.Empty(ConServicio(s => s.OperacionesConMismoComprobanteAsync(ClienteA, mismoComprobanteQueB)));
        // Para el Estudio el aviso sí aparece: la operación existe.
        Assert.Single(ConServicio(s => s.OperacionesConMismoComprobanteAsync(Estudio, mismoComprobanteQueB)));
    }

    /// <summary>Fuga 3: el próximo número diría cuántos certificados emitió B.</summary>
    [Fact]
    public async Task El_proximo_numero_de_certificado_de_otra_empresa_no_se_revela()
    {
        using var db = _base.NuevoContexto();
        var servicio = new ServicioRegistroOperaciones(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.SiguienteNumeroCertificadoAsync(ClienteA, _clienteB));
        Assert.Equal(3, await servicio.SiguienteNumeroCertificadoAsync(Estudio, _clienteB));
    }

    // ---------------------------------------------------------------------
    // El Cliente no registra ni anula a nombre de otra empresa
    // ---------------------------------------------------------------------

    /// <summary>
    /// Con los dos importes: uno que emite certificado y uno que no llega al mínimo. El
    /// segundo importa: una operación sin certificado no pide número correlativo, así que
    /// no pasa por el control de SiguienteNumeroCertificadoAsync. Con sólo el primer
    /// caso, este test seguía en verde aunque RegistrarAsync y SimularAsync no
    /// controlaran nada (verificado metiendo el defecto).
    /// </summary>
    [Theory]
    [InlineData(400_000)] // emite certificado
    [InlineData(1_000)]   // no llega al mínimo: sin certificado (regla 10)
    public void No_registra_una_operacion_a_nombre_de_otra_empresa_aunque_el_formulario_la_mande(int gravado)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Registrar(ClienteA, OperacionDeB(gravado, 301)));

        Assert.Equal("El cliente indicado no existe.", error.Message);
        Assert.Equal(2, ConServicio(s => s.ListarOperacionesAsync(Estudio, _clienteB)).Count);
    }

    [Fact]
    public async Task No_anula_una_operacion_de_otra_empresa_y_la_operacion_queda_vigente()
    {
        await Assert.ThrowsAsync<OperacionNoEncontradaException>(() => Anular(ClienteA, _operacionB));

        var deB = Leer(_operacionB)!;
        Assert.False(deB.Anulada);
        Assert.Null(deB.MotivoAnulacion);
    }

    /// <summary>
    /// Si la operación de B ya estaba anulada, a A tampoco se le dice "ya estaba
    /// anulada": eso le confirmaría que existe.
    /// </summary>
    [Fact]
    public async Task Una_operacion_ajena_ya_anulada_da_no_encontrada_y_no_ya_anulada()
    {
        await Anular(Estudio, _operacionB);

        var error = await Assert.ThrowsAsync<OperacionNoEncontradaException>(() => Anular(ClienteA, _operacionB));
        Assert.IsNotType<OperacionYaAnuladaException>(error);
    }

    // ---------------------------------------------------------------------
    // Sobre su empresa, el Cliente puede todo lo que le corresponde
    // ---------------------------------------------------------------------

    [Fact]
    public async Task El_cliente_registra_simula_y_anula_en_su_propia_empresa()
    {
        var nueva = Registrar(ClienteA, OperacionDeA(300_000m, 102));
        Assert.Equal(2, nueva.NumeroCertificado);

        Assert.True(ConServicio(s => s.SimularAsync(ClienteA, OperacionDeA(100_000m, 103))).RetencionAPracticar >= 0);

        var anulada = await Anular(ClienteA, nueva.Id);
        Assert.True(anulada.Anulada);
    }

    // ---------------------------------------------------------------------
    // El Estudio sigue viendo y pudiendo todo
    // ---------------------------------------------------------------------

    [Fact]
    public void El_estudio_ve_las_operaciones_y_los_datos_de_todas_las_empresas()
    {
        Assert.Equal(new[] { _operacionA, _operacionB, _operacionB2 }.OrderBy(i => i),
            ConServicio(s => s.ListarOperacionesAsync(Estudio, null)).Select(o => o.Id).OrderBy(i => i));

        Assert.NotNull(ConServicio(s => s.ObtenerOperacionAsync(Estudio, _operacionA)));
        Assert.NotNull(ConServicio(s => s.ObtenerOperacionAsync(Estudio, _operacionB)));
        Assert.NotNull(ConServicio(s => s.ObtenerClienteAsync(Estudio, _clienteB)));
        Assert.Single(ConServicio(s => s.ProveedoresActivosAsync(Estudio, _clienteB)));
        Assert.NotNull(ConServicio(s => s.ObtenerProveedorAsync(Estudio, _proveedorB)));
    }

    [Fact]
    public async Task El_estudio_registra_y_anula_en_cualquier_empresa()
    {
        var enA = Registrar(Estudio, OperacionDeA(300_000m, 104));
        var enB = Registrar(Estudio, OperacionDeB(300_000m, 203));
        Assert.Equal(_clienteA, enA.ClienteId);
        Assert.Equal(_clienteB, enB.ClienteId);

        Assert.True((await Anular(Estudio, _operacionA)).Anulada);
        Assert.True((await Anular(Estudio, enB.Id)).Anulada);
    }

    // ---------------------------------------------------------------------
    // Un usuario Cliente sin empresa asociada (un dato mal cargado) no ve nada
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Un_cliente_sin_empresa_no_ve_ni_registra_ni_anula_nada()
    {
        var ninguna = AlcanceEmpresa.Ninguna;

        Assert.Empty(ConServicio(s => s.ListarOperacionesAsync(ninguna, null)));
        Assert.Null(ConServicio(s => s.ObtenerOperacionAsync(ninguna, _operacionA)));
        Assert.Throws<InvalidOperationException>(() => Registrar(ninguna, OperacionDeA(300_000m, 105)));
        await Assert.ThrowsAsync<OperacionNoEncontradaException>(() => Anular(ninguna, _operacionA));
        Assert.False(Leer(_operacionA)!.Anulada);
    }
}
