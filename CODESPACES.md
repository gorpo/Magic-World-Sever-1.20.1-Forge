# Rodar online pelo GitHub Codespaces

Este e o fluxo parecido com tutoriais que usam `github.com`, Codespaces e
Crafty/playit.

## Servidor Forge direto

1. Abra o repositorio no GitHub.
2. Clique em `Code` > `Codespaces` > `Create codespace on main`.
3. Espere o Codespace terminar o `postCreateCommand`.
4. No terminal do Codespace rode:

```bash
bash scripts/run-online-codespace.sh
```

5. Na primeira execucao, o playit.gg vai mostrar um link de claim.
6. Abra o link, entre no playit.gg e crie um tunnel:

- Tipo: Minecraft Java
- Local address: `127.0.0.1`
- Local port: `25565`

7. Use o endereco publico gerado pelo playit.gg no Minecraft.

## Crafty Controller igual ao video

Se quiser seguir exatamente a linha do video, rode:

```bash
bash scripts/install-crafty-video.sh
```

Quando o instalador perguntar o diretorio, use:

```bash
/workspaces/Magic-World-Sever-1.20.1-Forge/minecraft
```

Depois rode:

```bash
/workspaces/Magic-World-Sever-1.20.1-Forge/minecraft/run_crafty.sh
```

Em outro terminal:

```bash
bash scripts/install-playit.sh
playit
```

No playit.gg, crie um tunnel Minecraft Java para `127.0.0.1:25565`.

## Crafty Controller via Docker

Para subir o painel Crafty no Codespaces ou numa VPS Linux:

```bash
docker compose -f crafty-compose.yml up -d
docker compose -f crafty-compose.yml logs -f
```

Depois abra a porta `8443` no painel `Ports` do Codespaces para acessar o
Crafty. Para Forge 1.20.1, selecione Java 17 dentro do Crafty.

## Limites importantes

Codespaces nao e um servidor dedicado permanente. Ele para apos inatividade e
consome a cota da sua conta GitHub. Para ficar 24/7 de verdade, use uma VPS ou
host de Minecraft.
