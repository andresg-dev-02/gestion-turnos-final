-- =====================================================================
--  SOFTWARE DE GESTIÓN DE TURNOS Y ASISTENCIA
--  SCRIPT COMPLETO DE CREACIÓN DE LA BASE DE DATOS — PostgreSQL 14+
--
--  Consolida en un solo archivo, sobre una base de datos VACÍA:
--    1. Modelo de datos (sql/gestion_turnos.sql): esquemas, catálogos,
--       tablas, restricciones RG-01..RG-06, disparadores, vistas y datos
--       iniciales de parametrización.
--    2. Permisos del rol Administrador (sql/seed_permisos_administrador.sql).
--    3. Complementos alineados con el código de la API hasta la Fase 6:
--       - Índices para reportes (sección 14), auditoría (sección 15) y
--         bandeja de cambios de turno (sección 16).
--       - Auditoría de las tablas que aún no tenían disparador
--         (solicitud de cambio de turno, períodos vacacionales, festivos,
--         parámetros, estructura organizacional y roles).
--       - Tabla "DataProtectionKeys" (llaves de ASP.NET Data Protection).
--       - Rol de aplicación opcional con privilegios mínimos
--         (sin UPDATE/DELETE sobre la bitácora, RG-06).
--       - Zona horaria de la base y verificación final.
--
--  NO crea el usuario administrador: la contraseña debe quedar con bcrypt,
--  por eso se crea desde la API:  dotnet run -- seed-admin
--  (AdministradorInicial__Correo / AdministradorInicial__Contrasena).
--
--  ---------------------------------------------------------------------
--  PASO 0 — Crear la base (como superusuario, conectado a "postgres").
--  CREATE DATABASE no puede ejecutarse dentro de una transacción, por eso
--  va aparte:
--
--    CREATE DATABASE gestion_turnos
--        WITH ENCODING 'UTF8'
--             TEMPLATE template0
--             LC_COLLATE 'es_CO.UTF-8'      -- o 'C.UTF-8' / 'en_US.UTF-8'
--             LC_CTYPE   'es_CO.UTF-8';
--
--  PASO 1 — Ejecutar este script conectado a la base nueva:
--
--    psql -h <host> -U <usuario> -d gestion_turnos -v ON_ERROR_STOP=1 \
--         -f sql/gestion_turnos_completo.sql
--
--  Todo corre en una sola transacción: si algo falla no queda nada a medias.
-- =====================================================================

BEGIN;

-- Zona horaria de la empresa (AppSettings:TimeZone). La API también la fija
-- por conexión (SessionAndTimeZoneInterceptor); esto cubre consultas manuales
-- y la vista v_horas_por_turno (now()).
DO $$
BEGIN
    EXECUTE format('ALTER DATABASE %I SET timezone TO %L', current_database(), 'America/Bogota');
END;
$$;
SET LOCAL timezone TO 'America/Bogota';

-- =====================================================================
--  1 a 10. MODELO DE DATOS (contenido de sql/gestion_turnos.sql)
-- =====================================================================

CREATE EXTENSION IF NOT EXISTS btree_gist;   -- necesario para RG-01

CREATE SCHEMA parametrizacion;
CREATE SCHEMA seguridad;
CREATE SCHEMA organizacion;
CREATE SCHEMA turnos;
CREATE SCHEMA asistencia;
CREATE SCHEMA novedades;
CREATE SCHEMA auditoria;


-- =====================================================================
--  1. ESQUEMA parametrizacion
-- =====================================================================

CREATE TABLE parametrizacion.tipo_documento (
    cod_tipo_documento  varchar(10)  PRIMARY KEY,
    nombre              varchar(60)  NOT NULL UNIQUE,
    activo              boolean      NOT NULL DEFAULT true
);

CREATE TABLE parametrizacion.tipo_contrato (
    cod_tipo_contrato   varchar(20)  PRIMARY KEY,
    nombre              varchar(60)  NOT NULL UNIQUE,
    activo              boolean      NOT NULL DEFAULT true
);

CREATE TABLE parametrizacion.estado_usuario (
    cod_estado_usuario  varchar(20)  PRIMARY KEY,
    nombre              varchar(60)  NOT NULL UNIQUE
);

CREATE TABLE parametrizacion.estado_empleado (
    cod_estado_empleado varchar(20)  PRIMARY KEY,
    nombre              varchar(60)  NOT NULL UNIQUE
);

-- Estados comunes a toda solicitud (registro, vacaciones, permisos, cambios)
CREATE TABLE parametrizacion.estado_solicitud (
    cod_estado_solicitud varchar(20) PRIMARY KEY,
    nombre               varchar(60) NOT NULL UNIQUE,
    es_final             boolean     NOT NULL DEFAULT false
);

CREATE TABLE parametrizacion.estado_asignacion (
    cod_estado_asignacion varchar(20) PRIMARY KEY,
    nombre                varchar(60) NOT NULL UNIQUE
);

-- Tipos de permiso / ausencia con sus valores por defecto (RF-07)
CREATE TABLE parametrizacion.tipo_novedad (
    cod_tipo_novedad     varchar(30)  PRIMARY KEY,
    nombre               varchar(80)  NOT NULL UNIQUE,
    remunerado_defecto   boolean      NOT NULL,
    afecta_horas_defecto boolean      NOT NULL,
    requiere_aprobacion  boolean      NOT NULL DEFAULT true,
    requiere_soporte     boolean      NOT NULL DEFAULT false,
    activo               boolean      NOT NULL DEFAULT true
);

-- Categorías de horas: ordinaria, extra, recargos... (RF-05)
CREATE TABLE parametrizacion.categoria_hora (
    cod_categoria_hora  varchar(30)   PRIMARY KEY,
    nombre              varchar(80)   NOT NULL UNIQUE,
    factor_pago         numeric(5,2)  NOT NULL DEFAULT 1.00 CHECK (factor_pago >= 0),
    activo              boolean       NOT NULL DEFAULT true
);

CREATE TABLE parametrizacion.fuente_marcacion (
    cod_fuente_marcacion varchar(20) PRIMARY KEY,
    nombre               varchar(60) NOT NULL UNIQUE
);

CREATE TABLE parametrizacion.tipo_accion (
    cod_tipo_accion     varchar(20)  PRIMARY KEY,
    nombre              varchar(60)  NOT NULL UNIQUE
);

