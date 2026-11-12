namespace RetencionesRG830.Application.Servicios;

/// <summary>
/// Qué empresas (clientes del estudio) puede ver un usuario. HU-15, RF-04 y RN-17: el
/// usuario del rol Cliente opera sólo sobre su propia empresa; el del rol Estudio, sobre
/// todas.
///
/// Lo reciben, como parámetro obligatorio, los métodos de ServicioRegistroOperaciones
/// que leen o escriben datos de una empresa. Obligatorio a propósito: no hay una versión
/// del método que, si se olvida el alcance, devuelva todo. Un olvido tiene que ser un
/// error de compilación, no una fuga de datos.
///
/// Un dato fuera del alcance se trata como inexistente, no como prohibido: decirle a un
/// usuario "no tenés permiso para la operación 7" le confirma que la operación 7 existe.
///
/// No se puede construir con new: sólo con Estudio, DelCliente o Ninguna, para que cada
/// uso diga cuál de los tres casos es.
/// </summary>
public sealed class AlcanceEmpresa
{
    private readonly bool _todas;

    private AlcanceEmpresa(bool todas, int? clienteId)
    {
        _todas = todas;
        ClienteId = clienteId;
    }

    /// <summary>Todas las empresas: el rol Estudio.</summary>
    public static AlcanceEmpresa Estudio { get; } = new(true, null);

    /// <summary>Sólo esa empresa: un usuario del rol Cliente.</summary>
    public static AlcanceEmpresa DelCliente(int clienteId) => new(false, clienteId);

    /// <summary>
    /// Ninguna empresa. Es lo que corresponde a un usuario Cliente sin empresa asociada,
    /// que sería un dato mal cargado: ante la duda, no ve nada.
    /// </summary>
    public static AlcanceEmpresa Ninguna { get; } = new(false, null);

    /// <summary>La empresa del usuario, o null si ve todas (Estudio) o ninguna.</summary>
    public int? ClienteId { get; }

    /// <summary>true para el Estudio: ve todas las empresas.</summary>
    public bool TodasLasEmpresas => _todas;

    /// <summary>true si el usuario puede ver los datos de esa empresa.</summary>
    public bool Incluye(int clienteId) => _todas || ClienteId == clienteId;
}
