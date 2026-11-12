using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;

namespace RetencionesRG830.Infrastructure.Servicios;

/// <summary>
/// Registra una operación aplicando toda la regla del régimen: busca el
/// acumulado del mes, calcula la retención y asigna el número de certificado.
///
/// Vive acá y no en Application porque necesita consultar la base de datos.
/// Está separado del controlador para poder probarlo automáticamente: un
/// controlador necesita un servidor web corriendo para ejecutarse, un
/// servicio como este no.
///
/// HU-15 (RF-04, RN-17): cada método que lee o escribe datos de una empresa recibe el
/// AlcanceEmpresa del usuario como primer parámetro, obligatorio. Lo que queda fuera del
/// alcance se comporta como si no existiera: null, una lista vacía, o el mismo error
/// que da un Id inexistente. El filtro va acá y no en el controlador porque acá lo
/// pueden probar los tests (AislamientoEmpresasTests).
///
/// No reciben alcance los métodos sin datos de una empresa (VersionVigenteAsync,
/// RegimenesAsync) ni los del archivo de SICORE, que es una función exclusiva del rol
/// Estudio: SicoreController la exige con [Authorize(Roles = "Estudio")].
/// </summary>
public class ServicioRegistroOperaciones
{
    private readonly RetencionesRG830DbContext _db;

    public ServicioRegistroOperaciones(RetencionesRG830DbContext db)
    {
        _db = db;
    }

    /// <summary>El mensaje para un cliente inexistente o fuera del alcance: es el mismo.</summary>
    private const string ClienteInexistente = "El cliente indicado no existe.";

    /// <summary>
    /// Deja en la consulta sólo las operaciones de las empresas que el usuario puede
    /// ver. Cada consulta de operaciones de una empresa pasa por acá.
    /// </summary>
    private static IQueryable<Operacion> DeEmpresasVisibles(IQueryable<Operacion> operaciones, AlcanceEmpresa alcance) =>
        alcance.TodasLasEmpresas ? operaciones
        : alcance.ClienteId is int clienteId ? operaciones.Where(o => o.ClienteId == clienteId)
        : operaciones.Where(o => false);

    /// <summary>
    /// La versión del régimen que rige en la fecha (regla 9), con sus tramos de
    /// escala, o null si ninguna rige. La usan el formulario, para resolver el
    /// código elegido, y el seed. Cuál rige lo decide VigenciaRegimen, la misma
    /// regla que usa la pantalla de Regímenes.
    /// </summary>
    public async Task<Regimen?> VersionVigenteAsync(int codigoRegimen, DateOnly fecha)
    {
        var versiones = await _db.Regimenes
            .Include(r => r.Tramos)
            .Where(r => r.Codigo == codigoRegimen)
            .ToListAsync();

        return VigenciaRegimen.VersionVigente(versiones, fecha);
    }

