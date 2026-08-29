#!/bin/bash
# Script auxiliar: agrega la columna "MinimoRetencionNoInscripto" a la tabla
# Regimenes (el mínimo de retención diferenciado para proveedores no
# inscriptos, $1.020 en alquileres de inmuebles urbanos) y aplica el cambio
# a la base de datos.
set -e

echo "== Creando la migración AgregarMinimoRetencionNoInscripto =="
dotnet ef migrations add AgregarMinimoRetencionNoInscripto --project src/RetencionesRG830.Infrastructure --startup-project src/RetencionesRG830.Web

echo ""
echo "== Aplicando la migración a la base de datos =="
dotnet ef database update --project src/RetencionesRG830.Infrastructure --startup-project src/RetencionesRG830.Web

echo ""
echo "Listo. La tabla Regimenes ahora tiene la columna MinimoRetencionNoInscripto."
