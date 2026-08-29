namespace RetencionesRG830.Domain.Enums;

/// <summary>
/// Los dos roles que puede tener una persona que usa el sistema.
///
/// Estudio: es el personal del estudio contable. Ve y administra TODOS los
/// clientes (agentes de retención), sus proveedores, y las tablas normativas
/// (Régimen y TramoEscala) que rigen para todos.
///
/// Cliente: es el operador de un agente de retención puntual (por ejemplo,
/// GIMI S.A.). Sólo ve y carga las operaciones de SU cliente, nunca las de otro.
/// </summary>
public enum RolUsuario
{
    Estudio = 0,
    Cliente = 1
}
