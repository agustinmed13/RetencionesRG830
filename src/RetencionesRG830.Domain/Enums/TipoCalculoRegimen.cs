namespace RetencionesRG830.Domain.Enums;

/// <summary>
/// Cada Régimen de la RG 830 calcula la retención de una de estas dos formas
/// (es el mismo "IF(C22=s/escala, ..., ...)" que vimos en la fórmula de la
/// celda C25 de la planilla Excel):
///
/// TasaFija: se multiplica el neto sujeto a retención por un porcentaje fijo.
/// Es el caso de, por ejemplo, el régimen 78 (venta de bienes muebles).
///
/// EscalaProgresiva: se busca en qué tramo de TramoEscala cae el neto sujeto
/// a retención, y se aplica "monto fijo del tramo + porcentaje del tramo
/// sobre el excedente del piso del tramo". Es el caso del régimen 116
/// (honorarios de directores).
/// </summary>
public enum TipoCalculoRegimen
{
    TasaFija = 0,
    EscalaProgresiva = 1
}
