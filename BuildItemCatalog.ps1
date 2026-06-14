param(
    [string]$ServerRoot = (Split-Path -Parent $MyInvocation.MyCommand.Path)
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$assetsRoot = Join-Path $ServerRoot "launcher-assets"
$iconsRoot = Join-Path $assetsRoot "item-icons"
$catalogPath = Join-Path $assetsRoot "item-catalog.tsv"
$modsRoot = Join-Path $ServerRoot "mods"
$vanillaJar = Join-Path $env:LOCALAPPDATA "MagicWorldLauncher\.minecraft\versions\1.20.1\1.20.1.jar"
$magicWorldAssets = "C:\Users\guilh\Desktop\MinecraftProjects\Magic-World_ultimate-Forge1.20.1\src\main\resources\assets"

New-Item -ItemType Directory -Force -Path $iconsRoot | Out-Null

$items = @{}
$langs = @{}

function Convert-Name {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }
    return (($Value -replace "_", " " -replace "/", " ") -split " " | ForEach-Object {
        if ($_.Length -gt 1) { $_.Substring(0,1).ToUpperInvariant() + $_.Substring(1) } else { $_.ToUpperInvariant() }
    }) -join " "
}

function Set-LangValue {
    param([string]$Key, [string]$Value)
    if (![string]::IsNullOrWhiteSpace($Key) -and !$langs.ContainsKey($Key)) {
        $langs[$Key] = $Value
    }
}

