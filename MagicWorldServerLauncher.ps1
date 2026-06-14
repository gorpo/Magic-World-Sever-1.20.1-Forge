param(
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

$ServerRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$AssetsRoot = Join-Path $ServerRoot "launcher-assets"
$BackgroundPath = Join-Path $AssetsRoot "title_background_static.png"
$LogoPath = Join-Path $AssetsRoot "title_logo.png"
$IconPath = Join-Path $AssetsRoot "magicworld.ico"
$LatestLogPath = Join-Path $ServerRoot "logs\latest.log"
$LauncherLogPath = Join-Path $ServerRoot "logs\server-launcher.log"
$WorldPath = Join-Path $ServerRoot "world"
$BackupsPath = Join-Path $ServerRoot "backups"
$JvmArgsPath = Join-Path $ServerRoot "user_jvm_args.txt"
$ForgeArgsPath = Join-Path $ServerRoot "libraries\net\minecraftforge\forge\1.20.1-47.4.20\win_args.txt"
$ServerPort = 25565

$script:ServerProcess = $null
$script:LastLogLength = 0

function Ensure-Directory {
    param([string]$Path)
    if (!(Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
    }
}

function Read-SharedText {
    param([string]$Path)
    if (!(Test-Path -LiteralPath $Path)) {
        return ""
    }
    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8, $true)
        try {
            return $reader.ReadToEnd()
        } finally {
            $reader.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}

function Get-LogTail {
    param(
        [string]$Path,
        [int]$Lines = 180
    )
    $text = Read-SharedText -Path $Path
    if ([string]::IsNullOrEmpty($text)) {
        return ""
    }
    $split = $text -split "`r?`n"
    if ($split.Length -le $Lines) {
        return ($split -join [Environment]::NewLine)
    }
    return (($split[($split.Length - $Lines)..($split.Length - 1)]) -join [Environment]::NewLine)
}

function Get-ServerProcess {
    if ($script:ServerProcess -and !$script:ServerProcess.HasExited) {
        return $script:ServerProcess
    }

    $candidate = Get-CimInstance Win32_Process -Filter "name='java.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -like "*@libraries/net/minecraftforge/forge/1.20.1-47.4.20/win_args.txt*" -or
            $_.CommandLine -like "*@libraries\net\minecraftforge\forge\1.20.1-47.4.20\win_args.txt*"
        } |
        Select-Object -First 1

    if ($candidate) {
        try {
            return [System.Diagnostics.Process]::GetProcessById([int]$candidate.ProcessId)
        } catch {
            return $null
        }
    }
    return $null
}

function Test-PortOpen {
    try {
        $connection = Get-NetTCPConnection -LocalPort $ServerPort -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
        return $null -ne $connection
    } catch {
        return $false
    }
}

function Get-JavaCommand {
    $candidates = New-Object System.Collections.Generic.List[string]

    $launcherRuntime = Join-Path $env:LOCALAPPDATA "MagicWorldLauncher\runtime\java17"
    if (Test-Path -LiteralPath $launcherRuntime) {
        Get-ChildItem -Path $launcherRuntime -Recurse -Filter java.exe -ErrorAction SilentlyContinue |
            Sort-Object FullName |
            ForEach-Object { $candidates.Add($_.FullName) }
    }

    Get-ChildItem -Path "C:\Program Files\Eclipse Adoptium","C:\Program Files\Java" -Recurse -Filter java.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "(jdk|jre)-?17|java17" } |
        Sort-Object FullName |
        ForEach-Object { $candidates.Add($_.FullName) }

    if ($env:JAVA_HOME) {
        $javaHomeCandidate = Join-Path $env:JAVA_HOME "bin\java.exe"
        $candidates.Add($javaHomeCandidate)
    }

    $pathJava = Get-Command java.exe -ErrorAction SilentlyContinue
    if ($pathJava) {
        $candidates.Add($pathJava.Source)
    }

    Get-ChildItem -Path "C:\Program Files\Eclipse Adoptium","C:\Program Files\Java" -Recurse -Filter java.exe -ErrorAction SilentlyContinue |
        Sort-Object FullName |
        ForEach-Object { $candidates.Add($_.FullName) }

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    throw "Java nao encontrado. Instale Java 17 ou use o launcher oficial uma vez para baixar o runtime."
}

