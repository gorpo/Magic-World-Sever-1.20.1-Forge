$javaVersionOutput = & java -version 2>&1
$versionLine = ($javaVersionOutput | Select-Object -First 1).ToString()
if ($versionLine -match '"(?<major>\d+)') {
    $major = [int]$Matches.major
    if ($major -ne 17) {
        Write-Host "Aviso: Minecraft Forge 1.20.1 recomenda Java 17. Detectado: $versionLine" -ForegroundColor Yellow
        Write-Host "Se o servidor fechar com erro de Java, instale Temurin/OpenJDK 17 e tente de novo." -ForegroundColor Yellow
    }
}

Write-Host "Iniciando Magic World Server 1.20.1 Forge..."
& java "@user_jvm_args.txt" "@libraries/net/minecraftforge/forge/1.20.1-47.4.20/win_args.txt" nogui
