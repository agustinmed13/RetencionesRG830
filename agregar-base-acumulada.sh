#!/bin/bash
# Script auxiliar: agrega la columna "BaseCalculoAcumulada" a la tabla
# Operaciones (la base sobre la que se calculó cada retención, que es lo
# que informa el archivo para SICORE) y aplica el cambio a la base de datos.
set -e

echo "== Creando la migración AgregarBaseCalculoAcumulada =="
dotnet ef migrations add AgregarBaseCalculoAcumulada --project src/RetencionesRG830.Infrastructure --startup-project src/RetencionesRG830.Web

echo ""
echo "== Aplicando la migración a la base de datos =="
dotnet ef database update --project src/RetencionesRG830.Infrastructure --startup-project src/RetencionesRG830.Web

echo ""
echo "Listo. La tabla Operaciones ahora tiene la columna BaseCalculoAcumulada."
echo "Ojo: las operaciones ya cargadas quedan con 0 en ese campo. Conviene"
echo "borrarlas y volver a cargarlas para que el archivo de SICORE salga bien."
