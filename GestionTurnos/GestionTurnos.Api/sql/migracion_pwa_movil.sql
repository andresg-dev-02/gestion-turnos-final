-- =====================================================================
--  MIGRACIÓN INCREMENTAL — APP MÓVIL (SESIÓN PERSISTENTE Y MARCAJE QR)
--  PostgreSQL 14+
--
--  1. asistencia.registro.salida_no_registrada: una jornada sin salida que
--     venció se cierra para revisión del administrador (hora_salida sigue NULL).
--  2. Índice único: a lo sumo una jornada abierta por empleado (evita dobles
--     entradas por escaneos simultáneos).
--  3. seguridad.sesion_dispositivo: refresh tokens revocables (solo el hash).
--
--  Idempotente: puede ejecutarse varias veces. Una sola transacción.
--  En Supabase: SQL Editor → pegar el archivo completo → Run.
-- =====================================================================

BEGIN;

-- 1. Jornadas cerradas sin salida ---------------------------------------
ALTER TABLE asistencia.registro
  ADD COLUMN IF NOT EXISTS salida_no_registrada boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN asistencia.registro.salida_no_registrada IS
  'La jornada quedó sin salida y se cerró automáticamente al vencer. El administrador la corrige con el registro manual.';

-- 2. Una sola jornada abierta por empleado --------------------------------
CREATE UNIQUE INDEX IF NOT EXISTS ux_registro_abierto_por_empleado
  ON asistencia.registro (id_empleado)
  WHERE hora_salida IS NULL AND NOT salida_no_registrada;

-- 3. Sesiones renovables por dispositivo ----------------------------------
CREATE TABLE IF NOT EXISTS seguridad.sesion_dispositivo (
  id_sesion bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  id_usuario bigint NOT NULL REFERENCES seguridad.usuario (id_usuario),
  hash_token varchar(64) NOT NULL,
  familia uuid NOT NULL,
  recordar boolean NOT NULL DEFAULT false,
  creado_en timestamptz NOT NULL DEFAULT now(),
  expira_en timestamptz NOT NULL,
  revocada_en timestamptz NULL,
  motivo_revocacion varchar(30) NULL,
  user_agent varchar(300) NULL,
  ip_origen varchar(64) NULL,
  CONSTRAINT uq_sesion_dispositivo_hash UNIQUE (hash_token)
);

CREATE INDEX IF NOT EXISTS ix_sesion_dispositivo_usuario
  ON seguridad.sesion_dispositivo (id_usuario)
  WHERE revocada_en IS NULL;

CREATE INDEX IF NOT EXISTS ix_sesion_dispositivo_familia
  ON seguridad.sesion_dispositivo (familia);

COMMENT ON TABLE seguridad.sesion_dispositivo IS
  'Refresh tokens por dispositivo (solo el hash SHA-256). Se rotan en cada uso; reutilizar uno rotado revoca la familia.';

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gestion_turnos_app') THEN
    GRANT SELECT, INSERT, UPDATE ON seguridad.sesion_dispositivo TO gestion_turnos_app;
  END IF;
END;
$$;

COMMIT;
