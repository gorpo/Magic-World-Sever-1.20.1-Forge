# Comandos do video adaptados para este repositorio

Use estes comandos dentro do GitHub Codespaces, um por vez.

```bash
sudo apt update && sudo apt upgrade -y && sudo apt install -y git python3-pip curl
```

```bash
pip install distro
```

```bash
git clone https://gitlab.com/crafty-controller/crafty-installer-4.0.git
```

```bash
cd crafty-installer-4.0
```

```bash
sudo ./install_crafty.sh
```

Quando o instalador perguntar o diretorio de instalacao, use o caminho do seu
Codespace. Neste repo deve ficar parecido com:

```bash
/workspaces/Magic-World-Sever-1.20.1-Forge/minecraft
```

Depois rode o Crafty:

```bash
/workspaces/Magic-World-Sever-1.20.1-Forge/minecraft/run_crafty.sh
```

Em outro terminal, instale o playit:

```bash
bash scripts/install-playit.sh
```

Depois rode:

```bash
playit
```

No painel do playit.gg, crie um tunnel Minecraft Java apontando para:

```txt
127.0.0.1:25565
```

## Jeito mais rapido

Eu tambem deixei um script que executa a parte de instalacao do Crafty:

```bash
bash scripts/install-crafty-video.sh
```
