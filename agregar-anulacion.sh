#!/bin/bash
# Script auxiliar: agrega a la tabla Operaciones las columnas de anulación
# (Anulada, FechaAnulacion, MotivoAnulacion, AnuladaPorUsuarioId) y aplica
# el cambio a la base de datos.
set -e

echo "== Creando la migración AgregarAnulacionDeOperaciones =="
dotnet ef migrations add AgregarAnulacionDeOperaciones --project src/RetencionesRG830.Infrastructure --startup-project src/RetencionesRG830.Web

echo ""
echo "== Aplicando la migración a la base de datos =="
dotnet ef database update --project src/RetencionesRG830.Infrastructure --startup-project src/RetencionesRG830.Web

echo ""
echo "Listo. Las operaciones ya cargadas quedan como NO anuladas, que es lo correcto."
