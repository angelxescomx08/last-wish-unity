<#
.SYNOPSIS
    Supabase local en Docker para la telemetría de poder (docs/telemetry.md).

.DESCRIPTION
    start       Arranca Supabase (Docker), aplica supabase/migrations y conecta el juego a esta base local.
    connect     Solo escribe telemetry_config.json (URL + clave publicable) en la carpeta de datos del juego.
    disconnect  Borra ese archivo: el juego vuelve a la configuración de Resources/Telemetry/telemetry.json.
    status      URLs y claves locales (Studio: http://127.0.0.1:54323).
    test        Ejecuta las pruebas SQL (supabase/tests).
    reset       Borra los datos locales y vuelve a aplicar las migraciones.
    stop        Para los contenedores (los datos se conservan).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools/Telemetry/LocalSupabase.ps1 start
#>
param(
    [ValidateSet('start', 'connect', 'disconnect', 'status', 'test', 'reset', 'stop')]
    [string]$Command = 'start',
    # Application.persistentDataPath del juego (Windows: LocalLow\<companyName>\<productName>).
    [string]$DataPath = (Join-Path $env:USERPROFILE ('AppData\LocalLow\Last Wish\' + [char]0xDA + 'ltimo Deseo')),
    # Versión probada del CLI de Supabase (se descarga con npx la primera vez).
    [string]$CliVersion = '2.122.0'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$configFile = Join-Path $DataPath 'telemetry_config.json'

function Invoke-Supabase {
    param([string[]]$Arguments)
    $installed = Get-Command supabase -ErrorAction SilentlyContinue
    if ($installed) { & supabase @Arguments --workdir $projectRoot }
    else {
        if (-not (Get-Command npx -ErrorAction SilentlyContinue)) { throw 'Falta Node.js (npx) o el CLI de Supabase: instala uno de los dos (https://supabase.com/docs/guides/local-development/cli/getting-started).' }
        & npx --yes "supabase@$CliVersion" @Arguments --workdir $projectRoot
    }
    if ($LASTEXITCODE -ne 0) { throw "supabase $($Arguments -join ' ') terminó con código $LASTEXITCODE" }
}

function Assert-Docker {
    docker info --format '{{.ServerVersion}}' *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Docker no responde: abre Docker Desktop, espera a que diga "Engine running" y repite.' }
}

function Connect-Game {
    $status = (Invoke-Supabase @('status', '-o', 'json') | Out-String) | ConvertFrom-Json
    $key = if ($status.PUBLISHABLE_KEY) { $status.PUBLISHABLE_KEY } else { $status.ANON_KEY }
    if (-not $status.API_URL -or -not $key) { throw 'supabase status no devolvió API_URL ni clave: ¿está arrancado?' }
    New-Item -ItemType Directory -Force -Path $DataPath | Out-Null
    $json = [ordered]@{ enabled = $true; url = $status.API_URL; key = $key } | ConvertTo-Json
    [System.IO.File]::WriteAllText($configFile, $json, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Juego conectado a $($status.API_URL) ($configFile)."
    Write-Host "Studio: $($status.STUDIO_URL)  ·  consultas de poder: docs/telemetry.md"
    Write-Host 'Si el juego estaba abierto, sal de Play y vuelve a entrar.'
}

switch ($Command) {
    'start' { Assert-Docker; Invoke-Supabase @('start'); Connect-Game }
    'connect' { Connect-Game }
    'disconnect' {
        if (Test-Path -LiteralPath $configFile) { Remove-Item -LiteralPath $configFile; Write-Host "Borrado $configFile." }
        else { Write-Host 'El juego no tenía configuración local.' }
    }
    'status' { Invoke-Supabase @('status') }
    'test' { Invoke-Supabase @('test', 'db') }
    'reset' { Invoke-Supabase @('db', 'reset') }
    'stop' { Invoke-Supabase @('stop') }
}
