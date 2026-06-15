# Release pronta - Magic World Server Launcher

Tag sugerida:

```text
server-launcher-v1.0.0
```

Arquivo ZIP local:

```text
release\Magic-World-Server-Launcher-Forge-1.20.1-v1.0.0.zip
```

Observacao: este arquivo `RELEASE_READY.md` fica fora do ZIP para o checksum do asset ficar estavel. Use este texto como descricao manual da release.

## Titulo sugerido

```text
Magic World Server Launcher - Forge 1.20.1
```

## Descricao para colar no GitHub Release

Pacote do servidor Magic World para Minecraft Forge 1.20.1 com launcher grafico Windows.

Inclui:

- Launcher `MagicWorldServerLauncher.exe`
- Start, stop e restart do servidor Minecraft
- Logs ao vivo do servidor
- Status geral: online/offline, porta, uptime, RAM e jogadores
- Lista de jogadores conectados
- Campo para enviar comandos com Enter ou botao de seta
- Comandos rapidos para administracao
- Integracao com tunel externo Playit
- Logs e status do Playit separados dos logs do servidor
- Backup do mundo
- Mods ativos do servidor
- Configuracoes do servidor
- Banner e documentacao

Nao inclui:

- Mundo salvo local
- Logs antigos
- Crash reports
- Bibliotecas baixadas pelo Forge em runtime
- Cache do JourneyMap
- Agente Playit baixado localmente
- Mods client-only desativados

## Como usar

1. Baixe o ZIP da release.
2. Extraia para uma pasta, por exemplo:

```text
C:\Users\SEU_USUARIO\Desktop\Magic-World-Sever-1.20.1-Forge
```

3. Abra `MagicWorldServerLauncher.exe`.
4. Clique em `Iniciar servidor`.
5. Para acesso fora da rede, use a area `Tunel externo`.
6. Copie o endereco do Playit e passe para os jogadores.

## Cliente necessario

Os jogadores precisam usar o pacote cliente Magic World compativel.

Repositorio do cliente, mod, resource pack, shader, installer e launcher:

```text
https://github.com/gorpo/Magic-World_ultimate-Forge1.20.1
```

Esse pacote complementar contem:

- `Magic_World_Mod_1.20.1-1.0.0.1.jar`
- `MagicWorldResource_1.20.1-256x.zip`
- `MagicWorldResource_1.20.1-addon.zip`
- `MagicWorldResource_1.20.1-bonus.zip`
- `MagicWorldResource_1.20.1-models.zip`
- `MagicWorldScreenOverlays.zip`
- `MagicWorld_Shaders_Extreme_v1.0_.zip`
- Launcher e installer FULL do cliente

## Observacoes tecnicas

- Porta padrao do servidor: `25565`
- Forge: `1.20.1`
- Java recomendado: `17`
- O Playit e baixado/instalado pelo launcher quando usado.
- Se o botao `Copiar endereco` nao retornar um dominio `joinmc.link`, confirme o claim/login no Playit.
- Launcher recompilado com menos flicker: buffer duplo, refresh menos agressivo do Playit e lista de jogadores preservando selecao.
- Endereco publico do Playit aparece em campo copiavel no topo da area de logs, com botao `Copiar`.
- Detector do Playit tambem decodifica enderecos embutidos nos logs tecnicos, como dominios `joinmc.link` ou `ply.gg`.
- Layout principal usa containers opacos e pintura otimizada para redimensionar sem rastros ou duplicacao visual dos paineis.
- `TP destino` teleporta o jogador selecionado para outro jogador ou coordenadas `X Y Z` informadas no popup.
- Botoes `Desativar mods` e `Ativar mods` movem/restauram os `.jar` entre `mods/` e `mods-disabled/launcher-toggle/` para testar o servidor sem mods.

## Checksum

O SHA256 deve ser preenchido depois que o ZIP for gerado.

```text
SHA256: B41FCDE645E49EA630C0F48E5B08C7416DE91F5228EF73C5AD3ABF449F31C0CF
Tamanho: 155.51 MB
```
