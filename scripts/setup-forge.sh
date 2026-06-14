#!/usr/bin/env bash
set -euo pipefail

FORGE_VERSION="1.20.1-47.4.20"
INSTALLER="forge-${FORGE_VERSION}-installer.jar"
URL="https://maven.minecraftforge.net/net/minecraftforge/forge/${FORGE_VERSION}/${INSTALLER}"

cd "$(dirname "$0")/.."

if [ -f "libraries/net/minecraftforge/forge/1.20.1-47.4.20/unix_args.txt" ]; then
  echo "Forge ${FORGE_VERSION} ja esta instalado."
  exit 0
fi

echo "Baixando Forge ${FORGE_VERSION}..."
curl -fsSL "$URL" -o "$INSTALLER"

echo "Instalando servidor Forge..."
java -jar "$INSTALLER" --installServer

rm -f "$INSTALLER" "${INSTALLER}.log"
echo "Servidor Forge instalado."