CREATE TABLE parametrizacion.dia_festivo (
    fecha               date         PRIMARY KEY,
    nombre              varchar(100) NOT NULL
);

-- Reglas de negocio configurables (sección 10 de la ERS)
CREATE TABLE parametrizacion.parametro (
    cod_parametro       varchar(60)  PRIMARY KEY,
    nombre              varchar(120) NOT NULL,
    descripcion         text,
    tipo_dato           varchar(10)  NOT NULL
                        CHECK (tipo_dato IN ('ENTERO','DECIMAL','TEXTO','BOOLEANO','HORA')),
    editable            boolean      NOT NULL DEFAULT true
);

-- Valores con vigencia: cambiar una regla no altera cálculos pasados
CREATE TABLE parametrizacion.parametro_valor (
    id_parametro_valor  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cod_parametro       varchar(60) NOT NULL
                        REFERENCES parametrizacion.parametro (cod_parametro),
    valor               varchar(200) NOT NULL,
    vigente_desde       date NOT NULL,
    vigente_hasta       date,
    CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde),
    EXCLUDE USING gist (
        cod_parametro WITH =,
        daterange(vigente_desde, vigente_hasta, '[]') WITH &&
    )
);


-- =====================================================================
--  2. ESQUEMA seguridad
-- =====================================================================

CREATE TABLE seguridad.rol (
    id_rol        integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre        varchar(50)  NOT NULL UNIQUE,
    descripcion   varchar(200),
    activo        boolean      NOT NULL DEFAULT true
);

CREATE TABLE seguridad.permiso (
    id_permiso    integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    codigo        varchar(80)  NOT NULL UNIQUE,   -- ej. 'vacaciones.aprobar'
    descripcion   varchar(200) NOT NULL
);

-- Nuevos roles se agregan con filas, sin tocar el modelo (RF-01)
CREATE TABLE seguridad.rol_permiso (
    id_rol        integer NOT NULL REFERENCES seguridad.rol (id_rol),
    id_permiso    integer NOT NULL REFERENCES seguridad.permiso (id_permiso),
    PRIMARY KEY (id_rol, id_permiso)
);

