#!/bin/bash
# Script auxiliar: agrega la columna "Activo" a la tabla Proveedores (para
# poder darlos de baja sin borrarlos) y aplica el cambio a la base de datos.
set -e

echo "== Creando la migración AgregarActivoAProveedor =="
dotnet ef migrations add AgregarActivoAProveedor --project src/RetencionesRG830.Infrastructure --startup-project src/RetencionesRG830.Web

echo ""
echo "== Aplicando la migración a la base de datos =="
dotnet ef database update --project src/RetencionesRG830.Infrastructure --startup-project src/RetencionesRG830.Web

echo ""
echo "Listo. La tabla Proveedores ahora tiene la columna Activo."
