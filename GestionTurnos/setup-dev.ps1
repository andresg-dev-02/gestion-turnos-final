# ==============================================================================
# Configura el entorno local de desarrollo de GestionTurnos (Windows PowerShell).
#
# Uso:
#   .\setup-dev.ps1            Configura user-secrets, restaura y compila.
#   .\setup-dev.ps1 -Run       Igual, y luego levanta la API (dotnet watch).
#   .\setup-dev.ps1 -RunAll    Igual, y levanta API + front Angular (npm run dev).
#
# Si PowerShell bloquea el script:
#   powershell -ExecutionPolicy Bypass -File .\setup-dev.ps1
# ==============================================================================
param([switch]$Run, [switch]$RunAll)
$ErrorActionPreference = 'Stop'

$RootDir  = $PSScriptRoot
$ApiDir   = Join-Path $RootDir 'GestionTurnos.Api\GestionTurnos.Api'
$FrontDir = Join-Path $RootDir 'GestionTurnos.IU'
$EnvFile  = Join-Path $RootDir '.env'

function Info($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host " OK $m" -ForegroundColor Green }
function Fail($m) { Write-Host "ERROR: $m" -ForegroundColor Red; exit 1 }

# --- 1. Requisitos ---
Info 'Verificando requisitos'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail "No se encontró 'dotnet'. Instale el SDK de .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0"
}
if (-not (dotnet --list-sdks | Select-String '^10\.')) { Fail 'Se requiere el SDK de .NET 10.' }
Ok 'SDK .NET 10 disponible'

# --- 2. Cargar .env ---
$cfg = @{}
if (Test-Path $EnvFile) {
    Info "Leyendo $EnvFile"
    foreach ($line in Get-Content $EnvFile) {
        if ($line -match '^\s*#' -or $line -notmatch '=') { continue }
        $k, $v = $line -split '=', 2
        $v = $v.Trim()
        if ($v -match '^"(.*)"$' -or $v -match "^'(.*)'$") { $v = $Matches[1] }
        $cfg[$k.Trim()] = $v
    }
} else {
    Info 'No existe .env; se pedirán los valores (puede crear uno: copy .env.example .env)'
}

function Ask($key, $prompt, $default = '', [switch]$Secret) {
    $val = $cfg[$key]
    if ([string]::IsNullOrWhiteSpace($val) -or $val -match '<.*>') {
        if ($Secret) {
            $s = Read-Host $prompt -AsSecureString
            $val = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s))
        } else {
            $val = Read-Host "$prompt [$default]"
            if ([string]::IsNullOrWhiteSpace($val)) { $val = $default }
        }
    }
    if ([string]::IsNullOrWhiteSpace($val)) { Fail "$key es obligatorio" }
    return $val
}

$DbHost = Ask 'DB_HOST' 'Host PostgreSQL' 'aws-1-us-east-1.pooler.supabase.com'
$DbPort = Ask 'DB_PORT' 'Puerto' '5432'
$DbName = Ask 'DB_NAME' 'Base de datos' 'postgres'
$DbUser = Ask 'DB_USER' 'Usuario (p. ej. postgres.<id-proyecto>)'
$DbPass = Ask 'DB_PASSWORD' 'Contraseña de la BD' -Secret

# Contraseña entre comillas simples (las ' internas se duplican) para admitir ; y otros caracteres
$PassQ = "'" + ($DbPass -replace "'", "''") + "'"
$Conn = "Host=$DbHost;Port=$DbPort;Database=$DbName;Username=$DbUser;Password=$PassQ;SSL Mode=Require;Timeout=15;Maximum Pool Size=10"

# --- 3. User-secrets ---
Info 'Guardando configuración en user-secrets'
Push-Location $ApiDir
try {
    dotnet user-secrets set 'Database:Provider' 'PostgreSql' | Out-Null
    dotnet user-secrets set 'Database:ConnectionStringName' 'PostgreSqlConnection' | Out-Null
    dotnet user-secrets set 'ConnectionStrings:PostgreSqlConnection' $Conn | Out-Null
    if ($cfg['SMTP_USERNAME'] -and $cfg['SMTP_PASSWORD']) {
        $SmtpHost = if ($cfg['SMTP_HOST']) { $cfg['SMTP_HOST'] } else { 'smtp.gmail.com' }
        $SmtpPort = if ($cfg['SMTP_PORT']) { $cfg['SMTP_PORT'] } else { '587' }
        $SmtpFrom = if ($cfg['SMTP_FROM_ADDRESS']) { $cfg['SMTP_FROM_ADDRESS'] } else { $cfg['SMTP_USERNAME'] }
        dotnet user-secrets set 'EmailSettings:Enabled' 'true' | Out-Null
        dotnet user-secrets set 'EmailSettings:Host' $SmtpHost | Out-Null
        dotnet user-secrets set 'EmailSettings:Port' $SmtpPort | Out-Null
        dotnet user-secrets set 'EmailSettings:SecureSocket' 'StartTls' | Out-Null
        dotnet user-secrets set 'EmailSettings:Username' $cfg['SMTP_USERNAME'] | Out-Null
        dotnet user-secrets set 'EmailSettings:Password' ($cfg['SMTP_PASSWORD'] -replace '\s', '') | Out-Null
        dotnet user-secrets set 'EmailSettings:FromAddress' $SmtpFrom | Out-Null
        Ok "Correo SMTP habilitado ($($cfg['SMTP_USERNAME']))"
    }
    Ok 'user-secrets configurados (ver: dotnet user-secrets list)'

    # --- 4. Compilar ---
    Info 'Restaurando y compilando la API'
    dotnet build --nologo -v q
    if ($LASTEXITCODE -ne 0) { Fail 'La compilación falló' }
    Ok 'API compilada'

    # --- 5. Primer administrador (opcional) ---
    if ($cfg['ADMIN_CORREO'] -and $cfg['ADMIN_CONTRASENA']) {
        Info 'Creando administrador inicial (idempotente)'
        $env:AdministradorInicial__Correo = $cfg['ADMIN_CORREO']
        $env:AdministradorInicial__Contrasena = $cfg['ADMIN_CONTRASENA']
        dotnet run --no-build -- seed-admin
        Remove-Item Env:AdministradorInicial__Correo, Env:AdministradorInicial__Contrasena
    }

    # --- 6. Ejecutar ---
    if ($Run) {
        Info 'Levantando API en http://localhost:5161 (Swagger en la raíz)'
        dotnet watch run --launch-profile http
    } elseif ($RunAll) {
        if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { Fail "No se encontró 'npm'. Instale Node.js LTS." }
        Info 'Instalando dependencias npm'
        Push-Location $RootDir;  npm install --no-audit --no-fund; Pop-Location
        Push-Location $FrontDir; npm install --no-audit --no-fund; Pop-Location
        Info 'Levantando API + front'
        Set-Location $RootDir
        npm run dev
    } else {
        Write-Host ''
        Ok 'Entorno listo. Para ejecutar:'
        Write-Host '    cd GestionTurnos.Api\GestionTurnos.Api; dotnet run --launch-profile http'
        Write-Host '  o bien: .\setup-dev.ps1 -Run   |   .\setup-dev.ps1 -RunAll'
    }
} finally {
    Pop-Location
}
