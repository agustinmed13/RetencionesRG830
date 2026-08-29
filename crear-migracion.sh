#!/bin/bash
# Script auxiliar: crea la migración inicial de la base de datos y la aplica.
# Se corre una sola vez (o cada vez que cambien las entidades del Domain).
set -e

echo "== Creando la migración InicialEsquema =="
dotnet ef migrations add InicialEsquema --project src/RetencionesRG830.Infrastructure --startup-project src/RetencionesRG830.Web

echo ""
echo "== Aplicando la migración a la base de datos (retenciones.db) =="
dotnet ef database update --project src/RetencionesRG830.Infrastructure --startup-project src/RetencionesRG830.Web

echo ""
echo "Listo. Se creó src/RetencionesRG830.Web/retenciones.db"