CREATE TABLE seguridad.usuario (
    id_usuario         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    correo             varchar(150) NOT NULL,
    hash_contrasena    varchar(100) NOT NULL,          -- bcrypt (RNF-02)
    id_rol             integer      NOT NULL REFERENCES seguridad.rol (id_rol),
    cod_estado_usuario varchar(20)  NOT NULL DEFAULT 'ACTIVO'
                       REFERENCES parametrizacion.estado_usuario (cod_estado_usuario),
    ultimo_acceso      timestamptz,
    creado_en          timestamptz  NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX uq_usuario_correo ON seguridad.usuario (lower(correo));

-- Solicitud de registro que el administrador aprueba o rechaza
CREATE TABLE seguridad.solicitud_registro (
    id_solicitud_registro bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cod_tipo_documento    varchar(10)  NOT NULL
                          REFERENCES parametrizacion.tipo_documento (cod_tipo_documento),
    numero_documento      varchar(30)  NOT NULL,
    nombres               varchar(80)  NOT NULL,
    apellidos             varchar(80)  NOT NULL,
    correo                varchar(150) NOT NULL,
    telefono              varchar(30),
    hash_contrasena       varchar(100) NOT NULL,
    cod_estado_solicitud  varchar(20)  NOT NULL DEFAULT 'PENDIENTE'
                          REFERENCES parametrizacion.estado_solicitud (cod_estado_solicitud),
    solicitado_en         timestamptz  NOT NULL DEFAULT now(),
    revisado_por          bigint REFERENCES seguridad.usuario (id_usuario),
    revisado_en           timestamptz,
    motivo_rechazo        varchar(300),
    id_usuario_creado     bigint UNIQUE REFERENCES seguridad.usuario (id_usuario),
    CHECK ((revisado_por IS NULL) = (revisado_en IS NULL))
);
-- Una sola solicitud pendiente por correo
CREATE UNIQUE INDEX uq_solicitud_registro_pendiente
    ON seguridad.solicitud_registro (lower(correo))
    WHERE cod_estado_solicitud = 'PENDIENTE';


-- =====================================================================
--  3. ESQUEMA organizacion
-- =====================================================================

CREATE TABLE organizacion.sucursal (
    id_sucursal   integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre        varchar(100) NOT NULL UNIQUE,
    direccion     varchar(200),
    ciudad        varchar(80),
    activo        boolean      NOT NULL DEFAULT true
);

CREATE TABLE organizacion.area (
    id_area       integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_sucursal   integer      NOT NULL REFERENCES organizacion.sucursal (id_sucursal),
    nombre        varchar(100) NOT NULL,
    activo        boolean      NOT NULL DEFAULT true,
    UNIQUE (id_sucursal, nombre)
);

CREATE TABLE organizacion.cargo (
    id_cargo      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre        varchar(100) NOT NULL UNIQUE,
    activo        boolean      NOT NULL DEFAULT true
);

-- La sucursal del empleado se obtiene a través de su área (sin redundancia)
CREATE TABLE organizacion.empleado (
    id_empleado         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_usuario          bigint UNIQUE REFERENCES seguridad.usuario (id_usuario),
    cod_tipo_documento  varchar(10)  NOT NULL
                        REFERENCES parametrizacion.tipo_documento (cod_tipo_documento),
    numero_documento    varchar(30)  NOT NULL,
    nombres             varchar(80)  NOT NULL,
    apellidos           varchar(80)  NOT NULL,
    correo_contacto     varchar(150),
    telefono            varchar(30),
    direccion           varchar(200),
    id_area             integer      NOT NULL REFERENCES organizacion.area (id_area),
    id_cargo            integer      NOT NULL REFERENCES organizacion.cargo (id_cargo),
    cod_tipo_contrato   varchar(20)
                        REFERENCES parametrizacion.tipo_contrato (cod_tipo_contrato),
    fecha_ingreso       date         NOT NULL,
    fecha_retiro        date,
    cod_estado_empleado varchar(20)  NOT NULL DEFAULT 'ACTIVO'
                        REFERENCES parametrizacion.estado_empleado (cod_estado_empleado),
    creado_en           timestamptz  NOT NULL DEFAULT now(),
    UNIQUE (cod_tipo_documento, numero_documento),
    CHECK (fecha_retiro IS NULL OR fecha_retiro >= fecha_ingreso)
);
CREATE INDEX ix_empleado_area  ON organizacion.empleado (id_area);
CREATE INDEX ix_empleado_cargo ON organizacion.empleado (id_cargo);


-- =====================================================================
--  4. ESQUEMA turnos
-- =====================================================================

-- Tipos de turno configurables: Mañana, Tarde, Noche, Fin de semana... (RF-03)
CREATE TABLE turnos.turno (
    id_turno          integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre            varchar(60) NOT NULL UNIQUE,
    hora_inicio       time        NOT NULL,
    hora_fin          time        NOT NULL,
    minutos_descanso  integer     NOT NULL DEFAULT 0 CHECK (minutos_descanso >= 0),
    -- RG-05: si la hora final es menor o igual a la inicial, termina al día siguiente
    cruza_medianoche  boolean GENERATED ALWAYS AS (hora_fin <= hora_inicio) STORED,
    duracion_minutos  integer GENERATED ALWAYS AS (
        (CASE WHEN hora_fin > hora_inicio
              THEN EXTRACT(EPOCH FROM (hora_fin - hora_inicio))
              ELSE EXTRACT(EPOCH FROM (hora_fin - hora_inicio)) + 86400
         END / 60)::integer
    ) STORED,
    color             varchar(7),                 -- para el calendario
    activo            boolean     NOT NULL DEFAULT true
);

-- Programación: un turno de un empleado en una fecha (RF-04, CU-01)
CREATE TABLE turnos.asignacion (
    id_asignacion         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_empleado           bigint  NOT NULL REFERENCES organizacion.empleado (id_empleado),
    id_turno              integer NOT NULL REFERENCES turnos.turno (id_turno),
    fecha                 date    NOT NULL,       -- día en que inicia el turno
    -- Inicio y fin reales programados. Se guardan porque el turno puede
    -- reconfigurarse después y la programación histórica no debe cambiar.
    periodo               tstzrange NOT NULL,
    cod_estado_asignacion varchar(20) NOT NULL DEFAULT 'PROGRAMADA'
                          REFERENCES parametrizacion.estado_asignacion (cod_estado_asignacion),
    observacion           varchar(300),
    creado_por            bigint NOT NULL REFERENCES seguridad.usuario (id_usuario),
    creado_en             timestamptz NOT NULL DEFAULT now(),
    CHECK (NOT isempty(periodo) AND lower_inf(periodo) = false AND upper_inf(periodo) = false),
    UNIQUE (id_asignacion, id_empleado),
    -- RG-01: un empleado no puede tener dos turnos vigentes que se superpongan
    CONSTRAINT ex_asignacion_sin_cruce EXCLUDE USING gist (
        id_empleado WITH =,
        periodo     WITH &&
    ) WHERE (cod_estado_asignacion <> 'ANULADA')
);
CREATE INDEX ix_asignacion_fecha    ON turnos.asignacion (fecha);
CREATE INDEX ix_asignacion_empleado ON turnos.asignacion (id_empleado, fecha);
CREATE INDEX ix_asignacion_turno    ON turnos.asignacion (id_turno, fecha);

-- Días de descanso programados
CREATE TABLE turnos.dia_descanso (
    id_dia_descanso bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_empleado     bigint NOT NULL REFERENCES organizacion.empleado (id_empleado),
    fecha           date   NOT NULL,
    creado_por      bigint NOT NULL REFERENCES seguridad.usuario (id_usuario),
    creado_en       timestamptz NOT NULL DEFAULT now(),
    UNIQUE (id_empleado, fecha)
);

-- OPC-03 (Fase 2): cambio de turno entre compañeros
CREATE TABLE turnos.solicitud_cambio_turno (
    id_solicitud_cambio      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_asignacion_solicitante bigint NOT NULL REFERENCES turnos.asignacion (id_asignacion),
    id_asignacion_companero   bigint NOT NULL REFERENCES turnos.asignacion (id_asignacion),
    motivo                   varchar(300),
    cod_estado_solicitud     varchar(20) NOT NULL DEFAULT 'PENDIENTE'
                             REFERENCES parametrizacion.estado_solicitud (cod_estado_solicitud),
    solicitado_en            timestamptz NOT NULL DEFAULT now(),
    resuelto_por             bigint REFERENCES seguridad.usuario (id_usuario),
    resuelto_en              timestamptz,
    motivo_rechazo           varchar(300),
    CHECK (id_asignacion_solicitante <> id_asignacion_companero),
    CHECK ((resuelto_por IS NULL) = (resuelto_en IS NULL))
);


-- =====================================================================
--  5. ESQUEMA asistencia
-- =====================================================================

-- Entrada y salida reales, asociadas al turno programado (RF-05, CU-02)
CREATE TABLE asistencia.registro (
    id_registro          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_empleado          bigint NOT NULL REFERENCES organizacion.empleado (id_empleado),
    id_asignacion        bigint,                 -- nulo si asistió sin turno programado
    hora_entrada         timestamptz NOT NULL,
    hora_salida          timestamptz,            -- nulo mientras el turno está en curso
    cod_fuente_marcacion varchar(20) NOT NULL DEFAULT 'WEB'
                         REFERENCES parametrizacion.fuente_marcacion (cod_fuente_marcacion),
    observacion          varchar(300),
    registrado_por       bigint REFERENCES seguridad.usuario (id_usuario),
    creado_en            timestamptz NOT NULL DEFAULT now(),
    CHECK (hora_salida IS NULL OR hora_salida > hora_entrada),
    UNIQUE (id_asignacion),
    -- Garantiza que la asignación pertenece al mismo empleado
    FOREIGN KEY (id_asignacion, id_empleado)
        REFERENCES turnos.asignacion (id_asignacion, id_empleado)
);
CREATE INDEX ix_registro_empleado ON asistencia.registro (id_empleado, hora_entrada);

-- Resultado de la liquidación de horas por categoría (ordinaria, extra...)
CREATE TABLE asistencia.detalle_horas (
    id_registro        bigint NOT NULL REFERENCES asistencia.registro (id_registro),
    cod_categoria_hora varchar(30) NOT NULL
                       REFERENCES parametrizacion.categoria_hora (cod_categoria_hora),
    minutos            integer NOT NULL CHECK (minutos > 0),
    PRIMARY KEY (id_registro, cod_categoria_hora)
);


-- =====================================================================
--  6. ESQUEMA novedades
-- =====================================================================

-- Días de vacaciones causados por período (el saldo se calcula, no se guarda)
CREATE TABLE novedades.periodo_vacacional (
    id_periodo_vacacional bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_empleado           bigint NOT NULL REFERENCES organizacion.empleado (id_empleado),
    fecha_inicio          date   NOT NULL,
    fecha_fin             date   NOT NULL,
    dias_otorgados        numeric(5,2) NOT NULL CHECK (dias_otorgados >= 0),
    CHECK (fecha_fin > fecha_inicio),
    UNIQUE (id_empleado, fecha_inicio)
);

-- Solicitudes de vacaciones (RF-06, CU-03)
CREATE TABLE novedades.solicitud_vacaciones (
    id_solicitud_vacaciones bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_empleado             bigint NOT NULL REFERENCES organizacion.empleado (id_empleado),
    fecha_inicio            date   NOT NULL,
    fecha_fin               date   NOT NULL,
    dias_solicitados        numeric(5,2) NOT NULL CHECK (dias_solicitados > 0),
    cod_estado_solicitud    varchar(20) NOT NULL DEFAULT 'PENDIENTE'
                            REFERENCES parametrizacion.estado_solicitud (cod_estado_solicitud),
    observacion             varchar(300),
    solicitado_por          bigint NOT NULL REFERENCES seguridad.usuario (id_usuario),
    solicitado_en           timestamptz NOT NULL DEFAULT now(),
    resuelto_por            bigint REFERENCES seguridad.usuario (id_usuario),
    resuelto_en             timestamptz,
    motivo_rechazo          varchar(300),
    CHECK (fecha_fin >= fecha_inicio),
    CHECK ((resuelto_por IS NULL) = (resuelto_en IS NULL)),
    -- No puede haber dos períodos vigentes que se crucen para el mismo empleado
    CONSTRAINT ex_vacaciones_sin_cruce EXCLUDE USING gist (
        id_empleado WITH =,
        daterange(fecha_inicio, fecha_fin, '[]') WITH &&
    ) WHERE (cod_estado_solicitud IN ('PENDIENTE','APROBADA'))
);
CREATE INDEX ix_vacaciones_empleado ON novedades.solicitud_vacaciones (id_empleado, fecha_inicio);

-- Permisos, incapacidades, calamidades y ausencias (RF-07)
CREATE TABLE novedades.novedad (
    id_novedad           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_empleado          bigint NOT NULL REFERENCES organizacion.empleado (id_empleado),
    cod_tipo_novedad     varchar(30) NOT NULL
                         REFERENCES parametrizacion.tipo_novedad (cod_tipo_novedad),
    inicio               timestamptz NOT NULL,
    fin                  timestamptz NOT NULL,
    remunerado           boolean NOT NULL,
    afecta_horas         boolean NOT NULL,
    cod_estado_solicitud varchar(20) NOT NULL DEFAULT 'PENDIENTE'
                         REFERENCES parametrizacion.estado_solicitud (cod_estado_solicitud),
    observaciones        text,
    url_soporte          varchar(500),           -- incapacidad médica, etc.
    registrado_por       bigint NOT NULL REFERENCES seguridad.usuario (id_usuario),
    registrado_en        timestamptz NOT NULL DEFAULT now(),
    resuelto_por         bigint REFERENCES seguridad.usuario (id_usuario),
    resuelto_en          timestamptz,
    motivo_rechazo       varchar(300),
    CHECK (fin > inicio),
    CHECK ((resuelto_por IS NULL) = (resuelto_en IS NULL))
);
CREATE INDEX ix_novedad_empleado ON novedades.novedad (id_empleado, inicio);


-- =====================================================================
--  7. ESQUEMA auditoria — bitácora de negocio (RF-08)
-- =====================================================================

CREATE TABLE auditoria.bitacora_negocio (
    id_bitacora     bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fecha_hora      timestamptz NOT NULL DEFAULT now(),          -- cuándo
    id_usuario      bigint REFERENCES seguridad.usuario (id_usuario),  -- quién
    cod_tipo_accion varchar(20) NOT NULL
                    REFERENCES parametrizacion.tipo_accion (cod_tipo_accion),
    esquema         varchar(63) NOT NULL,                        -- qué registro
    tabla           varchar(63) NOT NULL,
    id_registro     varchar(60) NOT NULL,
    id_empleado     bigint REFERENCES organizacion.empleado (id_empleado), -- empleado afectado
    valor_anterior  jsonb,                                       -- solo campos modificados
    valor_nuevo     jsonb,
    descripcion     varchar(300),
    ip_origen       inet
);
CREATE INDEX ix_bitacora_empleado ON auditoria.bitacora_negocio (id_empleado, fecha_hora DESC);
CREATE INDEX ix_bitacora_fecha    ON auditoria.bitacora_negocio (fecha_hora DESC);
CREATE INDEX ix_bitacora_accion   ON auditoria.bitacora_negocio (cod_tipo_accion, fecha_hora DESC);
CREATE INDEX ix_bitacora_registro ON auditoria.bitacora_negocio (esquema, tabla, id_registro);

-- La bitácora es de solo inserción
CREATE FUNCTION auditoria.fn_bitacora_inmutable() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'La bitácora de auditoría no admite modificaciones ni borrados';
END;
$$;

CREATE TRIGGER tg_bitacora_inmutable
    BEFORE UPDATE OR DELETE ON auditoria.bitacora_negocio
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_bitacora_inmutable();

-- Disparador genérico. La aplicación informa el usuario al iniciar cada
-- transacción:  SET LOCAL app.id_usuario = '15';  SET LOCAL app.ip = '10.0.0.8';
-- Argumento: nombre de la columna PK de la tabla auditada.
CREATE FUNCTION auditoria.fn_registrar_cambio() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER
SET search_path = pg_catalog, pg_temp AS $$
DECLARE
    v_pk       text  := TG_ARGV[0];
    v_anterior jsonb;
    v_nuevo    jsonb;
    v_fila     jsonb;
    v_accion   text;
    v_dif_ant  jsonb;
    v_dif_nue  jsonb;
BEGIN
    IF TG_OP = 'INSERT' THEN
        v_nuevo  := to_jsonb(NEW) - 'hash_contrasena';
        v_fila   := v_nuevo;
        v_accion := 'CREAR';
        v_dif_nue := v_nuevo;
    ELSIF TG_OP = 'UPDATE' THEN
        v_anterior := to_jsonb(OLD) - 'hash_contrasena';
        v_nuevo    := to_jsonb(NEW) - 'hash_contrasena';
        v_fila     := v_nuevo;
        v_accion   := 'MODIFICAR';
        SELECT jsonb_object_agg(a.key, a.value), jsonb_object_agg(a.key, v_nuevo -> a.key)
          INTO v_dif_ant, v_dif_nue
          FROM jsonb_each(v_anterior) a
         WHERE (v_nuevo -> a.key) IS DISTINCT FROM a.value;
        IF v_dif_ant IS NULL THEN
            RETURN NEW;                           -- no cambió nada relevante
        END IF;
    ELSE
        v_anterior := to_jsonb(OLD) - 'hash_contrasena';
        v_fila     := v_anterior;
        v_accion   := 'ELIMINAR';
        v_dif_ant  := v_anterior;
    END IF;

    INSERT INTO auditoria.bitacora_negocio
        (id_usuario, cod_tipo_accion, esquema, tabla, id_registro,
         id_empleado, valor_anterior, valor_nuevo, ip_origen)
    VALUES
        (NULLIF(current_setting('app.id_usuario', true), '')::bigint,
         v_accion, TG_TABLE_SCHEMA, TG_TABLE_NAME, v_fila ->> v_pk,
         (v_fila ->> 'id_empleado')::bigint,
         v_dif_ant, v_dif_nue,
         NULLIF(current_setting('app.ip', true), '')::inet);

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;

-- Tablas de negocio auditadas
CREATE TRIGGER tg_aud_usuario AFTER INSERT OR UPDATE OR DELETE ON seguridad.usuario
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_usuario');
CREATE TRIGGER tg_aud_solicitud_registro AFTER INSERT OR UPDATE OR DELETE ON seguridad.solicitud_registro
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_solicitud_registro');
CREATE TRIGGER tg_aud_rol_permiso AFTER INSERT OR UPDATE OR DELETE ON seguridad.rol_permiso
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_rol');
CREATE TRIGGER tg_aud_empleado AFTER INSERT OR UPDATE OR DELETE ON organizacion.empleado
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_empleado');
CREATE TRIGGER tg_aud_turno AFTER INSERT OR UPDATE OR DELETE ON turnos.turno
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_turno');
CREATE TRIGGER tg_aud_asignacion AFTER INSERT OR UPDATE OR DELETE ON turnos.asignacion
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_asignacion');
CREATE TRIGGER tg_aud_dia_descanso AFTER INSERT OR UPDATE OR DELETE ON turnos.dia_descanso
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_dia_descanso');
CREATE TRIGGER tg_aud_registro AFTER INSERT OR UPDATE OR DELETE ON asistencia.registro
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_registro');
CREATE TRIGGER tg_aud_vacaciones AFTER INSERT OR UPDATE OR DELETE ON novedades.solicitud_vacaciones
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_solicitud_vacaciones');
CREATE TRIGGER tg_aud_novedad AFTER INSERT OR UPDATE OR DELETE ON novedades.novedad
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_novedad');
CREATE TRIGGER tg_aud_parametro_valor AFTER INSERT OR UPDATE OR DELETE ON parametrizacion.parametro_valor
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_parametro_valor');


-- =====================================================================
--  8. REGLAS DE NEGOCIO ENTRE TABLAS
-- =====================================================================

-- RG-02 / RG-03: no programar un turno sobre vacaciones aprobadas,
-- una novedad aprobada o un día de descanso.
CREATE FUNCTION turnos.fn_validar_asignacion() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.cod_estado_asignacion = 'ANULADA' THEN
        RETURN NEW;
    END IF;

    IF EXISTS (SELECT 1 FROM novedades.solicitud_vacaciones v
                WHERE v.id_empleado = NEW.id_empleado
                  AND v.cod_estado_solicitud = 'APROBADA'
                  AND NEW.fecha BETWEEN v.fecha_inicio AND v.fecha_fin) THEN
        RAISE EXCEPTION 'El empleado tiene vacaciones aprobadas en la fecha %', NEW.fecha
              USING ERRCODE = 'check_violation';
    END IF;

    IF EXISTS (SELECT 1 FROM novedades.novedad n
                WHERE n.id_empleado = NEW.id_empleado
                  AND n.cod_estado_solicitud = 'APROBADA'
                  AND tstzrange(n.inicio, n.fin) && NEW.periodo) THEN
        RAISE EXCEPTION 'El empleado tiene un permiso o ausencia aprobada que se cruza con el turno'
              USING ERRCODE = 'check_violation';
    END IF;

    IF EXISTS (SELECT 1 FROM turnos.dia_descanso d
                WHERE d.id_empleado = NEW.id_empleado AND d.fecha = NEW.fecha) THEN
        RAISE EXCEPTION 'El empleado tiene día de descanso programado el %', NEW.fecha
              USING ERRCODE = 'check_violation';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER tg_validar_asignacion
    BEFORE INSERT OR UPDATE OF id_empleado, fecha, periodo, cod_estado_asignacion
    ON turnos.asignacion
    FOR EACH ROW EXECUTE FUNCTION turnos.fn_validar_asignacion();


-- =====================================================================
--  9. VISTAS (valores derivados)
-- =====================================================================

-- Saldo de vacaciones: otorgados, utilizados (aprobados) y pendientes por aprobar
CREATE VIEW novedades.v_saldo_vacaciones AS
SELECT e.id_empleado,
       COALESCE(o.dias_otorgados, 0)                               AS dias_otorgados,
       COALESCE(s.dias_aprobados, 0)                               AS dias_utilizados,
       COALESCE(s.dias_pendientes, 0)                              AS dias_en_solicitud,
       COALESCE(o.dias_otorgados, 0) - COALESCE(s.dias_aprobados, 0) AS dias_disponibles
  FROM organizacion.empleado e
  LEFT JOIN (SELECT id_empleado, SUM(dias_otorgados) AS dias_otorgados
               FROM novedades.periodo_vacacional GROUP BY id_empleado) o USING (id_empleado)
  LEFT JOIN (SELECT id_empleado,
                    SUM(dias_solicitados) FILTER (WHERE cod_estado_solicitud = 'APROBADA')  AS dias_aprobados,
                    SUM(dias_solicitados) FILTER (WHERE cod_estado_solicitud = 'PENDIENTE') AS dias_pendientes
               FROM novedades.solicitud_vacaciones GROUP BY id_empleado) s USING (id_empleado);

-- Programado contra real por turno (RG-07, CU-05). Para semana, mes o año
-- basta agrupar esta vista con date_trunc('week' | 'month' | 'year', fecha).
CREATE VIEW asistencia.v_horas_por_turno AS
SELECT a.id_asignacion,
       a.id_empleado,
       a.fecha,
       a.id_turno,
       (EXTRACT(EPOCH FROM (upper(a.periodo) - lower(a.periodo))) / 60)::integer
           - t.minutos_descanso                                        AS minutos_programados,
       COALESCE((EXTRACT(EPOCH FROM (r.hora_salida - r.hora_entrada)) / 60)::integer
           - t.minutos_descanso, 0)                                    AS minutos_trabajados,
       r.hora_entrada,
       r.hora_salida,
       GREATEST((EXTRACT(EPOCH FROM (r.hora_entrada - lower(a.periodo))) / 60)::integer, 0)
                                                                       AS minutos_tarde,
       (r.id_registro IS NULL AND upper(a.periodo) < now())            AS sin_asistencia
  FROM turnos.asignacion a
  JOIN turnos.turno t USING (id_turno)
  LEFT JOIN asistencia.registro r ON r.id_asignacion = a.id_asignacion
 WHERE a.cod_estado_asignacion <> 'ANULADA';

-- Calendario unificado del empleado: turnos, descansos, vacaciones y novedades (RF-09)
CREATE VIEW turnos.v_calendario AS
SELECT a.id_empleado, 'TURNO'::text AS tipo, t.nombre AS detalle,
       lower(a.periodo) AS inicio, upper(a.periodo) AS fin
  FROM turnos.asignacion a JOIN turnos.turno t USING (id_turno)
 WHERE a.cod_estado_asignacion <> 'ANULADA'
UNION ALL
SELECT d.id_empleado, 'DESCANSO', 'Día de descanso',
       d.fecha::timestamptz, (d.fecha + 1)::timestamptz
  FROM turnos.dia_descanso d
UNION ALL
SELECT v.id_empleado, 'VACACIONES', 'Vacaciones',
       v.fecha_inicio::timestamptz, (v.fecha_fin + 1)::timestamptz
  FROM novedades.solicitud_vacaciones v
 WHERE v.cod_estado_solicitud = 'APROBADA'
UNION ALL
SELECT n.id_empleado, 'NOVEDAD', tn.nombre, n.inicio, n.fin
  FROM novedades.novedad n
  JOIN parametrizacion.tipo_novedad tn USING (cod_tipo_novedad)
 WHERE n.cod_estado_solicitud = 'APROBADA';


-- =====================================================================
--  10. DATOS INICIALES DE PARAMETRIZACIÓN
-- =====================================================================

INSERT INTO parametrizacion.tipo_documento VALUES
    ('CC','Cédula de ciudadanía'), ('CE','Cédula de extranjería'),
    ('PA','Pasaporte'), ('PPT','Permiso por protección temporal');

INSERT INTO parametrizacion.tipo_contrato VALUES
    ('INDEFINIDO','Término indefinido'), ('FIJO','Término fijo'),
    ('OBRA_LABOR','Obra o labor'), ('APRENDIZAJE','Aprendizaje'),
    ('PRESTACION','Prestación de servicios');

INSERT INTO parametrizacion.estado_usuario VALUES
    ('ACTIVO','Activo'), ('INACTIVO','Inactivo'), ('BLOQUEADO','Bloqueado');

INSERT INTO parametrizacion.estado_empleado VALUES
    ('ACTIVO','Activo'), ('INACTIVO','Inactivo'), ('SUSPENDIDO','Suspendido');

INSERT INTO parametrizacion.estado_solicitud VALUES
    ('PENDIENTE','Pendiente',false), ('APROBADA','Aprobada',true),
    ('RECHAZADA','Rechazada',true),  ('CANCELADA','Cancelada',true);

INSERT INTO parametrizacion.estado_asignacion VALUES
    ('PROGRAMADA','Programada'), ('CONFIRMADA','Confirmada / publicada'),
    ('ANULADA','Anulada');

INSERT INTO parametrizacion.tipo_novedad
    (cod_tipo_novedad, nombre, remunerado_defecto, afecta_horas_defecto,
     requiere_aprobacion, requiere_soporte) VALUES
    ('INCAPACIDAD','Incapacidad médica',        true,  true,  false, true),
    ('CALAMIDAD','Calamidad doméstica',         true,  true,  true,  false),
    ('PERMISO_PERSONAL','Permiso personal',     false, true,  true,  false),
    ('AUSENCIA_JUST','Ausencia justificada',    true,  true,  false, true),
    ('AUSENCIA_INJUST','Ausencia injustificada',false, true,  false, false);

INSERT INTO parametrizacion.categoria_hora (cod_categoria_hora, nombre, factor_pago) VALUES
    ('ORDINARIA','Hora ordinaria',1.00),
    ('RECARGO_NOCTURNO','Recargo nocturno',1.35),
    ('EXTRA_DIURNA','Hora extra diurna',1.25),
    ('EXTRA_NOCTURNA','Hora extra nocturna',1.75),
    ('DOMINICAL_FESTIVO','Dominical o festivo',1.75);

INSERT INTO parametrizacion.fuente_marcacion VALUES
    ('WEB','Portal web'), ('MOVIL','Aplicación móvil'),
    ('ADMIN','Registro manual del administrador'), ('BIOMETRICO','Reloj biométrico');

INSERT INTO parametrizacion.tipo_accion VALUES
    ('CREAR','Creación'), ('MODIFICAR','Modificación'), ('ELIMINAR','Eliminación'),
    ('APROBAR','Aprobación'), ('RECHAZAR','Rechazo'), ('INICIO_SESION','Inicio de sesión');

-- Valores de ejemplo: deben validarse con el cliente (sección 10 de la ERS)
INSERT INTO parametrizacion.parametro (cod_parametro, nombre, tipo_dato) VALUES
    ('JORNADA_MAX_HORAS_DIA',     'Horas máximas por día',                      'DECIMAL'),
    ('JORNADA_MAX_HORAS_SEMANA',  'Horas máximas por semana',                   'DECIMAL'),
    ('DESCANSO_MIN_ENTRE_TURNOS', 'Horas mínimas de descanso entre turnos',     'DECIMAL'),
    ('HORA_INICIO_NOCTURNA',      'Hora de inicio de la jornada nocturna',      'HORA'),
    ('HORA_FIN_NOCTURNA',         'Hora de fin de la jornada nocturna',         'HORA'),
    ('TOLERANCIA_ENTRADA_MIN',    'Minutos de tolerancia en la entrada',        'ENTERO'),
    ('VACACIONES_DIAS_POR_ANIO',  'Días hábiles de vacaciones por año',         'DECIMAL'),
    ('CONFLICTO_BLOQUEA',         'Un conflicto bloquea la asignación (si no, solo advierte)', 'BOOLEANO');

INSERT INTO parametrizacion.parametro_valor (cod_parametro, valor, vigente_desde) VALUES
    ('JORNADA_MAX_HORAS_DIA','10','2026-01-01'),
    ('JORNADA_MAX_HORAS_SEMANA','44','2026-01-01'),
    ('DESCANSO_MIN_ENTRE_TURNOS','12','2026-01-01'),
    ('HORA_INICIO_NOCTURNA','19:00','2026-01-01'),
    ('HORA_FIN_NOCTURNA','06:00','2026-01-01'),
    ('TOLERANCIA_ENTRADA_MIN','5','2026-01-01'),
    ('VACACIONES_DIAS_POR_ANIO','15','2026-01-01'),
    ('CONFLICTO_BLOQUEA','true','2026-01-01');

INSERT INTO seguridad.rol (nombre, descripcion) VALUES
    ('Administrador','Gestión general de la plataforma'),
    ('Empleado','Consulta y solicitudes sobre su propia información');

INSERT INTO turnos.turno (nombre, hora_inicio, hora_fin, minutos_descanso, color) VALUES
    ('Mañana',        '06:00','14:00',30,'#F5B041'),
    ('Tarde',         '14:00','22:00',30,'#5DADE2'),
    ('Noche',         '22:00','06:00',30,'#34495E'),
    ('Fin de semana', '08:00','18:00',60,'#58D68D');



-- =====================================================================
--  11. PERMISOS DEL ROL ADMINISTRADOR (Sección 2)
--  Sincronizado con Application/Common/Security/PermisosSistema.cs.
--  La autorización es por código de permiso, nunca por nombre de rol.
-- =====================================================================

INSERT INTO seguridad.permiso (codigo, descripcion) VALUES
    ('usuarios.gestionar',          'Crear, activar y desactivar usuarios; administrar roles y permisos'),
    ('registros.aprobar',           'Aprobar o rechazar solicitudes de registro'),
    ('empleados.gestionar',         'Crear, actualizar, desactivar y reactivar empleados'),
    ('organizacion.gestionar',      'Sucursales, áreas y cargos'),
    ('turnos.configurar',           'Tipos de turno'),
    ('turnos.programar',            'Asignar, modificar, anular y publicar turnos; descansos y cambios de turno'),
    ('asistencia.consultar',        'Ver la asistencia de todos los empleados'),
    ('asistencia.registrar_manual', 'Registrar o corregir marcaciones'),
    ('vacaciones.aprobar',          'Registrar días causados y resolver solicitudes de vacaciones'),
    ('novedades.aprobar',           'Resolver y registrar permisos y ausencias'),
    ('parametros.editar',           'Parámetros de negocio y catálogos'),
    ('reportes.consultar',          'Calendario general y reportes'),
    ('auditoria.consultar',         'Consultar la bitácora de auditoría')
ON CONFLICT (codigo) DO NOTHING;

INSERT INTO seguridad.rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
  FROM seguridad.rol r
 CROSS JOIN seguridad.permiso p
 WHERE r.nombre = 'Administrador'
ON CONFLICT (id_rol, id_permiso) DO NOTHING;
-- El rol Empleado no necesita permisos: sus endpoints ("mi-...") solo exigen sesión.


-- =====================================================================
--  12. ÍNDICES COMPLEMENTARIOS (Fase 6)
-- =====================================================================

-- Sección 15 — Auditoría: historial de un registro ordenado por fecha
-- (reemplaza al índice sin fecha), filtros por usuario y por tabla.
DROP INDEX auditoria.ix_bitacora_registro;
CREATE INDEX ix_bitacora_registro ON auditoria.bitacora_negocio (esquema, tabla, id_registro, fecha_hora DESC);
CREATE INDEX ix_bitacora_usuario  ON auditoria.bitacora_negocio (id_usuario, fecha_hora DESC);
CREATE INDEX ix_bitacora_tabla    ON auditoria.bitacora_negocio (tabla, fecha_hora DESC);

-- Sección 14 — Reportes acotados por rango de fechas sin filtro de empleado.
CREATE INDEX ix_registro_hora_entrada  ON asistencia.registro (hora_entrada);
CREATE INDEX ix_novedad_rango          ON novedades.novedad (inicio, fin);
CREATE INDEX ix_novedad_estado         ON novedades.novedad (cod_estado_solicitud, registrado_en DESC);
CREATE INDEX ix_vacaciones_rango       ON novedades.solicitud_vacaciones (fecha_inicio, fecha_fin);
CREATE INDEX ix_vacaciones_estado      ON novedades.solicitud_vacaciones (cod_estado_solicitud, solicitado_en DESC);
CREATE INDEX ix_periodo_vac_empleado   ON novedades.periodo_vacacional (id_empleado);

-- Sección 16 — Bandeja de cambios de turno y búsqueda por asignación/empleado.
CREATE INDEX ix_cambio_turno_estado      ON turnos.solicitud_cambio_turno (cod_estado_solicitud, solicitado_en DESC);
CREATE INDEX ix_cambio_turno_solicitante ON turnos.solicitud_cambio_turno (id_asignacion_solicitante);
CREATE INDEX ix_cambio_turno_companero   ON turnos.solicitud_cambio_turno (id_asignacion_companero);

-- Llaves foráneas consultadas con frecuencia
CREATE INDEX ix_usuario_rol         ON seguridad.usuario (id_rol);
CREATE INDEX ix_area_sucursal       ON organizacion.area (id_sucursal);
CREATE INDEX ix_detalle_horas_cat   ON asistencia.detalle_horas (cod_categoria_hora);
CREATE INDEX ix_parametro_valor_cod ON parametrizacion.parametro_valor (cod_parametro, vigente_desde DESC);


-- =====================================================================
--  13. AUDITORÍA COMPLEMENTARIA
--  Se crean después de los datos iniciales para no registrar la carga base.
--  El cambio de turno se aprueba anulando y creando asignaciones (ya
--  auditadas); aquí se audita además la propia solicitud y su resolución.
-- =====================================================================

CREATE TRIGGER tg_aud_solicitud_cambio_turno AFTER INSERT OR UPDATE OR DELETE ON turnos.solicitud_cambio_turno
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_solicitud_cambio');
CREATE TRIGGER tg_aud_periodo_vacacional AFTER INSERT OR UPDATE OR DELETE ON novedades.periodo_vacacional
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_periodo_vacacional');
CREATE TRIGGER tg_aud_dia_festivo AFTER INSERT OR UPDATE OR DELETE ON parametrizacion.dia_festivo
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('fecha');
CREATE TRIGGER tg_aud_parametro AFTER INSERT OR UPDATE OR DELETE ON parametrizacion.parametro
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('cod_parametro');
CREATE TRIGGER tg_aud_sucursal AFTER INSERT OR UPDATE OR DELETE ON organizacion.sucursal
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_sucursal');
CREATE TRIGGER tg_aud_area AFTER INSERT OR UPDATE OR DELETE ON organizacion.area
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_area');
CREATE TRIGGER tg_aud_cargo AFTER INSERT OR UPDATE OR DELETE ON organizacion.cargo
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_cargo');
CREATE TRIGGER tg_aud_rol AFTER INSERT OR UPDATE OR DELETE ON seguridad.rol
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_rol');


-- =====================================================================
--  14. LLAVES DE ASP.NET DATA PROTECTION
--  Equivale al modelo de Microsoft.AspNetCore.DataProtection.EntityFrameworkCore
--  usado por DataProtectionKeyContext (PersistKeysToDbContext).
-- =====================================================================

CREATE TABLE public."DataProtectionKeys" (
    "Id"           integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "FriendlyName" text,
    "Xml"          text
);


-- =====================================================================
--  15. ROL DE APLICACIÓN CON PRIVILEGIOS MÍNIMOS (opcional)
--  Rol sin login; el DBA crea el usuario de conexión y lo hace miembro:
--    CREATE ROLE gestion_turnos_api LOGIN PASSWORD '<secreto>' IN ROLE gestion_turnos_app;
--  Coherente con UnitOfWork: no hay DELETE salvo en detalle_horas
--  (reliquidación), rol_permiso y dia_festivo; la bitácora solo admite
--  INSERT y SELECT.
-- =====================================================================

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gestion_turnos_app') THEN
        CREATE ROLE gestion_turnos_app NOLOGIN;
    END IF;
END;
$$;

GRANT USAGE ON SCHEMA parametrizacion, seguridad, organizacion, turnos, asistencia, novedades, auditoria
    TO gestion_turnos_app;

GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA
    parametrizacion, seguridad, organizacion, turnos, asistencia, novedades
    TO gestion_turnos_app;
GRANT DELETE ON asistencia.detalle_horas, seguridad.rol_permiso, parametrizacion.dia_festivo
    TO gestion_turnos_app;

GRANT SELECT, INSERT ON auditoria.bitacora_negocio TO gestion_turnos_app;
REVOKE UPDATE, DELETE, TRUNCATE ON auditoria.bitacora_negocio FROM gestion_turnos_app;

GRANT SELECT, INSERT, UPDATE, DELETE ON public."DataProtectionKeys" TO gestion_turnos_app;

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA
    parametrizacion, seguridad, organizacion, turnos, asistencia, novedades, auditoria, public
    TO gestion_turnos_app;


-- =====================================================================
--  16. VERIFICACIÓN FINAL (aborta y revierte todo si algo no cuadra)
-- =====================================================================

DO $$
DECLARE
    v_tablas    integer;
    v_vistas    integer;
    v_permisos  integer;
    v_admin     integer;
    v_params    integer;
    v_auditadas integer;
BEGIN
    SELECT count(*) INTO v_tablas
      FROM information_schema.tables
     WHERE table_type = 'BASE TABLE'
       AND table_schema IN ('parametrizacion','seguridad','organizacion','turnos','asistencia','novedades','auditoria');

    SELECT count(*) INTO v_vistas
      FROM information_schema.views
     WHERE table_schema IN ('turnos','asistencia','novedades');

    SELECT count(*) INTO v_permisos FROM seguridad.permiso;

    SELECT count(*) INTO v_admin
      FROM seguridad.rol_permiso rp JOIN seguridad.rol r USING (id_rol)
     WHERE r.nombre = 'Administrador';

    SELECT count(DISTINCT p.cod_parametro) INTO v_params
      FROM parametrizacion.parametro p
      JOIN parametrizacion.parametro_valor pv USING (cod_parametro);

    SELECT count(*) INTO v_auditadas
      FROM pg_trigger
     WHERE NOT tgisinternal AND tgname LIKE 'tg_aud_%';

    IF v_tablas <> 32 THEN
        RAISE EXCEPTION 'Se esperaban 32 tablas de negocio y hay %', v_tablas;
    END IF;
    IF v_vistas <> 3 THEN
        RAISE EXCEPTION 'Se esperaban 3 vistas y hay %', v_vistas;
    END IF;
    IF v_permisos <> 13 OR v_admin <> 13 THEN
        RAISE EXCEPTION 'Permisos: % cargados, % asignados al Administrador (se esperaban 13 y 13)', v_permisos, v_admin;
    END IF;
    IF v_params <> 8 THEN
        RAISE EXCEPTION 'Se esperaban 8 parámetros con valor vigente y hay %', v_params;
    END IF;
    IF v_auditadas <> 19 THEN
        RAISE EXCEPTION 'Se esperaban 19 disparadores de auditoría y hay %', v_auditadas;
    END IF;

    RAISE NOTICE 'OK: % tablas, % vistas, % permisos del Administrador, % parámetros, % tablas auditadas.',
        v_tablas, v_vistas, v_admin, v_params, v_auditadas;
    RAISE NOTICE 'Siguiente paso: crear el administrador con  dotnet run -- seed-admin';
END;
$$;

COMMIT;
