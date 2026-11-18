# GestionTurnos

Gestión de Turnos

## Puesta en marcha (desarrollo)

La cadena de conexión a la base de datos **no está en el repositorio**: cada desarrollador la guarda
en sus user-secrets locales. El script `setup-dev` lo hace por usted.

Requisitos: SDK de .NET 10 (y Node.js LTS si va a levantar el front).

1. Pida al equipo, por un canal privado, los datos de la base de datos (usuario y contraseña de Supabase).
2. Cree su archivo `.env` (está en `.gitignore`, nunca se sube):

   ```bash
   cp .env.example .env      # Windows: copy .env.example .env
   ```

   y complete `DB_USER` y `DB_PASSWORD`. Si no crea el `.env`, el script pide los datos en consola.
3. Ejecute el script desde la raíz del repositorio:

   | Sistema | Solo configurar | Configurar y levantar la API | API + front |
   |---|---|---|---|
   | Linux / macOS / Git Bash | `./setup-dev.sh` | `./setup-dev.sh --run` | `./setup-dev.sh --run-all` |
   | Windows PowerShell | `.\setup-dev.ps1` | `.\setup-dev.ps1 -Run` | `.\setup-dev.ps1 -RunAll` |

   Si PowerShell bloquea el script: `powershell -ExecutionPolicy Bypass -File .\setup-dev.ps1`.

La API queda en `http://localhost:5161` (Swagger en la raíz). Después de la primera configuración
basta con `dotnet run` (o F5 en Visual Studio / Rider): los user-secrets persisten en su máquina.

## Base de datos: permisos y primer administrador

Las tablas ya existen (se crean con `GestionTurnos.Api/sql/gestion_turnos.sql`, PostgreSQL 14+).
No se usan migraciones de EF Core ni `EnsureCreated()`.

Sin estos dos pasos, todas las operaciones administrativas responden **403**. Ejecútelos en este orden.

### 1. Permisos del rol Administrador

Carga los 13 códigos de permiso en `seguridad.permiso` y los asigna al rol Administrador
en `seguridad.rol_permiso`. Es idempotente: se puede ejecutar varias veces sin duplicar filas.

```bash
cd GestionTurnos.Api
psql -h <host> -p <puerto> -U <usuario> -d <base> -v ON_ERROR_STOP=1 \
     -f sql/seed_permisos_administrador.sql
```

Si termina bien muestra `NOTICE: OK: 13 permisos cargados y asignados al rol Administrador`.
Si el resultado no es 13/13, el script falla y revierte la transacción.

Los códigos también están en `Application/Common/Security/PermisosSistema.cs`. Si cambia uno,
cambie los dos archivos.

### 2. Primer administrador

Crea el primer usuario activo con rol Administrador. La contraseña se guarda con el hash del
`BcryptPasswordHasher` del proyecto. No hay endpoint para esto: se ejecuta desde la línea de comandos.

El correo y la contraseña salen de la configuración y **no se escriben en el repositorio**.
Use user-secrets en desarrollo:

```bash
cd GestionTurnos.Api/GestionTurnos.Api
dotnet user-secrets set "AdministradorInicial:Correo" "admin@dominio.com"
dotnet user-secrets set "AdministradorInicial:Contrasena" "<contraseña de al menos 12 caracteres>"
dotnet run -- seed-admin
```

O variables de entorno (por ejemplo, en un servidor):

```bash
export AdministradorInicial__Correo="admin@dominio.com"
export AdministradorInicial__Contrasena="<contraseña de al menos 12 caracteres>"
dotnet run -- seed-admin
```

El comando:

- termina con código 1 si el rol Administrador no tiene los 13 permisos (ejecute antes el paso 1);
- no hace cambios si ya existe un administrador activo (es idempotente);
- no modifica un usuario existente con ese correo que no sea administrador; lo informa y termina con código 1.

Cambie la contraseña inicial después del primer inicio de sesión y borre el secreto
(`dotnet user-secrets remove "AdministradorInicial:Contrasena"`).

### Verificación

```sql
SELECT count(*) FROM seguridad.permiso;                         -- 13
SELECT count(*) FROM seguridad.rol_permiso rp
  JOIN seguridad.rol r USING (id_rol) WHERE r.nombre = 'Administrador';  -- 13
SELECT u.correo FROM seguridad.usuario u
  JOIN seguridad.rol r USING (id_rol)
 WHERE r.nombre = 'Administrador' AND u.cod_estado_usuario = 'ACTIVO';    -- 1 fila
```