function Add-Item {
    param(
        [string]$Namespace,
        [string]$Path,
        [string]$IconPath,
        [string]$Source
    )

    if (!(Test-ProbablyRealItem -Path $Path)) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($Namespace) -or [string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    $id = "$Namespace`:$Path"
    if (!$items.ContainsKey($id)) {
        $items[$id] = [ordered]@{
            id = $id
            namespace = $Namespace
            path = $Path
            label = Convert-Name $Path
            icon = ""
            source = $Source
        }
    }
    if (![string]::IsNullOrWhiteSpace($IconPath)) {
        $items[$id].icon = $IconPath
    }
}

function Test-ProbablyRealItem {
    param([string]$Path)

    $name = ($Path -replace "^.*/", "")
    if ($name -match "_trim$") { return $false }
    if ($name -match "^(clock|compass|recovery_compass|light)_[0-9]+$") { return $false }
    if ($name -match "^(bow|crossbow)_pulling_[0-9]+$") { return $false }
    if ($name -eq "crossbow_arrow" -or $name -eq "crossbow_firework") { return $false }
    if ($name -eq "fishing_rod_cast" -or $name -eq "shield_blocking") { return $false }
    if ($name -eq "filled_map_markings") { return $false }
    if ($name -like "empty_*") { return $false }
    if ($name -like "leather_*_overlay") { return $false }
    return $true
}

function Get-TextureReference {
    param([string]$Json)
    try {
        $model = $Json | ConvertFrom-Json -ErrorAction Stop
        if ($model.textures) {
            foreach ($prop in @("layer0", "all", "particle", "side", "front", "texture")) {
                if ($model.textures.PSObject.Properties.Name -contains $prop) {
                    $value = [string]$model.textures.$prop
                    if (![string]::IsNullOrWhiteSpace($value) -and !$value.StartsWith("#")) {
                        return $value
                    }
                }
            }
        }
    } catch {
    }
    return ""
}

function Resolve-TextureEntry {
    param(
        [System.IO.Compression.ZipArchive]$Zip,
        [string]$Namespace,
        [string]$ItemPath,
        [string]$TextureRef
    )

    $refs = New-Object System.Collections.Generic.List[string]
    if (![string]::IsNullOrWhiteSpace($TextureRef)) {
        if ($TextureRef.Contains(":")) {
            $parts = $TextureRef.Split(":", 2)
            $refs.Add("assets/$($parts[0])/textures/$($parts[1]).png")
        } else {
            $refs.Add("assets/$Namespace/textures/$TextureRef.png")
        }
    }
    $refs.Add("assets/$Namespace/textures/item/$ItemPath.png")
    $refs.Add("assets/$Namespace/textures/items/$ItemPath.png")
    $refs.Add("assets/$Namespace/textures/block/$ItemPath.png")
    $refs.Add("assets/$Namespace/textures/blocks/$ItemPath.png")

    foreach ($ref in ($refs | Select-Object -Unique)) {
        $entry = $Zip.GetEntry($ref)
        if ($entry) {
            return $entry
        }
    }
    return $null
}

function Resolve-TextureFile {
    param(
        [string]$AssetsRoot,
        [string]$Namespace,
        [string]$ItemPath,
        [string]$TextureRef
    )

    $refs = New-Object System.Collections.Generic.List[string]
    if (![string]::IsNullOrWhiteSpace($TextureRef)) {
        if ($TextureRef.Contains(":")) {
            $parts = $TextureRef.Split(":", 2)
            $refs.Add((Join-Path $AssetsRoot ("{0}\textures\{1}.png" -f $parts[0], ($parts[1] -replace "/", "\"))))
        } else {
            $refs.Add((Join-Path $AssetsRoot ("$Namespace\textures\" + ($TextureRef -replace "/", "\") + ".png")))
        }
    }
    $refs.Add((Join-Path $AssetsRoot ("$Namespace\textures\item\" + ($ItemPath -replace "/", "\") + ".png")))
    $refs.Add((Join-Path $AssetsRoot ("$Namespace\textures\items\" + ($ItemPath -replace "/", "\") + ".png")))
    $refs.Add((Join-Path $AssetsRoot ("$Namespace\textures\block\" + ($ItemPath -replace "/", "\") + ".png")))
    $refs.Add((Join-Path $AssetsRoot ("$Namespace\textures\blocks\" + ($ItemPath -replace "/", "\") + ".png")))

    foreach ($ref in ($refs | Select-Object -Unique)) {
        if (Test-Path -LiteralPath $ref) {
            return $ref
        }
    }
    return ""
}

function Save-IconFromStream {
    param(
        [System.IO.Stream]$Stream,
        [string]$Namespace,
        [string]$ItemPath
    )

    $safeName = ($ItemPath -replace "[\\/]", "_")
    $targetDir = Join-Path $iconsRoot $Namespace
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    $target = Join-Path $targetDir "$safeName.png"
    $out = [System.IO.File]::Create($target)
    try {
        $Stream.CopyTo($out)
    } finally {
        $out.Dispose()
    }
    return $target
}

function Read-LangsFromZip {
    param([System.IO.Compression.ZipArchive]$Zip)
    foreach ($entry in $Zip.Entries) {
        if ($entry.FullName -match '^assets/([^/]+)/lang/(en_us|pt_br)\.json$') {
            $ns = $Matches[1]
            $reader = New-Object System.IO.StreamReader($entry.Open())
            try {
                $json = $reader.ReadToEnd() | ConvertFrom-Json -ErrorAction Stop
                foreach ($prop in $json.PSObject.Properties) {
                    if ($prop.Name -like "item.$ns.*" -or $prop.Name -like "block.$ns.*") {
                        Set-LangValue -Key $prop.Name -Value ([string]$prop.Value)
                    }
                }
            } catch {
            } finally {
                $reader.Dispose()
            }
        }
    }
}

function Read-ItemsFromZip {
    param([string]$JarPath)

    if (!(Test-Path -LiteralPath $JarPath)) {
        return
    }

    $zip = [System.IO.Compression.ZipFile]::OpenRead($JarPath)
    try {
        Read-LangsFromZip -Zip $zip
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -match '^assets/([^/]+)/models/item/(.+)\.json$') {
                $ns = $Matches[1]
                $itemPath = $Matches[2]
                $reader = New-Object System.IO.StreamReader($entry.Open())
                try {
                    $json = $reader.ReadToEnd()
                } finally {
                    $reader.Dispose()
                }

                $textureRef = Get-TextureReference -Json $json
                $textureEntry = Resolve-TextureEntry -Zip $zip -Namespace $ns -ItemPath $itemPath -TextureRef $textureRef
                $icon = ""
                if ($textureEntry) {
                    $stream = $textureEntry.Open()
                    try {
                        $icon = Save-IconFromStream -Stream $stream -Namespace $ns -ItemPath $itemPath
                    } finally {
                        $stream.Dispose()
                    }
                }
                Add-Item -Namespace $ns -Path $itemPath -IconPath $icon -Source (Split-Path -Leaf $JarPath)
            }
        }
    } finally {
        $zip.Dispose()
    }
}

function Read-ItemsFromAssetsFolder {
    param([string]$AssetsPath)

    if (!(Test-Path -LiteralPath $AssetsPath)) {
        return
    }

    Get-ChildItem -Path $AssetsPath -Recurse -Filter "*.json" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\assets\\([^\\]+)\\lang\\(en_us|pt_br)\.json$' } |
        ForEach-Object {
            $ns = $Matches[1]
            try {
                $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json -ErrorAction Stop
                foreach ($prop in $json.PSObject.Properties) {
                    if ($prop.Name -like "item.$ns.*" -or $prop.Name -like "block.$ns.*") {
                        Set-LangValue -Key $prop.Name -Value ([string]$prop.Value)
                    }
                }
            } catch {
            }
        }

    Get-ChildItem -Path $AssetsPath -Recurse -Filter "*.json" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\assets\\([^\\]+)\\models\\item\\(.+)\.json$' } |
        ForEach-Object {
            $ns = $Matches[1]
            $itemPath = ($Matches[2] -replace "\\", "/")
            $json = Get-Content -LiteralPath $_.FullName -Raw
            $textureRef = Get-TextureReference -Json $json
            $textureFile = Resolve-TextureFile -AssetsRoot $AssetsPath -Namespace $ns -ItemPath $itemPath -TextureRef $textureRef
            $icon = ""
            if ($textureFile) {
                $stream = [System.IO.File]::OpenRead($textureFile)
                try {
                    $icon = Save-IconFromStream -Stream $stream -Namespace $ns -ItemPath $itemPath
                } finally {
                    $stream.Dispose()
                }
            }
            Add-Item -Namespace $ns -Path $itemPath -IconPath $icon -Source "MagicWorld source assets"
        }
}

Read-ItemsFromZip -JarPath $vanillaJar
if (Test-Path -LiteralPath $modsRoot) {
    Get-ChildItem -LiteralPath $modsRoot -Filter "*.jar" -File | ForEach-Object {
        Read-ItemsFromZip -JarPath $_.FullName
    }
}
Read-ItemsFromAssetsFolder -AssetsPath $magicWorldAssets

foreach ($item in $items.Values) {
    $itemKey = "item.$($item.namespace).$($item.path)"
    $blockKey = "block.$($item.namespace).$($item.path)"
    if ($langs.ContainsKey($itemKey)) {
        $item.label = $langs[$itemKey]
    } elseif ($langs.ContainsKey($blockKey)) {
        $item.label = $langs[$blockKey]
    }
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("id`tlabel`tnamespace`ticon`tsource")
foreach ($item in ($items.Values | Sort-Object namespace,path)) {
    $relativeIcon = ""
    if (![string]::IsNullOrWhiteSpace($item.icon)) {
        $rootPrefix = $ServerRoot.TrimEnd("\") + "\"
        if ($item.icon.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            $relativeIcon = $item.icon.Substring($rootPrefix.Length)
        } else {
            $relativeIcon = $item.icon
        }
    }
    $lines.Add(("{0}`t{1}`t{2}`t{3}`t{4}" -f $item.id, ($item.label -replace "`t", " "), $item.namespace, $relativeIcon, ($item.source -replace "`t", " ")))
}

Set-Content -LiteralPath $catalogPath -Encoding UTF8 -Value $lines
Write-Host ("Catalogo gerado: {0} itens em {1}" -f ($lines.Count - 1), $catalogPath)
