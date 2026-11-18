#!/usr/bin/env bash
# ==============================================================================
# Configura el entorno local de desarrollo de GestionTurnos y (opcionalmente) lo ejecuta.
#
# Uso:
#   ./setup-dev.sh            Configura user-secrets, restaura y compila.
#   ./setup-dev.sh --run      Igual, y luego levanta la API (dotnet watch).
#   ./setup-dev.sh --run-all  Igual, y levanta API + front Angular (npm run dev).
#
# Lee los valores de .env (copie .env.example). Lo que falte se pregunta en consola.
# ==============================================================================
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$ROOT_DIR/GestionTurnos.Api/GestionTurnos.Api"
FRONT_DIR="$ROOT_DIR/GestionTurnos.IU"
ENV_FILE="$ROOT_DIR/.env"
MODE="${1:-}"

info()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
ok()    { printf '\033[1;32m OK\033[0m %s\n' "$*"; }
fail()  { printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; exit 1; }

# --- 1. Requisitos -----------------------------------------------------------
info "Verificando requisitos"
command -v dotnet >/dev/null 2>&1 || fail "No se encontró 'dotnet'. Instale el SDK de .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0"
if ! dotnet --list-sdks | grep -qE '^10\.'; then
  fail "Se requiere el SDK de .NET 10 (encontrados: $(dotnet --list-sdks | cut -d' ' -f1 | tr '\n' ' '))"
fi
ok "SDK .NET 10 disponible"

# --- 2. Cargar .env (sin ejecutarlo: los valores pueden tener $, ;, etc.) -----
if [[ -f "$ENV_FILE" ]]; then
  info "Leyendo $ENV_FILE"
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    [[ -z "$line" || "$line" == \#* || "$line" != *=* ]] && continue
    key="${line%%=*}"; value="${line#*=}"
    key="$(echo "$key" | tr -d '[:space:]')"
    # Quitar comillas envolventes si las hay
    if [[ "$value" =~ ^\"(.*)\"$ || "$value" =~ ^\'(.*)\'$ ]]; then value="${BASH_REMATCH[1]}"; fi
    [[ "$key" =~ ^[A-Z_][A-Z0-9_]*$ ]] && printf -v "$key" '%s' "$value"
  done < "$ENV_FILE"
else
  info "No existe .env; se pedirán los valores (puede crear uno con: cp .env.example .env)"
fi

ask() { # ask VAR "Pregunta" [default] [secret]
  local var="$1" prompt="$2" def="${3:-}" secret="${4:-}" val="${!1:-}"
  if [[ -z "$val" || "$val" == *"<"*">"* ]]; then
    if [[ -n "$secret" ]]; then read -rsp "$prompt: " val; echo
    else read -rp "$prompt${def:+ [$def]}: " val; val="${val:-$def}"; fi
  fi
  [[ -n "$val" ]] || fail "$var es obligatorio"
  printf -v "$var" '%s' "$val"
}

ask DB_HOST     "Host PostgreSQL" "aws-1-us-east-1.pooler.supabase.com"
ask DB_PORT     "Puerto" "5432"
ask DB_NAME     "Base de datos" "postgres"
ask DB_USER     "Usuario (p. ej. postgres.<id-proyecto>)"
ask DB_PASSWORD "Contraseña de la BD" "" secret

# Contraseña entre comillas simples (las ' internas se duplican) para admitir ; y otros caracteres
PASS_Q="'${DB_PASSWORD//\'/\'\'}'"
CONN="Host=$DB_HOST;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$PASS_Q;SSL Mode=Require;Timeout=15;Maximum Pool Size=10"

# --- 3. User-secrets (quedan solo en esta máquina, fuera del repo) -------------
info "Guardando configuración en user-secrets"
cd "$API_DIR"
dotnet user-secrets set "Database:Provider" "PostgreSql" >/dev/null
dotnet user-secrets set "Database:ConnectionStringName" "PostgreSqlConnection" >/dev/null
dotnet user-secrets set "ConnectionStrings:PostgreSqlConnection" "$CONN" >/dev/null
if [[ -n "${SMTP_USERNAME:-}" && -n "${SMTP_PASSWORD:-}" ]]; then
  dotnet user-secrets set "EmailSettings:Enabled" "true" >/dev/null
  dotnet user-secrets set "EmailSettings:Host" "${SMTP_HOST:-smtp.gmail.com}" >/dev/null
  dotnet user-secrets set "EmailSettings:Port" "${SMTP_PORT:-587}" >/dev/null
  dotnet user-secrets set "EmailSettings:SecureSocket" "StartTls" >/dev/null
  dotnet user-secrets set "EmailSettings:Username" "$SMTP_USERNAME" >/dev/null
  dotnet user-secrets set "EmailSettings:Password" "${SMTP_PASSWORD// /}" >/dev/null
  dotnet user-secrets set "EmailSettings:FromAddress" "${SMTP_FROM_ADDRESS:-$SMTP_USERNAME}" >/dev/null
  ok "Correo SMTP habilitado ($SMTP_USERNAME)"
fi
ok "user-secrets configurados (ver: dotnet user-secrets list)"

# --- 4. Restaurar y compilar ---------------------------------------------------
info "Restaurando y compilando la API"
dotnet build --nologo -v q || fail "La compilación falló"
ok "API compilada"

# --- 5. Primer administrador (opcional) ----------------------------------------
if [[ -n "${ADMIN_CORREO:-}" && -n "${ADMIN_CONTRASENA:-}" ]]; then
  info "Creando administrador inicial (idempotente)"
  AdministradorInicial__Correo="$ADMIN_CORREO" AdministradorInicial__Contrasena="$ADMIN_CONTRASENA" \
    dotnet run --no-build -- seed-admin || info "seed-admin terminó con error (revise el mensaje de arriba)"
fi

# --- 6. Ejecutar ---------------------------------------------------------------
case "$MODE" in
  --run)
    info "Levantando API en http://localhost:5161 (Swagger en la raíz)"
    exec dotnet watch run --launch-profile http
    ;;
  --run-all)
    command -v npm >/dev/null 2>&1 || fail "No se encontró 'npm'. Instale Node.js LTS."
    info "Instalando dependencias npm"
    (cd "$ROOT_DIR" && npm install --no-audit --no-fund)
    (cd "$FRONT_DIR" && npm install --no-audit --no-fund)
    info "Levantando API + front"
    cd "$ROOT_DIR" && exec npm run dev
    ;;
  *)
    echo
    ok "Entorno listo. Para ejecutar:"
    echo "    cd GestionTurnos.Api/GestionTurnos.Api && dotnet run --launch-profile http"
    echo "  o bien: ./setup-dev.sh --run   |   ./setup-dev.sh --run-all"
    ;;
esac
