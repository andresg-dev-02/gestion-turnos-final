-- =====================================================================
--  MIGRACIÓN INCREMENTAL — FOTO DE PERFIL DEL USUARIO
--  PostgreSQL 14+
--
--  Agrega seguridad.usuario_foto_perfil: referencia a la imagen de perfil
--  de cada usuario (empleado o administrador) almacenada en Cloudinary.
--  No altera ninguna tabla existente.
--
--  Idempotente: puede ejecutarse varias veces. Una sola transacción.
--
--    psql "<cadena de conexión>" -v ON_ERROR_STOP=1 -f sql/migracion_foto_perfil.sql
-- =====================================================================

BEGIN;

DO $$
BEGIN
    IF to_regclass('seguridad.usuario') IS NULL THEN
        RAISE EXCEPTION 'No se encontró seguridad.usuario. Ejecute primero el modelo base.';
    END IF;
END;
$$;

CREATE TABLE IF NOT EXISTS seguridad.usuario_foto_perfil (
    id_usuario       bigint        PRIMARY KEY REFERENCES seguridad.usuario (id_usuario),
    url              varchar(500)  NOT NULL,
    public_id        varchar(255)  NOT NULL,
    actualizado_en   timestamptz   NOT NULL DEFAULT now()
);

COMMENT ON TABLE  seguridad.usuario_foto_perfil IS 'Foto de perfil del usuario. La imagen vive en Cloudinary; aquí solo se guarda su referencia.';
COMMENT ON COLUMN seguridad.usuario_foto_perfil.url IS 'URL segura (https) de la imagen, con versión para invalidar cachés.';
COMMENT ON COLUMN seguridad.usuario_foto_perfil.public_id IS 'Identificador del recurso en Cloudinary (para reemplazarlo o eliminarlo).';

-- Auditoría con la misma función genérica del modelo base.
DROP TRIGGER IF EXISTS tg_aud_usuario_foto_perfil ON seguridad.usuario_foto_perfil;
CREATE TRIGGER tg_aud_usuario_foto_perfil AFTER INSERT OR UPDATE OR DELETE ON seguridad.usuario_foto_perfil
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_usuario');

-- Permisos del rol de aplicación, si existe. DELETE permitido: quitar la foto elimina la referencia.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gestion_turnos_app') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON seguridad.usuario_foto_perfil TO gestion_turnos_app;
    END IF;
END;
$$;

COMMIT;