    /// <summary>
    /// Calcula la retención SIN guardar nada, para poder mostrarla antes de
    /// confirmar. Deja cargadas en la operación recibida la base acumulada y la
    /// versión del régimen que se usó.
    ///
    /// El régimen es el CÓDIGO; la versión es una consecuencia de la fecha, no una
    /// decisión de quien llama (regla 9). Por eso el RegimenId recibido sólo se usa
    /// para saber de qué código se trata: la versión se resuelve acá con la fecha
    /// de retención, y la operación queda con el RegimenId de esa versión, que es
    /// lo que permite auditar después con qué valores se calculó. Así ni el seed,
    /// ni un formulario alterado, ni ningún otro código pueden calcular con una
    /// versión que no rige en esa fecha.
    ///
    /// La empresa de la operación tiene que estar en el alcance: el resultado muestra
    /// el acumulado del mes y lo ya retenido, que son datos de esa empresa.
    /// </summary>
    public async Task<ResultadoCalculoRetencion> SimularAsync(AlcanceEmpresa alcance, Operacion operacion)
    {
        if (!alcance.Incluye(operacion.ClienteId))
            throw new InvalidOperationException(ClienteInexistente);

        var proveedor = await _db.Proveedores.FindAsync(operacion.ProveedorId)
            ?? throw new InvalidOperationException("El proveedor indicado no existe.");

        // Un proveedor pertenece a un único cliente (regla 8). Si se aceptara uno
        // de otro cliente, el acumulado del mes se buscaría sobre las operaciones de
        // otro agente de retención y la operación ensuciaría el acumulado de las dos
        // empresas. Se controla acá, que es por donde pasan la simulación y el
        // registro, aunque la pantalla ya lo valide antes con un mensaje amable.
        if (proveedor.ClienteId != operacion.ClienteId)
            throw new InvalidOperationException("El proveedor indicado no pertenece a este cliente.");

        var codigo = await _db.Regimenes
            .Where(r => r.Id == operacion.RegimenId)
            .Select(r => (int?)r.Codigo)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("El régimen indicado no existe.");

        var regimen = await VersionVigenteAsync(codigo, operacion.FechaRetencion)
            ?? throw new InvalidOperationException(
                $"El régimen {codigo} no tiene valores vigentes al {operacion.FechaRetencion:dd/MM/yyyy}.");
        operacion.RegimenId = regimen.Id;

        var acumulado = await CalcularAcumuladoDelMesAsync(operacion);

        operacion.BaseCalculoAcumulada = acumulado.Gravado + operacion.ImporteGravado;

        return ServicioCalculoRetencion.Calcular(
            regimen,
            proveedor.InscriptoEnGanancias,
            proveedor.TipoPersona,
            operacion.BaseCalculoAcumulada,
            acumulado.Retenido);
    }

    /// <summary>
    /// Confirma la operación: calcula, le asigna el número de certificado y
    /// la guarda. Reutiliza SimularAsync para que el número que se le mostró
    /// al usuario y el que se guarda salgan del mismo cálculo.
    ///
    /// Antes de nada controla que el cliente esté activo (regla 11, consulta al
    /// contador de octubre de 2026): no existen operaciones atrasadas de un cliente
    /// dado de baja, porque el estudio lo da de baja el día 1 de un mes, después de
    /// cerrar el anterior. La pantalla lo controla antes con un mensaje amable; acá
    /// se vuelve a controlar porque es por donde pasa todo registro.
    /// </summary>
    public async Task<Operacion> RegistrarAsync(AlcanceEmpresa alcance, Operacion operacion)
    {
        // Una empresa fuera del alcance da el mismo error que una inexistente (HU-15):
        // se controla antes de leerla, para no decir nada de ella, ni que está de baja.
        var cliente = alcance.Incluye(operacion.ClienteId)
            ? await _db.Clientes.FindAsync(operacion.ClienteId)
            : null;
        if (cliente is null)
            throw new InvalidOperationException(ClienteInexistente);
        if (!cliente.Activo)
            throw new InvalidOperationException(
                $"El cliente {cliente.RazonSocial} está dado de baja: no se le pueden registrar operaciones.");

        var resultado = await SimularAsync(alcance, operacion);

        operacion.MontoRetenido = resultado.RetencionAPracticar;

        // Regla 10 (consulta al contador, octubre de 2026): "Una operación podría no
        // generar retención si no alcanza el mínimo, por lo que no debería emitirse
        // ningún certificado." Sin certificado cuando la retención a practicar es
        // $ 0, por cualquier causa: el acumulado no llegó al monto no sujeto, o lo
        // que falta retener queda debajo del mínimo. La operación igual se guarda,
        // porque suma al acumulado del mes. Sin número no consume correlativo, así
        // que los certificados emitidos no tienen saltos.
        operacion.NumeroCertificado = resultado.RetencionAPracticar > 0
            ? await SiguienteNumeroCertificadoAsync(alcance, operacion.ClienteId)
            : null;
        operacion.FechaCarga = DateTime.Now;

        _db.Operaciones.Add(operacion);
        await _db.SaveChangesAsync();

        return operacion;
    }

