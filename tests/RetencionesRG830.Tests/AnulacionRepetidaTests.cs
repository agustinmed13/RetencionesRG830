using RetencionesRG830.Application.Servicios;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// Qué pasa cuando se pide anular una operación que ya está anulada: dos pestañas
/// abiertas, o dos personas a la vez. La primera anulación es la que vale. El motivo,
/// el responsable y la fecha son el respaldo frente a una inspección, y un segundo
/// pedido no los puede pisar.
///
/// Cada paso usa su propio contexto, como en producción, donde cada pedido tiene el
/// suyo, y lo guardado se lee con otro contexto nuevo: uno que ya tiene la entidad en
/// memoria la devuelve sin mirar la base.
/// </summary>
public class AnulacionRepetidaTests : IDisposable
{
    private const string MotivoPrimero = "Importe mal cargado";
    private const string MotivoSegundo = "Comprobante equivocado";
    private const int UsuarioPrimero = 11;
    private const int UsuarioSegundo = 22;

    private readonly BaseDePrueba _base = new();
    private readonly int _operacionId;

    public AnulacionRepetidaTests()
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

        var fecha = new DateOnly(2026, 9, 10);
        var operacion = new ServicioRegistroOperaciones(db).RegistrarAsync(AlcanceEmpresa.Estudio, new Operacion
        {
            ClienteId = cliente.Id,
            ProveedorId = proveedor.Id,
            RegimenId = regimen.Id,
            TipoComprobante = 1,
            PuntoVenta = 1,
            NumeroComprobante = 100,
            FechaComprobante = fecha,
            FechaRetencion = fecha,
            ImporteComprobante = 1_000_000m,
            ImporteGravado = 1_000_000m,
            CreadoPorUsuarioId = 1
        }).GetAwaiter().GetResult();

        _operacionId = operacion.Id;
    }

    public void Dispose() => _base.Dispose();

    private async Task Anular(string motivo, int usuarioId)
    {
        using var db = _base.NuevoContexto();
        await new ServicioRegistroOperaciones(db).AnularAsync(AlcanceEmpresa.Estudio, _operacionId, motivo, usuarioId);
    }

    private Operacion LeerGuardada()
    {
        using var db = _base.NuevoContexto();
        return db.Operaciones.AsNoTracking().Single(o => o.Id == _operacionId);
    }

    [Fact]
    public async Task Anular_guarda_el_motivo_el_responsable_y_la_fecha()
    {
        using var db = _base.NuevoContexto();
        var cargadaAntes = await db.Operaciones.SingleAsync(o => o.Id == _operacionId);

        var devuelta = await new ServicioRegistroOperaciones(db).AnularAsync(AlcanceEmpresa.Estudio, _operacionId, MotivoPrimero, UsuarioPrimero);

        // Lo que devuelve el servicio, y la entidad que el contexto ya tenía en memoria,
        // tienen que reflejar la anulación igual que la base.
        Assert.True(devuelta.Anulada);
        Assert.True(cargadaAntes.Anulada);

        var guardada = LeerGuardada();
        Assert.True(guardada.Anulada);
        Assert.Equal(MotivoPrimero, guardada.MotivoAnulacion);
        Assert.Equal(UsuarioPrimero, guardada.AnuladaPorUsuarioId);
        Assert.NotNull(guardada.FechaAnulacion);
    }

    /// <summary>
    /// Dos pestañas: el segundo pedido llega cuando el primero ya terminó.
    /// </summary>
    [Fact]
    public async Task Anular_una_operacion_ya_anulada_avisa_y_no_pisa_la_primera_anulacion()
    {
        await Anular(MotivoPrimero, UsuarioPrimero);
        var primera = LeerGuardada();

        await Assert.ThrowsAsync<OperacionYaAnuladaException>(() => Anular(MotivoSegundo, UsuarioSegundo));

        var guardada = LeerGuardada();
        Assert.Equal(MotivoPrimero, guardada.MotivoAnulacion);
        Assert.Equal(UsuarioPrimero, guardada.AnuladaPorUsuarioId);
        Assert.Equal(primera.FechaAnulacion, guardada.FechaAnulacion);
    }

    /// <summary>
    /// Dos personas a la vez, en el peor momento: la primera termina justo antes de que
    /// se ejecute el UPDATE de la segunda, cuando la segunda ya pasó cualquier control
    /// previo. Con dos tareas en paralelo el resultado dependería del azar: acá el
    /// orden lo fija el interceptor, y da siempre lo mismo.
    /// </summary>
    [Fact]
    public async Task Si_dos_personas_anulan_a_la_vez_gana_la_primera_y_la_segunda_recibe_el_aviso()
    {
        DateTime? fechaPrimera = null;
        var interceptor = new AntesDelUpdateDeOperaciones(async () =>
        {
            await Anular(MotivoPrimero, UsuarioPrimero);
            fechaPrimera = LeerGuardada().FechaAnulacion;
        });
        var opciones = new DbContextOptionsBuilder<RetencionesRG830DbContext>(_base.Opciones)
            .AddInterceptors(interceptor)
            .Options;

        using var db = new RetencionesRG830DbContext(opciones);
        var error = await Record.ExceptionAsync(
            () => new ServicioRegistroOperaciones(db).AnularAsync(AlcanceEmpresa.Estudio, _operacionId, MotivoSegundo, UsuarioSegundo));

        Assert.True(interceptor.SeDisparo, "El interceptor nunca vio el UPDATE: el test no probó nada.");
        var guardada = LeerGuardada();
        Assert.Equal(MotivoPrimero, guardada.MotivoAnulacion);
        Assert.Equal(UsuarioPrimero, guardada.AnuladaPorUsuarioId);
        Assert.Equal(fechaPrimera, guardada.FechaAnulacion);
        Assert.IsType<OperacionYaAnuladaException>(error);
    }

    /// <summary>
    /// Una operación que no existe es otro caso, y no se le puede decir al operador que
    /// "ya estaba anulada".
    ///
    /// Desde la HU-15 lanza OperacionNoEncontradaException, que la pantalla convierte en
    /// 404. Antes lanzaba una InvalidOperationException genérica, que nadie capturaba, y
    /// el operador veía un error 500.
    /// </summary>
    [Fact]
    public async Task Anular_una_operacion_que_no_existe_no_se_confunde_con_una_ya_anulada()
    {
        using var db = _base.NuevoContexto();

        await Assert.ThrowsAsync<OperacionNoEncontradaException>(
            () => new ServicioRegistroOperaciones(db).AnularAsync(AlcanceEmpresa.Estudio, 999_999, MotivoPrimero, UsuarioPrimero));
    }

    /// <summary>
    /// Ejecuta una acción una sola vez, justo antes del primer UPDATE sobre Operaciones.
    /// Mira los dos caminos porque EF Core manda un UPDATE por uno u otro según se haga
    /// con SaveChanges o con ExecuteUpdate.
    /// </summary>
    private sealed class AntesDelUpdateDeOperaciones : DbCommandInterceptor
    {
        private readonly Func<Task> _accion;

        public bool SeDisparo { get; private set; }

        public AntesDelUpdateDeOperaciones(Func<Task> accion) => _accion = accion;

        private async Task Disparar(DbCommand comando)
        {
            if (SeDisparo
                || !comando.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                || !comando.CommandText.Contains("Operaciones", StringComparison.OrdinalIgnoreCase))
                return;

            SeDisparo = true;
            await _accion();
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await Disparar(command);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await Disparar(command);
            return result;
        }
    }
}
