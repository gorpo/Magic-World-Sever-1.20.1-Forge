# Magic World Server Launcher - Forge 1.20.1

![Magic World Server Launcher](docs/banner.png)

Servidor Minecraft Forge 1.20.1 com launcher grafico para Windows, painel de logs, controle do servidor, tunel externo Playit, jogadores online, comandos rapidos e utilitarios para administracao do mundo Magic World.

Repositorio do servidor:

```text
https://github.com/gorpo/Magic-World-Sever-1.20.1-Forge
```

Repositorio do pacote completo do cliente, mod, resource pack, shader, installer e launcher do jogador:

```text
https://github.com/gorpo/Magic-World_ultimate-Forge1.20.1
```

## O que este repositorio contem

- Servidor Forge 1.20.1 configurado para Magic World.
- Mods de servidor em `mods/`.
- Mods client-only ou problematicos guardados fora do carregamento em `mods-disabled/`.
- Launcher grafico Windows: `MagicWorldServerLauncher.exe`.
- Codigo fonte do launcher: `MagicWorldServerLauncherApp.cs`.
- Script antigo PowerShell mantido como referencia: `MagicWorldServerLauncher.ps1`.
- Configuracoes principais do servidor em `server.properties`, `config/` e `user_jvm_args.txt`.
- Ferramenta de catalogo de itens para comandos rapidos: `BuildItemCatalog.ps1`.
- Assets do launcher em `launcher-assets/`.

## Pacote complementar do cliente

Para entrar no servidor, o jogador precisa usar o pacote cliente Magic World compativel.

Use o repositorio:

```text
https://github.com/gorpo/Magic-World_ultimate-Forge1.20.1
```

Esse repo do cliente contem o pacote que completa este servidor:

- `Magic_World_Mod_1.20.1-1.0.0.1.jar`
- `MagicWorldLight-Forge1.20.1-1.0.0.jar`
- Resource packs principais:
  - `MagicWorldResource_1.20.1-256x.zip`
  - `MagicWorldResource_1.20.1-addon.zip`
  - `MagicWorldResource_1.20.1-bonus.zip`
  - `MagicWorldResource_1.20.1-models.zip`
- Overlay visual:
  - `Screen Overlays`
  - `MagicWorldScreenOverlays.zip`
- Shader:
  - `MagicWorld_Shaders_Extreme_v1.0_.zip`
- Launcher do jogador
- Installer FULL para instalar o cliente Magic World isolado em `%LOCALAPPDATA%\MagicWorldLauncher`

Este repositorio atual e focado no servidor. O outro repositorio e o pacote completo para o cliente/jogador.

Importante: resource packs, shaders e Oculus sao parte do cliente. Eles nao devem ser carregados no servidor Forge dedicado.

## Requisitos

- Windows 10 ou superior.
- Java 17. O servidor ja foi testado com JDK/JRE 17.
- Minecraft Forge 1.20.1.
- Porta padrao `25565`.
- Pelo menos 4 GB de RAM livre para testes leves.
- Para mundo com muitos mods, recomenda-se 8 GB ou mais livres no PC.

## Como usar no Windows

1. Baixe ou clone este repositorio.
2. Abra a pasta do servidor.
3. Execute:

```text
MagicWorldServerLauncher.exe
```

4. No launcher, use a area **Servidor Minecraft**:

- `Iniciar servidor`
- `Parar servidor`
- `Reiniciar servidor`

5. Acompanhe o lado direito da tela:

- Logs do servidor
- Status Playit
- Logs Playit
- Console para enviar comandos manualmente

## Primeira inicializacao

Antes de rodar publicamente, confirme a EULA da Mojang/Microsoft.

Arquivo:

```text
eula.txt
```

Se voce aceitar os termos oficiais, deixe:

```text
eula=true
```

Termos oficiais:

```text
https://aka.ms/MinecraftEULA
```

## Tela principal do launcher

O launcher esta dividido em blocos.

### Painel do servidor

Mostra:

- Status online/offline
- Porta `25565`
- Uptime
- RAM usada pelo processo Java
- Quantidade de jogadores
- Java detectado

### Jogadores online

Mostra a lista de jogadores conectados.

Botoes disponiveis para o jogador selecionado:

- `OP`
- `Kick`
- `Ban`
- `Criativo`
- `Survival`
- `Teleportar`

`Kick` e `Ban` pedem confirmacao antes de enviar o comando.

### Servidor Minecraft

Botoes principais:

- `Iniciar servidor`
- `Parar servidor`
- `Reiniciar servidor`

