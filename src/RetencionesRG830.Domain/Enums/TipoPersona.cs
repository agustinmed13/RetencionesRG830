namespace RetencionesRG830.Domain.Enums;

/// <summary>
/// La RG 830 aplica una tasa distinta según el tipo de persona del proveedor
/// cuando ese proveedor NO está inscripto en Ganancias. Esto es exactamente
/// el dato que en la planilla Excel se cargaba en la celda "Tipo Persona"
/// (C16), con las opciones "Humana y Suc Indivisa" o "Resto".
///
/// Si el proveedor SÍ está inscripto en Ganancias, este dato no afecta el
/// cálculo (se usa siempre la tasa de "Inscriptos").
/// </summary>
public enum TipoPersona
{
    HumanaYSucesionIndivisa = 0,
    Resto = 1
}
