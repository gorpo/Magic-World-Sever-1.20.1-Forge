#!/bin/sh
set -eu

if [ "${EULA:-false}" = "true" ] || [ "${EULA:-false}" = "TRUE" ]; then
  echo "eula=true" > eula.txt
else
  echo "Set EULA=true to confirm that you accept https://aka.ms/MinecraftEULA"
  echo "eula=false" > eula.txt
  exit 1
fi

exec java @user_jvm_args.txt @libraries/net/minecraftforge/forge/1.20.1-47.4.20/unix_args.txt nogui