    /// <summary>
    /// Suma lo gravado y lo ya retenido a ese proveedor, en ese régimen,
    /// dentro del mes de la fecha de retención. NO incluye la operación
    /// que se está por registrar.
    ///
    /// "Ese régimen" es el CÓDIGO, no la versión: se agrupa por Regimen.Codigo y
    /// no por RegimenId. Si una versión nueva empieza a mitad de mes, las
    /// operaciones calculadas con la vieja y con la nueva son del mismo régimen y
    /// tienen que acumularse (reglas 1, 2 y 3). Agrupando por RegimenId quedaban
    /// separadas: el piso se aplicaba dos veces y no se descontaba lo ya retenido.
    ///
    /// Es privado: devuelve datos de una empresa sin recibir alcance, y sólo lo usa
    /// SimularAsync, que controla el alcance antes de llamarlo. Antes de la HU-15 era
    /// público, aunque nadie más lo usaba.
    /// </summary>
    private async Task<(decimal Gravado, decimal Retenido)> CalcularAcumuladoDelMesAsync(Operacion operacion)
    {
        var primerDia = new DateOnly(operacion.FechaRetencion.Year, operacion.FechaRetencion.Month, 1);
        var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

        var codigo = await _db.Regimenes
            .Where(r => r.Id == operacion.RegimenId)
            .Select(r => r.Codigo)
            .SingleAsync();

        var anteriores = await _db.Operaciones
            .Where(o => o.ClienteId == operacion.ClienteId
                     && o.ProveedorId == operacion.ProveedorId
                     && o.Regimen!.Codigo == codigo
                     && o.Id != operacion.Id
                     && !o.Anulada
                     && o.FechaRetencion >= primerDia
                     && o.FechaRetencion <= ultimoDia)    
            .ToListAsync();

        return (anteriores.Sum(o => o.ImporteGravado), anteriores.Sum(o => o.MontoRetenido));
    }

    /// <summary>
    /// true si el proveedor existe y es de ese cliente. Lo usan la validación del
    /// formulario y el cálculo en vivo, para avisar antes de llegar al error de
    /// SimularAsync.
    /// </summary>
    public Task<bool> ProveedorPerteneceAlClienteAsync(AlcanceEmpresa alcance, int proveedorId, int clienteId) =>
        alcance.Incluye(clienteId)
            ? _db.Proveedores.AnyAsync(p => p.Id == proveedorId && p.ClienteId == clienteId)
            : Task.FromResult(false);

    /// <summary>
    /// Las operaciones vigentes del mismo cliente y proveedor con el mismo comprobante
    /// (tipo, punto de venta y número). NO es un error: la retención se practica al
    /// pagar, no al facturar, así que una factura pagada en dos cuotas da dos
    /// retenciones legítimas con el mismo comprobante. Sirve para avisarle al
    /// operador, que decide. Las anuladas no cuentan: volver a cargar un comprobante
    /// después de anular su operación es la forma normal de corregir un error.
    /// Pendiente de confirmar con el contador del estudio (ver CLAUDE.md).
    ///
    /// Fuera del alcance devuelve una lista vacía: el aviso muestra número de
    /// certificado y fecha de las operaciones encontradas.
    /// </summary>
    public Task<List<Operacion>> OperacionesConMismoComprobanteAsync(AlcanceEmpresa alcance, Operacion operacion) =>
        DeEmpresasVisibles(_db.Operaciones, alcance)
            .Where(o => o.ClienteId == operacion.ClienteId
                     && o.ProveedorId == operacion.ProveedorId
                     && o.TipoComprobante == operacion.TipoComprobante
                     && o.PuntoVenta == operacion.PuntoVenta
                     && o.NumeroComprobante == operacion.NumeroComprobante
                     && o.Id != operacion.Id
                     && !o.Anulada)
            .OrderBy(o => o.NumeroCertificado)
            .ToListAsync();