### Tunel externo

Botoes:

- `Iniciar tunel Playit`
- `Parar tunel Playit`
- `Copiar endereco publico`
- `Configurar tunel`

O Playit e usado para permitir conexoes de fora da sua rede sem abrir porta manualmente no roteador.

### Configuracoes

Inclui:

- RAM minima e maxima do servidor
- `Editar server.properties`
- `Abrir config do servidor`
- `Config do tunel Playit`

### Ferramentas

Inclui:

- `Comandos rapidos`
- `Backup mundo`
- `Abrir pasta de mods`
- `Abrir pasta de logs`
- `Pasta servidor`
- `Editar EULA`

## Playit / acesso fora da rede

Para liberar acesso externo:

1. Abra o launcher.
2. Clique em `Configurar tunel`.
3. Clique em `Baixar agente`, se ainda nao existir.
4. Clique em `Instalar agente`.
5. Clique em `Login Playit`.
6. Clique em `Iniciar tunel Playit`.
7. No painel do Playit, crie ou confirme um tunel Minecraft Java apontando para:

```text
127.0.0.1:25565
```

8. Clique em `Copiar endereco publico`.
9. Passe esse endereco para os jogadores.

Observacao: `localhost` e `127.0.0.1` so funcionam no proprio computador do servidor. Para amigos fora da rede, use o endereco do Playit.

## Comandos manuais

Na parte inferior do launcher existe uma caixa de comando.

Exemplos:

```text
list
say Servidor reiniciando em 1 minuto
time set day
weather clear
save-all
op NomeDoJogador
kick NomeDoJogador
gamemode creative NomeDoJogador
tp NomeDoJogador 0 80 0
```

Nao use `/` no inicio. O console do servidor usa o comando direto.

## Comandos rapidos e itens

O botao `Comandos rapidos` abre um painel com:

- Comandos comuns
- Dar itens
- Busca de itens
- Itens do Minecraft e mods catalogados

O catalogo e gerado por:

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildItemCatalog.ps1
```

Arquivos gerados:

```text
launcher-assets\item-catalog.tsv
launcher-assets\item-icons\
```

Se adicionar ou remover mods, rode o script novamente para atualizar a lista.

## Mods

Mods carregados pelo servidor ficam em:

```text
mods\
```

Mods desativados ficam em:

```text
mods-disabled\
```

Regra importante:

- Mods de servidor e mods compartilhados ficam em `mods/`.
- Mods client-only ficam fora do servidor, em `mods-disabled/`.
- O cliente precisa ter os mesmos mods obrigatorios do servidor para conectar.

Exemplo de mod client-only:

```text
oculus-mc1.20.1-1.8.0.jar
```

Oculus/shader e coisa do cliente, nao do servidor.

## Configuracoes principais

### server.properties

Arquivo:

```text
server.properties
```

Campos comuns:

```text
server-port=25565
online-mode=false
enforce-secure-profile=false
max-players=20
view-distance=6
simulation-distance=4
max-tick-time=180000
sync-chunk-writes=false
```

Se for servidor publico com contas oficiais e autenticacao normal, avalie usar `online-mode=true`. Para rede local, testes e launchers offline, `online-mode=false` costuma ser usado.

### RAM

Arquivo:

```text
user_jvm_args.txt
```

Exemplo:

```text
-Xms2G
-Xmx4G
```

Voce tambem pode alterar no launcher pela area **Configuracoes**.

## Logs e crash reports

Logs:

```text
logs\
```

Crash reports:

```text
crash-reports\
```

Quando der erro:

1. Abra `logs/latest.log`.
2. Veja se existe arquivo novo em `crash-reports/`.
3. Procure por:

```text
Caused by:
Suspected Mod:
Exception
ERROR
```

## Backups

O botao `Backup mundo` compacta a pasta `world/` em:

```text
backups\
```

Antes de trocar mods, atualizar jar ou mexer em configs importantes, faca backup.

## Estrutura das pastas

```text
Magic-World-Sever-1.20.1-Forge\
  MagicWorldServerLauncher.exe        launcher grafico pronto
  MagicWorldServerLauncherApp.cs      codigo fonte do launcher
  BuildItemCatalog.ps1                gera catalogo de itens
  README.md                           documentacao principal
  RELEASE_READY.md                    notas para release manual
  server.properties                   config principal Minecraft
  user_jvm_args.txt                   RAM/JVM
  eula.txt                            aceite EULA
  mods\                               mods ativos do servidor
  mods-disabled\                      mods desativados/client-only
  config\                             configs dos mods
  launcher-assets\                    icone, fundo, logo, catalogo e imagens
  logs\                               logs gerados
  crash-reports\                      crash reports
  world\                              mundo local, ignorado no Git
  backups\                            backups locais, ignorado no Git
  release\                            zips locais de release, ignorado no Git