function Get-JavaVersionText {
    try {
        $java = Get-JavaCommand
        $output = & $java -version 2>&1
        return ("{0} ({1})" -f [string]($output | Select-Object -First 1), $java)
    } catch {
        return $_.Exception.Message
    }
}

function Get-RamSettings {
    $min = 2
    $max = 4
    if (Test-Path -LiteralPath $JvmArgsPath) {
        foreach ($line in Get-Content -LiteralPath $JvmArgsPath -ErrorAction SilentlyContinue) {
            if ($line -match '^-Xms(\d+)G') {
                $min = [int]$Matches[1]
            }
            if ($line -match '^-Xmx(\d+)G') {
                $max = [int]$Matches[1]
            }
        }
    }
    return [pscustomobject]@{ Min = $min; Max = $max }
}

function Save-RamSettings {
    param(
        [int]$Min,
        [int]$Max
    )
    $Min = [Math]::Max(1, [Math]::Min(16, $Min))
    $Max = [Math]::Max($Min, [Math]::Min(16, $Max))
    $content = @(
        "# Xmx and Xms set the maximum and minimum RAM usage."
        "# Adjust these values if the machine has little RAM available."
        "-Xms${Min}G"
        "-Xmx${Max}G"
    )
    Set-Content -LiteralPath $JvmArgsPath -Encoding ASCII -Value $content
}