    /// <summary>
    /// El próximo número de certificado correlativo del cliente. Es público para
    /// poder anticiparlo en la pantalla de confirmación; el número definitivo se
    /// vuelve a calcular en RegistrarAsync, en el momento de guardar.
    ///
    /// Fuera del alcance es un error: el número diría cuántos certificados emitió
    /// otra empresa.
    /// </summary>
    public async Task<int> SiguienteNumeroCertificadoAsync(AlcanceEmpresa alcance, int clienteId)
    {
        if (!alcance.Incluye(clienteId))
            throw new InvalidOperationException(ClienteInexistente);

        // Max ignora las operaciones sin número (las que no retuvieron) y cuenta
        // las anuladas, que conservan el suyo: así el número no se reutiliza.
        var ultimo = await _db.Operaciones
            .Where(o => o.ClienteId == clienteId)
            .MaxAsync(o => o.NumeroCertificado) ?? 0;

        return ultimo + 1;
    }

    /// <summary>
    /// Las operaciones que se informan en el archivo de SICORE: las del cliente con
    /// fecha de retención dentro del rango que emitieron certificado y no están
    /// anuladas, con proveedor y régimen cargados, que necesita el generador. Una
    /// operación sin certificado no retuvo nada, así que no hay retención que
    /// informar (regla 10).
    ///
    /// El filtro es por fecha de RETENCIÓN, que es la que define el período que se
    /// presenta, no la fecha del comprobante. Antes esta consulta estaba en
    /// SicoreController, sin test: qué se le informa a AFIP no puede quedar sin
    /// probar.
    /// </summary>
    public Task<List<Operacion>> OperacionesParaSicoreAsync(int clienteId, DateOnly desde, DateOnly hasta) =>
        _db.Operaciones
            .Include(o => o.Proveedor)
            .Include(o => o.Regimen)
            .Where(o => o.ClienteId == clienteId
                     && !o.Anulada
                     && o.NumeroCertificado != null
                     && o.FechaRetencion >= desde
                     && o.FechaRetencion <= hasta)
            .ToListAsync();

    /// <summary>
    /// Los clientes para los que se puede generar el archivo de SICORE: TODOS, también
    /// los dados de baja. Primero los activos, que son los habituales, y después los de
    /// baja, cada grupo por razón social.
    ///
    /// Por la regla 11: el estudio da de baja a un cliente el día 1 de un mes, después
    /// de cerrar el anterior, y el SICORE de ese mes anterior se presenta más tarde, con
    /// el cliente ya de baja. Generar el archivo no carga nada nuevo: informa lo que ya
    /// se retuvo. Es distinto de Nueva operación, donde sólo van los activos porque ahí
    /// sí se registra algo (RegistrarAsync rechaza a un cliente de baja).
    /// </summary>
    public Task<List<Cliente>> ClientesParaSicoreAsync() =>
        _db.Clientes
            .OrderBy(c => c.Activo ? 0 : 1)
            .ThenBy(c => c.RazonSocial)
            .ToListAsync();

    /// <summary>
    /// Cuántas operaciones del período entran en el archivo de SICORE y cuántas
    /// quedan afuera, y por qué. Lo muestra la pantalla de SICORE antes de descargar.
    ///
    /// Las que entran se cuentan con OperacionesParaSicoreAsync, el mismo método que
    /// arma el archivo, y no con una consulta propia: si el criterio de qué se informa
    /// cambia, el número que ve el operador cambia con él. Trae las entidades sólo
    /// para contarlas, lo que con el volumen esperado (decenas por cliente y período)
    /// no pesa. Las anuladas se cuentan todas, tengan o no certificado: para el
    /// operador son "anuladas" (así figuran en el listado), no "sin certificado".
    /// </summary>
    public async Task<ResumenPeriodoSicore> ResumenPeriodoSicoreAsync(int clienteId, DateOnly desde, DateOnly hasta)
    {
        var informadas = (await OperacionesParaSicoreAsync(clienteId, desde, hasta)).Count;

        var delPeriodo = _db.Operaciones
            .Where(o => o.ClienteId == clienteId
                     && o.FechaRetencion >= desde
                     && o.FechaRetencion <= hasta);

        var anuladas = await delPeriodo.CountAsync(o => o.Anulada);
        var sinCertificado = await delPeriodo.CountAsync(o => !o.Anulada && o.NumeroCertificado == null);

        return new ResumenPeriodoSicore(informadas, anuladas, sinCertificado);
    }

