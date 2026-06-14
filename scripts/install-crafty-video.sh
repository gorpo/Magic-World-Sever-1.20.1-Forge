#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

echo "Atualizando pacotes e instalando dependencias do video..."
sudo apt update
sudo apt upgrade -y
sudo apt install -y git python3-pip curl

python3 -m pip install --user distro

if [ ! -d "crafty-installer-4.0" ]; then
  git clone https://gitlab.com/crafty-controller/crafty-installer-4.0.git
fi

cat <<MSG

Quando o instalador perguntar o diretorio de instalacao, use:
$(pwd)/minecraft

Depois de instalar, rode:
$(pwd)/minecraft/run_crafty.sh

MSG

cd crafty-installer-4.0
sudo ./install_crafty.sh