function Append-LauncherLog {
    param([string]$Text)
    Ensure-Directory -Path (Split-Path -Parent $LauncherLogPath)
    Add-Content -LiteralPath $LauncherLogPath -Encoding UTF8 -Value ("[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Text)
}

function Start-Server {
    if (Get-ServerProcess) {
        Set-Status "Servidor ja esta rodando."
        return
    }
    if (!(Test-Path -LiteralPath $ForgeArgsPath)) {
        throw "Forge nao instalado. Rode setup-forge.bat antes."
    }

    Save-RamSettings -Min ([int]$script:MinRamBox.Text) -Max ([int]$script:MaxRamBox.Text)
    Ensure-Directory -Path (Join-Path $ServerRoot "logs")
    Append-LauncherLog "Start requested."

    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = Get-JavaCommand
    $start.Arguments = "@user_jvm_args.txt @libraries/net/minecraftforge/forge/1.20.1-47.4.20/win_args.txt nogui"
    $start.WorkingDirectory = $ServerRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $start
    $process.EnableRaisingEvents = $true
    $process.add_OutputDataReceived({
        param($sender, $eventArgs)
        if (![string]::IsNullOrWhiteSpace($eventArgs.Data)) {
            Append-LauncherLog $eventArgs.Data
        }
    })
    $process.add_ErrorDataReceived({
        param($sender, $eventArgs)
        if (![string]::IsNullOrWhiteSpace($eventArgs.Data)) {
            Append-LauncherLog $eventArgs.Data
        }
    })
    $process.add_Exited({
        Append-LauncherLog ("Server process exited with code {0}." -f $process.ExitCode)
    })

    if (!$process.Start()) {
        throw "Nao foi possivel iniciar java."
    }
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()
    $script:ServerProcess = $process
    Set-Status "Servidor iniciando..."
}

function Stop-Server {
    $process = Get-ServerProcess
    if (!$process) {
        Set-Status "Servidor nao esta rodando."
        return
    }

    Append-LauncherLog "Stop requested."
    if ($script:ServerProcess -and !$script:ServerProcess.HasExited) {
        try {
            $script:ServerProcess.StandardInput.WriteLine("stop")
            $script:ServerProcess.StandardInput.Flush()
            Set-Status "Enviando stop..."
            if (!$script:ServerProcess.WaitForExit(30000)) {
                $script:ServerProcess.Kill()
            }
            return
        } catch {
        }
    }

    $result = [System.Windows.MessageBox]::Show(
        "Este servidor foi iniciado fora do launcher. Encerrar o processo Java agora?",
        "Magic World Server",
        "YesNo",
        "Warning"
    )
    if ($result -eq "Yes") {
        Stop-Process -Id $process.Id -Force
        Set-Status "Servidor encerrado."
    }
}

function Restart-Server {
    Stop-Server
    Start-Sleep -Seconds 2
    Start-Server
}

function Backup-World {
    if (!(Test-Path -LiteralPath $WorldPath)) {
        Set-Status "Mundo ainda nao existe."
        return
    }
    Ensure-Directory -Path $BackupsPath
    $name = "world-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".zip"
    $destination = Join-Path $BackupsPath $name
    Compress-Archive -LiteralPath $WorldPath -DestinationPath $destination -Force
    Set-Status "Backup criado: $name"
}

function Open-Folder {
    param([string]$Path)
    Ensure-Directory -Path $Path
    Start-Process explorer.exe -ArgumentList "`"$Path`""
}

function Set-Status {
    param([string]$Text)
    if ($script:StatusText) {
        $script:StatusText.Text = $Text
    }
}

if ($SelfTest) {
    [pscustomobject]@{
        serverRoot = $ServerRoot
        forgeArgsExists = Test-Path -LiteralPath $ForgeArgsPath
        java = Get-JavaVersionText
        portOpen = Test-PortOpen
        processId = $(if (Get-ServerProcess) { (Get-ServerProcess).Id } else { $null })
        latestLogExists = Test-Path -LiteralPath $LatestLogPath
    } | ConvertTo-Json -Depth 3
    exit 0
}

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms

$window = New-Object System.Windows.Window
$window.Title = "Magic World Server Launcher"
$window.Width = 1080
$window.Height = 700
$window.MinWidth = 920
$window.MinHeight = 620
$window.WindowStartupLocation = "CenterScreen"
$window.ResizeMode = "CanResize"
if (Test-Path -LiteralPath $IconPath) {
    $window.Icon = [System.Windows.Media.Imaging.BitmapImage]::new([Uri]$IconPath)
}

$grid = New-Object System.Windows.Controls.Grid
$window.Content = $grid

if (Test-Path -LiteralPath $BackgroundPath) {
    $image = New-Object System.Windows.Media.Imaging.BitmapImage
    $image.BeginInit()
    $image.UriSource = New-Object System.Uri($BackgroundPath)
    $image.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
    $image.EndInit()
    $brush = New-Object System.Windows.Media.ImageBrush
    $brush.ImageSource = $image
    $brush.Stretch = "UniformToFill"
    $grid.Background = $brush
} else {
    $grid.Background = "#101820"
}

$overlay = New-Object System.Windows.Controls.Border
$overlay.Background = "#B0000000"
$grid.Children.Add($overlay) | Out-Null

$root = New-Object System.Windows.Controls.DockPanel
$root.Margin = "28"
$grid.Children.Add($root) | Out-Null

function New-MagicButton {
    param(
        [string]$Text,
        [int]$Width = 130,
        [int]$Height = 38,
        [switch]$Primary
    )
    $button = New-Object System.Windows.Controls.Button
    $button.Content = $Text
    $button.Width = $Width
    $button.Height = $Height
    $button.Margin = "5"
    $button.FontFamily = "Segoe UI"
    $button.FontWeight = "SemiBold"
    $button.FontSize = $(if ($Primary) { 15 } else { 13 })
    $button.Foreground = "White"
    $button.Background = $(if ($Primary) { "#D4A520" } else { "#D0101820" })
    $button.BorderBrush = $(if ($Primary) { "#FFF1C85B" } else { "#B0DAA520" })
    $button.BorderThickness = "1.5"
    return $button
}

$header = New-Object System.Windows.Controls.DockPanel
$header.Height = 105
[System.Windows.Controls.DockPanel]::SetDock($header, "Top")
$root.Children.Add($header) | Out-Null

if (Test-Path -LiteralPath $LogoPath) {
    $logoImage = New-Object System.Windows.Media.Imaging.BitmapImage
    $logoImage.BeginInit()
    $logoImage.UriSource = New-Object System.Uri($LogoPath)
    $logoImage.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
    $logoImage.EndInit()

    $logo = New-Object System.Windows.Controls.Image
    $logo.Source = $logoImage
    $logo.Width = 270
    $logo.Height = 90
    $logo.Stretch = "Uniform"
    $logo.HorizontalAlignment = "Left"
    [System.Windows.Controls.DockPanel]::SetDock($logo, "Left")
    $header.Children.Add($logo) | Out-Null
}

$headerText = New-Object System.Windows.Controls.StackPanel
$headerText.VerticalAlignment = "Center"
$headerText.Margin = "14,0,0,0"
$header.Children.Add($headerText) | Out-Null

$title = New-Object System.Windows.Controls.TextBlock
$title.Text = "Server Control"
$title.FontSize = 28
$title.FontWeight = "Bold"
$title.Foreground = "White"
$headerText.Children.Add($title) | Out-Null

$script:StatusText = New-Object System.Windows.Controls.TextBlock
$script:StatusText.Text = "Pronto."
$script:StatusText.FontSize = 14
$script:StatusText.Foreground = "#FFD8E0F0"
$script:StatusText.Margin = "0,6,0,0"
$headerText.Children.Add($script:StatusText) | Out-Null

$content = New-Object System.Windows.Controls.Grid
$content.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition -Property @{ Width = "310" }))
$content.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition -Property @{ Width = "*" }))
$root.Children.Add($content) | Out-Null

$leftPanel = New-Object System.Windows.Controls.StackPanel
$leftPanel.Margin = "0,0,18,0"
[System.Windows.Controls.Grid]::SetColumn($leftPanel, 0)
$content.Children.Add($leftPanel) | Out-Null

$statusBox = New-Object System.Windows.Controls.Border
$statusBox.Background = "#CC101820"
$statusBox.BorderBrush = "#80DAA520"
$statusBox.BorderThickness = "1"
$statusBox.Padding = "14"
$statusBox.Margin = "0,0,0,12"
$leftPanel.Children.Add($statusBox) | Out-Null

$statusStack = New-Object System.Windows.Controls.StackPanel
$statusBox.Child = $statusStack

$script:ServerStateText = New-Object System.Windows.Controls.TextBlock
$script:ServerStateText.FontSize = 18
$script:ServerStateText.FontWeight = "Bold"
$script:ServerStateText.Foreground = "#FFDAA520"
$script:ServerStateText.Text = "Status: verificando"
$statusStack.Children.Add($script:ServerStateText) | Out-Null

$script:PortText = New-Object System.Windows.Controls.TextBlock
$script:PortText.Foreground = "White"
$script:PortText.Margin = "0,8,0,0"
$statusStack.Children.Add($script:PortText) | Out-Null

$javaText = New-Object System.Windows.Controls.TextBlock
$javaText.Text = Get-JavaVersionText
$javaText.Foreground = "#FFD8E0F0"
$javaText.TextWrapping = "Wrap"
$javaText.Margin = "0,8,0,0"
$statusStack.Children.Add($javaText) | Out-Null

$ram = Get-RamSettings
$ramBox = New-Object System.Windows.Controls.Border
$ramBox.Background = "#AA101820"
$ramBox.BorderBrush = "#50DAA520"
$ramBox.BorderThickness = "1"
$ramBox.Padding = "12"
$ramBox.Margin = "0,0,0,12"
$leftPanel.Children.Add($ramBox) | Out-Null

$ramStack = New-Object System.Windows.Controls.StackPanel
$ramBox.Child = $ramStack

$ramTitle = New-Object System.Windows.Controls.TextBlock
$ramTitle.Text = "RAM do servidor"
$ramTitle.Foreground = "#FFDAA520"
$ramTitle.FontWeight = "Bold"
$ramStack.Children.Add($ramTitle) | Out-Null

$ramRow = New-Object System.Windows.Controls.StackPanel
$ramRow.Orientation = "Horizontal"
$ramRow.Margin = "0,8,0,0"
$ramStack.Children.Add($ramRow) | Out-Null

$ramRow.Children.Add((New-Object System.Windows.Controls.TextBlock -Property @{ Text = "Min"; Foreground = "White"; Width = 34; VerticalAlignment = "Center" })) | Out-Null
$script:MinRamBox = New-Object System.Windows.Controls.TextBox
$script:MinRamBox.Text = [string]$ram.Min
$script:MinRamBox.Width = 42
$script:MinRamBox.Margin = "0,0,10,0"
$ramRow.Children.Add($script:MinRamBox) | Out-Null
$ramRow.Children.Add((New-Object System.Windows.Controls.TextBlock -Property @{ Text = "Max"; Foreground = "White"; Width = 34; VerticalAlignment = "Center" })) | Out-Null
$script:MaxRamBox = New-Object System.Windows.Controls.TextBox
$script:MaxRamBox.Text = [string]$ram.Max
$script:MaxRamBox.Width = 42
$ramRow.Children.Add($script:MaxRamBox) | Out-Null
$ramRow.Children.Add((New-Object System.Windows.Controls.TextBlock -Property @{ Text = "GB"; Foreground = "White"; Margin = "8,0,0,0"; VerticalAlignment = "Center" })) | Out-Null

$buttonWrap = New-Object System.Windows.Controls.WrapPanel
$buttonWrap.Margin = "0,0,0,12"
$leftPanel.Children.Add($buttonWrap) | Out-Null

$startButton = New-MagicButton "Iniciar" 140 42 -Primary
$stopButton = New-MagicButton "Parar" 140 42
$restartButton = New-MagicButton "Reiniciar" 140 38
$backupButton = New-MagicButton "Backup mundo" 140 38
$modsButton = New-MagicButton "Mods" 140 38
$logsButton = New-MagicButton "Logs" 140 38
$folderButton = New-MagicButton "Pasta" 140 38
$eulaButton = New-MagicButton "EULA" 140 38

foreach ($button in @($startButton, $stopButton, $restartButton, $backupButton, $modsButton, $logsButton, $folderButton, $eulaButton)) {
    $buttonWrap.Children.Add($button) | Out-Null
}

$hint = New-Object System.Windows.Controls.TextBlock
$hint.Text = "Use localhost:25565 no Minecraft. Mods client-only, como shader, ficam so no cliente."
$hint.Foreground = "#FFD8E0F0"
$hint.TextWrapping = "Wrap"
$hint.Margin = "4,4,4,0"
$leftPanel.Children.Add($hint) | Out-Null

$rightGrid = New-Object System.Windows.Controls.Grid
$rightGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = "38" }))
$rightGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = "*" }))
[System.Windows.Controls.Grid]::SetColumn($rightGrid, 1)
$content.Children.Add($rightGrid) | Out-Null

$logHeader = New-Object System.Windows.Controls.DockPanel
[System.Windows.Controls.Grid]::SetRow($logHeader, 0)
$rightGrid.Children.Add($logHeader) | Out-Null

$logTitle = New-Object System.Windows.Controls.TextBlock
$logTitle.Text = "Logs ao vivo"
$logTitle.Foreground = "White"
$logTitle.FontSize = 18
$logTitle.FontWeight = "Bold"
$logTitle.VerticalAlignment = "Center"
$logHeader.Children.Add($logTitle) | Out-Null

$logBox = New-Object System.Windows.Controls.TextBox
$logBox.Background = "#E0101820"
$logBox.Foreground = "#FFE8EEF8"
$logBox.BorderBrush = "#80DAA520"
$logBox.FontFamily = "Consolas"
$logBox.FontSize = 12
$logBox.TextWrapping = "NoWrap"
$logBox.AcceptsReturn = $true
$logBox.AcceptsTab = $true
$logBox.VerticalScrollBarVisibility = "Auto"
$logBox.HorizontalScrollBarVisibility = "Auto"
$logBox.IsReadOnly = $true
[System.Windows.Controls.Grid]::SetRow($logBox, 1)
$rightGrid.Children.Add($logBox) | Out-Null

function Refresh-Ui {
    $process = Get-ServerProcess
    $portOpen = Test-PortOpen
    if ($process -and !$process.HasExited) {
        $script:ServerStateText.Text = "Status: online"
        $script:ServerStateText.Foreground = "#FF7CFF9B"
        $stopButton.IsEnabled = $true
        $restartButton.IsEnabled = $true
    } else {
        $script:ServerStateText.Text = "Status: parado"
        $script:ServerStateText.Foreground = "#FFFF7676"
        $stopButton.IsEnabled = $false
        $restartButton.IsEnabled = $false
    }
    $script:PortText.Text = "Porta ${ServerPort}: " + $(if ($portOpen) { "aberta" } else { "fechada" })

    $tail = Get-LogTail -Path $LatestLogPath -Lines 220
    if ([string]::IsNullOrWhiteSpace($tail)) {
        $tail = Get-LogTail -Path $LauncherLogPath -Lines 220
    }
    if ($logBox.Text -ne $tail) {
        $logBox.Text = $tail
        $logBox.ScrollToEnd()
    }
}

$startButton.Add_Click({
    try {
        Start-Server
    } catch {
        Set-Status "Erro: $($_.Exception.Message)"
        [System.Windows.MessageBox]::Show($_.Exception.Message, "Magic World Server", "OK", "Error") | Out-Null
    }
    Refresh-Ui
})

$stopButton.Add_Click({
    try {
        Stop-Server
    } catch {
        Set-Status "Erro: $($_.Exception.Message)"
        [System.Windows.MessageBox]::Show($_.Exception.Message, "Magic World Server", "OK", "Error") | Out-Null
    }
    Refresh-Ui
})

$restartButton.Add_Click({
    try {
        Restart-Server
    } catch {
        Set-Status "Erro: $($_.Exception.Message)"
        [System.Windows.MessageBox]::Show($_.Exception.Message, "Magic World Server", "OK", "Error") | Out-Null
    }
    Refresh-Ui
})

$backupButton.Add_Click({
    try {
        Backup-World
    } catch {
        Set-Status "Erro: $($_.Exception.Message)"
        [System.Windows.MessageBox]::Show($_.Exception.Message, "Magic World Server", "OK", "Error") | Out-Null
    }
})

$modsButton.Add_Click({ Open-Folder -Path (Join-Path $ServerRoot "mods") })
$logsButton.Add_Click({ Open-Folder -Path (Join-Path $ServerRoot "logs") })
$folderButton.Add_Click({ Open-Folder -Path $ServerRoot })
$eulaButton.Add_Click({ Start-Process notepad.exe -ArgumentList "`"$(Join-Path $ServerRoot 'eula.txt')`"" })

$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromSeconds(2)
$timer.Add_Tick({ Refresh-Ui })
$timer.Start()

$window.Add_Closing({
    $timer.Stop()
})

Refresh-Ui
$window.ShowDialog() | Out-Null