    /// <summary>
    /// Anula una operación. No la borra: queda registrada con el motivo,
    /// la fecha y quién la anuló, pero deja de sumar al acumulado del mes
    /// y no sale en el archivo de SICORE.
    ///
    /// Vale la primera anulación: si ya estaba anulada lanza
    /// OperacionYaAnuladaException y no cambia nada. El motivo, el responsable y la
    /// fecha son el respaldo frente a una inspección.
    ///
    /// Por eso el control va dentro del mismo UPDATE ("WHERE Anulada = 0") y no se
    /// lee primero. Antes se leía, se preguntaba y se guardaba: dos personas a la vez
    /// leían las dos "no anulada", y la segunda pisaba a la primera sin ningún error.
    /// Así decide la base, en un solo paso: uno de los dos pedidos encuentra cero filas.
    ///
    /// La empresa va en el mismo WHERE (HU-15): una operación de otra empresa no se
    /// toca. Si no existe o está fuera del alcance, lanza OperacionNoEncontradaException,
    /// y eso se decide ANTES de mirar si estaba anulada: "ya estaba anulada" confirmaría
    /// que existe. Antes de la HU-15, un Id inexistente daba InvalidOperationException y
    /// la pantalla mostraba un error 500.
    /// </summary>
    public async Task<Operacion> AnularAsync(AlcanceEmpresa alcance, int operacionId, string motivo, int usuarioId)
    {
        DateTime? fecha = DateTime.Now;
        var anuladas = await DeEmpresasVisibles(_db.Operaciones, alcance)
            .Where(o => o.Id == operacionId && !o.Anulada)
            .ExecuteUpdateAsync(cambios => cambios
                .SetProperty(o => o.Anulada, true)
                .SetProperty(o => o.MotivoAnulacion, motivo)
                .SetProperty(o => o.FechaAnulacion, fecha)
                .SetProperty(o => o.AnuladaPorUsuarioId, usuarioId));

        var operacion = await _db.Operaciones.FindAsync(operacionId);
        if (operacion is null || !alcance.Incluye(operacion.ClienteId))
            throw new OperacionNoEncontradaException();

        if (anuladas == 0)
        {
            throw new OperacionYaAnuladaException();
        }

        // ExecuteUpdate escribe directo en la base, sin pasar por las entidades: una que
        // este contexto ya tuviera en memoria seguiría diciendo que está vigente.
        await _db.Entry(operacion).ReloadAsync();

        return operacion;
    }

    // -------------------------------------------------------------------------
    // Consultas de las pantallas de Operaciones.
    //
    // Hasta noviembre de 2026 estaban escritas en OperacionesController. Se mudaron
    // acá primero sin cambiarlas, y después se les agregó el alcance de la HU-15 (el
    // rol Cliente ve sólo su empresa): ese filtro tiene que ir en el servicio, que es
    // lo que los tests pueden probar.
    // Cada una es la misma que tenía el controlador, incluido FindAsync donde lo
    // usaba: FindAsync mira primero las entidades que el contexto ya tiene en
    // memoria, y el servicio comparte el contexto del pedido con el controlador.
    // -------------------------------------------------------------------------