```

## Como alterar o codigo do launcher

Arquivo principal:

```text
MagicWorldServerLauncherApp.cs
```

Pontos importantes dentro dele:

- `BuildWindow()` monta a interface principal.
- `RefreshUi()` atualiza status, porta, uptime, RAM e jogadores.
- `StartServer()` inicia o Forge.
- `StopServer()` envia `stop` para o console.
- `StartPlayitTunnel()` inicia o servico Playit.
- `StopPlayitTunnel()` para o Playit.
- `OpenQuickCommands()` abre a tela de comandos rapidos.
- `GetOnlinePlayersFromLogs()` monta a lista de jogadores.
- `SendPlayerCommand()` envia comandos para jogador selecionado.
- `TeleportSelectedPlayer()` abre popup de teleport.

Depois de alterar o `.cs`, recompile:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /out:MagicWorldServerLauncher.exe /win32icon:launcher-assets\magicworld.ico MagicWorldServerLauncherApp.cs
```

Se o launcher estiver aberto, feche antes de recompilar.

## Como alterar layout, logo e fundo

Assets:

```text
launcher-assets\magicworld.ico
launcher-assets\title_logo.png
launcher-assets\title_background_static.png
docs\banner.png
```

Para mudar medidas, textos ou botoes, edite:

```text
MagicWorldServerLauncherApp.cs
```

Procure por:

```text
BuildWindow
ActionButton
SectionTitle
BoxPanel
SmallActionButton
```

## Como gerar ZIP de release

Este repo pode gerar um ZIP local pronto para anexar manualmente em GitHub Releases.

Pasta local de saida:

```text
release\
```

O ZIP deve conter launcher, mods, configs e docs, mas nao deve conter:

- `world/`
- `logs/`
- `crash-reports/`
- `backups/`
- `libraries/`
- arquivos temporarios

## Publicar release manual

1. Gere ou use o ZIP em `release/`.
2. Abra o GitHub:

```text
https://github.com/gorpo/Magic-World-Sever-1.20.1-Forge/releases
```

3. Crie uma nova release.
4. Tag sugerida:

```text
server-launcher-v1.0.0
```

5. Anexe o ZIP gerado.
6. Copie o texto de `RELEASE_READY.md` na descricao.

## Rodar online com Docker/VPS

Para VPS Linux com Docker:

```bash
git clone https://github.com/gorpo/Magic-World-Sever-1.20.1-Forge.git
cd Magic-World-Sever-1.20.1-Forge
docker compose up -d --build
```

Antes, aceite a EULA em `docker-compose.yml`, se concordar com os termos:

```yaml
EULA: "true"
```

Abra a porta TCP `25565` no firewall da VPS.

## GitHub Codespaces

GitHub Codespaces pode rodar testes, scripts e ambiente online temporario.

Arquivos relacionados:

```text
CODESPACES.md
VIDEO-COMMANDS.md
scripts\
```

Codespaces nao substitui uma VPS 24/7. Ele e bom para teste e demonstracao.

## Problemas comuns

### O jogo fecha ao abrir inventario

Verifique se cliente e servidor possuem os mesmos mods obrigatorios.

O pacote atual inclui:

```text
MagicWorldLight-Forge1.20.1-1.0.0.jar
Magic_World_Mod_1.20.1-1.0.0.1.jar
```

Se o cliente tiver um mod que adiciona item e o servidor nao tiver, a aba criativa pode quebrar.

### Porta fechada

Use Playit ou abra a porta `25565` no roteador/firewall.

### Nao consigo copiar endereco Playit

Abra `Configurar tunel`, faca login/claim no Playit e confirme que existe tunel Minecraft Java para:

```text
127.0.0.1:25565
```

### Servidor fica rodando sem eu ver

O launcher foi feito para parar o servidor quando a janela principal fecha. Use sempre o botao `Parar servidor` ou feche o launcher principal.

## Licencas e avisos

- Minecraft e marca da Mojang/Microsoft.
- Forge, Playit e mods externos pertencem aos respectivos autores.
- Este projeto e um pacote de configuracao/launcher para facilitar uso do servidor Magic World.
- Confira sempre as licencas dos mods antes de redistribuir publicamente.
