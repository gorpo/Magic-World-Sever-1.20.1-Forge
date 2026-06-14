#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

bash scripts/setup-forge.sh
bash scripts/install-playit.sh

if ! grep -q '^eula=true' eula.txt 2>/dev/null; then
  cat <<'MSG'
Para iniciar, leia e aceite a EULA da Mojang/Microsoft:
https://aka.ms/MinecraftEULA

Depois altere eula.txt para:
eula=true
MSG
  exit 1
fi

echo "Iniciando playit.gg em segundo plano..."
"$(pwd)/bin/playit-linux-amd64" > playit.log 2>&1 &
PLAYIT_PID=$!

sleep 5
echo
echo "=== playit.gg ==="
tail -n 80 playit.log || true
echo "================="
echo
echo "Se aparecer um link de claim do playit.gg, abra, conecte sua conta e crie um tunnel Minecraft Java para 127.0.0.1:25565."
echo "Depois use o endereco publico do playit.gg no Minecraft."
echo
echo "Iniciando Minecraft Forge..."

trap 'kill "$PLAYIT_PID" 2>/dev/null || true' EXIT
exec java @user_jvm_args.txt @libraries/net/minecraftforge/forge/1.20.1-47.4.20/unix_args.txt nogui