    /// <summary>
    /// El listado de operaciones, con cliente, proveedor y régimen. Con clienteId,
    /// sólo las de ese cliente; sin él, las de todos. En los dos casos, sólo las de
    /// las empresas del alcance: un Cliente que pide las de otra recibe una lista vacía.
    /// </summary>
    public Task<List<Operacion>> ListarOperacionesAsync(AlcanceEmpresa alcance, int? clienteId)
    {
        var query = DeEmpresasVisibles(_db.Operaciones, alcance)
            .Include(o => o.Cliente)
            .Include(o => o.Proveedor)
            .Include(o => o.Regimen)
            .AsQueryable();

        if (clienteId is not null)
            query = query.Where(o => o.ClienteId == clienteId);

        return query
            // Dentro del mismo día, por orden de carga y no por número de certificado:
            // las operaciones que no retuvieron no tienen número (regla 10).
            .OrderByDescending(o => o.FechaRetencion)
            .ThenByDescending(o => o.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Una operación con cliente, proveedor y régimen, o null si no existe. La usan
    /// el Detalle, el certificado (el generador necesita los datos del cliente, el
    /// proveedor y el régimen, no sólo sus identificadores) y la pantalla de Anular.
    /// Fuera del alcance, null: igual que si no existiera.
    /// </summary>
    public Task<Operacion?> ObtenerOperacionAsync(AlcanceEmpresa alcance, int id) =>
        DeEmpresasVisibles(_db.Operaciones, alcance)
            .Include(o => o.Cliente)
            .Include(o => o.Proveedor)
            .Include(o => o.Regimen)
            .FirstOrDefaultAsync(o => o.Id == id);

    /// <summary>El cliente, o null si no existe o está fuera del alcance.</summary>
    public async Task<Cliente?> ObtenerClienteAsync(AlcanceEmpresa alcance, int clienteId) =>
        alcance.Incluye(clienteId) ? await _db.Clientes.FindAsync(clienteId) : null;

    /// <summary>
    /// El proveedor, o null si no existe o es de una empresa fuera del alcance. No mira
    /// a qué cliente de las visibles pertenece: para eso, ProveedorDelClienteAsync.
    /// </summary>
    public async Task<Proveedor?> ObtenerProveedorAsync(AlcanceEmpresa alcance, int proveedorId)
    {
        var proveedor = await _db.Proveedores.FindAsync(proveedorId);
        return proveedor is not null && alcance.Incluye(proveedor.ClienteId) ? proveedor : null;
    }

    /// <summary>
    /// El proveedor si existe y es de ese cliente; si no, null. A diferencia de
    /// ProveedorPerteneceAlClienteAsync, devuelve la entidad: la ayuda del régimen
    /// necesita saber si está inscripto y qué tipo de persona es. Con el cliente fuera
    /// del alcance, null.
    /// </summary>
    public Task<Proveedor?> ProveedorDelClienteAsync(AlcanceEmpresa alcance, int proveedorId, int clienteId) =>
        alcance.Incluye(clienteId)
            ? _db.Proveedores.FirstOrDefaultAsync(p => p.Id == proveedorId && p.ClienteId == clienteId)
            : Task.FromResult<Proveedor?>(null);

    /// <summary>
    /// Los proveedores activos del cliente, por razón social: los que ofrece el
    /// desplegable de Nueva operación. Con el cliente fuera del alcance, ninguno.
    /// </summary>
    public Task<List<Proveedor>> ProveedoresActivosAsync(AlcanceEmpresa alcance, int clienteId) =>
        alcance.Incluye(clienteId)
            ? _db.Proveedores
                .Where(p => p.ClienteId == clienteId && p.Activo)
                .OrderBy(p => p.RazonSocial)
                .ToListAsync()
            : Task.FromResult(new List<Proveedor>());

    /// <summary>
    /// Todas las versiones de todos los regímenes, sin los tramos de escala. El
    /// desplegable de Nueva operación las agrupa por código.
    /// </summary>
    public Task<List<Regimen>> RegimenesAsync() =>
        _db.Regimenes.ToListAsync();
}