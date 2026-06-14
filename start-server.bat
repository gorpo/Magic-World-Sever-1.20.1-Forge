@echo off
setlocal

findstr /R /C:"^eula=false" eula.txt >nul 2>&1
if %errorlevel%==0 (
    echo Para iniciar o servidor voce precisa ler e aceitar a EULA da Mojang/Microsoft:
    echo https://aka.ms/MinecraftEULA
    echo.
    echo Se aceitar, troque eula=false para eula=true no arquivo que vai abrir.
    echo Depois salve o arquivo e rode este .bat novamente.
    echo.
    notepad eula.txt
    pause
    exit /b 1
)

echo Iniciando Magic World Server 1.20.1 Forge...
echo.
java @user_jvm_args.txt @libraries/net/minecraftforge/forge/1.20.1-47.4.20/win_args.txt nogui

echo.
echo Servidor encerrado.
pause
