#!/bin/bash
# Devuelve el proyecto Infrastructure a su lugar correcto.
#
# Ojo: en src/RetencionesRG830.Infrastructure puede haber quedado una
# carpeta vacía con sólo bin/ y obj/ (no se movieron porque estaban en
# uso). Por eso movemos el CONTENIDO y no la carpeta entera.
set -e

ORIGEN="src/RetencionesRG830.Web/RetencionesRG830.Infrastructure"
DESTINO="src/RetencionesRG830.Infrastructure"

if [ -f "$DESTINO/RetencionesRG830.Infrastructure.csproj" ]; then
  echo "El proyecto ya está en su lugar. No hay nada que mover."
else
  if [ ! -f "$ORIGEN/RetencionesRG830.Infrastructure.csproj" ]; then
    echo "ERROR: no encuentro el proyecto en $ORIGEN."
    exit 1
  fi

  echo "== Moviendo el contenido de $ORIGEN a $DESTINO =="
  mkdir -p "$DESTINO"
  mv "$ORIGEN"/* "$DESTINO"/
  rmdir "$ORIGEN" 2>/dev/null || true
  echo "Movido."
fi

echo ""
echo "Contenido de $DESTINO:"
ls -1 "$DESTINO"

echo ""
echo "== Compilando =="
dotnet build
