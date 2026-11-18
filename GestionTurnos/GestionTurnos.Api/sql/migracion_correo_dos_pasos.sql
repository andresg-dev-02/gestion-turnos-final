-- =====================================================================
--  MIGRACIÓN INCREMENTAL — SERVICIO DE CORREO / VERIFICACIÓN EN DOS PASOS
--  PostgreSQL 14+
--
--  Agrega seguridad.usuario_dos_pasos: preferencia OPCIONAL de cada usuario
--  para exigir un código enviado por correo al iniciar sesión.
--  No altera ninguna tabla existente.
--
--  Las notificaciones de registro y de rotación de turnos NO requieren
--  cambios en la base de datos.
--
--  Idempotente: puede ejecutarse varias veces. Una sola transacción.
--
--    psql "<cadena de conexión>" -v ON_ERROR_STOP=1 -f sql/migracion_correo_dos_pasos.sql
--
--  Ejecútela ANTES de poner DosPasos:Disponible = true en la configuración.
-- =====================================================================

BEGIN;

DO $$
BEGIN
    IF to_regclass('seguridad.usuario') IS NULL THEN
        RAISE EXCEPTION 'No se encontró seguridad.usuario. Ejecute primero el modelo base.';
    END IF;
END;
$$;

CREATE TABLE IF NOT EXISTS seguridad.usuario_dos_pasos (
    id_usuario       bigint       PRIMARY KEY REFERENCES seguridad.usuario (id_usuario),
    habilitado       boolean      NOT NULL DEFAULT false,
    habilitado_en    timestamptz  NULL,
    actualizado_en   timestamptz  NOT NULL DEFAULT now(),
    actualizado_por  bigint       NULL REFERENCES seguridad.usuario (id_usuario),
    CONSTRAINT ck_usuario_dos_pasos_habilitado_en CHECK (NOT habilitado OR habilitado_en IS NOT NULL)
);

COMMENT ON TABLE  seguridad.usuario_dos_pasos IS 'Preferencia opcional de verificación en dos pasos por correo. No se borra: se desactiva.';
COMMENT ON COLUMN seguridad.usuario_dos_pasos.actualizado_por IS 'Usuario que hizo el último cambio (el propio usuario o un administrador que la restableció).';

-- Auditoría con la misma función genérica del modelo base (sección 7).
DROP TRIGGER IF EXISTS tg_aud_usuario_dos_pasos ON seguridad.usuario_dos_pasos;
CREATE TRIGGER tg_aud_usuario_dos_pasos AFTER INSERT OR UPDATE OR DELETE ON seguridad.usuario_dos_pasos
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_usuario');

-- Permisos del rol de aplicación, si existe (sin DELETE: RG-06).
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gestion_turnos_app') THEN
        GRANT SELECT, INSERT, UPDATE ON seguridad.usuario_dos_pasos TO gestion_turnos_app;
    END IF;
END;
$$;

COMMIT;
