param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    & $Dotnet build WallpaperLight.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & '.\tests\WallpaperLight.Checks\bin\Release\net10.0-windows\WallpaperLight.Checks.exe' --unit
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
    # A fresh output folder prevents accidentally packaging personal settings.
    $stage = Join-Path $root ('artifacts\staging-' + [guid]::NewGuid().ToString('N'))
    $output = Join-Path $stage 'WallpaperLight'
    & $Dotnet publish WallpaperLight/WallpaperLight.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item LICENSE, docs\PORTABLE.txt -Destination $output
    $assets = Get-Content WallpaperLight/obj/project.assets.json -Raw | ConvertFrom-Json
    $notices = Join-Path $output 'ThirdPartyNotices'
    New-Item -ItemType Directory -Path $notices | Out-Null
    foreach ($runtime in @('Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
        $dependency = $assets.project.frameworks.'net10.0-windows'.downloadDependencies | Where-Object { $_.name -eq $runtime }
        if (!$dependency) { throw "Missing runtime dependency: $runtime" }
        $version = $dependency.version.Trim('[', ']').Split(',')[0].Trim()
        $package = $null
        foreach ($cache in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path $cache ($runtime.ToLowerInvariant() + '/' + $version)
            if (Test-Path $candidate) { $package = $candidate; break }
        }
        if (!$package) { throw "Missing runtime package: $runtime" }
        $destination = Join-Path $notices $runtime
        New-Item -ItemType Directory -Path $destination | Out-Null
        $licenseFiles = @(Get-ChildItem $package -File | Where-Object { $_.Name -match 'LICENSE|THIRD-PARTY-NOTICES' })
        if (!$licenseFiles.Count) { throw "Missing runtime notices: $runtime" }
        $licenseFiles | Copy-Item -Destination $destination
    }
    $archive = Join-Path $root 'artifacts\WallpaperLight-win-x64-self-contained.zip'
    Compress-Archive -Path $output -DestinationPath $archive -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath ($archive + '.sha256') -Encoding ascii
    Write-Output "Archive: $archive"
    Write-Output "Unpacked build: $output"
} finally { Pop-Location }
