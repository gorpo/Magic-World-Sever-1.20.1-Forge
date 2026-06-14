$forgeVersion = "1.20.1-47.4.20"
$installer = "forge-$forgeVersion-installer.jar"
$url = "https://maven.minecraftforge.net/net/minecraftforge/forge/$forgeVersion/$installer"

if (Test-Path -LiteralPath "libraries/net/minecraftforge/forge/1.20.1-47.4.20/win_args.txt") {
    Write-Host "Forge $forgeVersion ja esta instalado."
    exit 0
}

Write-Host "Baixando Forge $forgeVersion..."
Invoke-WebRequest -Uri $url -OutFile $installer

Write-Host "Instalando servidor Forge..."
& java -jar $installer --installServer

Remove-Item -LiteralPath $installer -Force
Remove-Item -LiteralPath "$installer.log" -Force -ErrorAction SilentlyContinue

Write-Host "Servidor Forge instalado."
