#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
mkdir -p bin

if [ -x "bin/playit-linux-amd64" ]; then
  echo "playit.gg ja esta instalado."
  exit 0
fi

echo "Baixando agente playit.gg..."
curl -fL "https://github.com/playit-cloud/playit-agent/releases/latest/download/playit-linux-amd64" -o "bin/playit-linux-amd64"
chmod +x "bin/playit-linux-amd64"
mkdir -p "$HOME/.local/bin"
ln -sf "$(pwd)/bin/playit-linux-amd64" "$HOME/.local/bin/playit"
echo "playit.gg instalado."
echo "Comando disponivel: playit"
