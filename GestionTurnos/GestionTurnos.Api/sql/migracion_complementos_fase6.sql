-- =====================================================================
--  MIGRACIÓN INCREMENTAL — COMPLEMENTOS FASE 6 — PostgreSQL 14+
--
--  Para una base que YA tiene sql/gestion_turnos.sql y
--  sql/seed_permisos_administrador.sql (p. ej. la de Supabase).
--  Agrega lo que sql/gestion_turnos_completo.sql incluye además del modelo:
--  índices de reportes/auditoría/cambios de turno, auditoría de 8 tablas
--  más, la tabla "DataProtectionKeys" que usa la API y el rol opcional
--  gestion_turnos_app.
--
--  Idempotente: puede ejecutarse varias veces. Una sola transacción.
--
--    psql "<cadena de conexión>" -v ON_ERROR_STOP=1 -f sql/migracion_complementos_fase6.sql
-- =====================================================================

BEGIN;

DO $$
BEGIN
    IF to_regclass('turnos.asignacion') IS NULL OR to_regclass('auditoria.bitacora_negocio') IS NULL THEN
        RAISE EXCEPTION 'No se encontró el modelo base. Use sql/gestion_turnos_completo.sql en una base vacía.';
    END IF;
END;
$$;

-- =====================================================================
--  12. ÍNDICES COMPLEMENTARIOS (Fase 6)
-- =====================================================================

-- Sección 15 — Auditoría: historial de un registro ordenado por fecha
-- (reemplaza al índice sin fecha), filtros por usuario y por tabla.
DROP INDEX IF EXISTS auditoria.ix_bitacora_registro;
CREATE INDEX IF NOT EXISTS ix_bitacora_registro ON auditoria.bitacora_negocio (esquema, tabla, id_registro, fecha_hora DESC);
CREATE INDEX IF NOT EXISTS ix_bitacora_usuario  ON auditoria.bitacora_negocio (id_usuario, fecha_hora DESC);
CREATE INDEX IF NOT EXISTS ix_bitacora_tabla    ON auditoria.bitacora_negocio (tabla, fecha_hora DESC);

-- Sección 14 — Reportes acotados por rango de fechas sin filtro de empleado.
CREATE INDEX IF NOT EXISTS ix_registro_hora_entrada  ON asistencia.registro (hora_entrada);
CREATE INDEX IF NOT EXISTS ix_novedad_rango          ON novedades.novedad (inicio, fin);
CREATE INDEX IF NOT EXISTS ix_novedad_estado         ON novedades.novedad (cod_estado_solicitud, registrado_en DESC);
CREATE INDEX IF NOT EXISTS ix_vacaciones_rango       ON novedades.solicitud_vacaciones (fecha_inicio, fecha_fin);
CREATE INDEX IF NOT EXISTS ix_vacaciones_estado      ON novedades.solicitud_vacaciones (cod_estado_solicitud, solicitado_en DESC);
CREATE INDEX IF NOT EXISTS ix_periodo_vac_empleado   ON novedades.periodo_vacacional (id_empleado);

-- Sección 16 — Bandeja de cambios de turno y búsqueda por asignación/empleado.
CREATE INDEX IF NOT EXISTS ix_cambio_turno_estado      ON turnos.solicitud_cambio_turno (cod_estado_solicitud, solicitado_en DESC);
CREATE INDEX IF NOT EXISTS ix_cambio_turno_solicitante ON turnos.solicitud_cambio_turno (id_asignacion_solicitante);
CREATE INDEX IF NOT EXISTS ix_cambio_turno_companero   ON turnos.solicitud_cambio_turno (id_asignacion_companero);

-- Llaves foráneas consultadas con frecuencia
CREATE INDEX IF NOT EXISTS ix_usuario_rol         ON seguridad.usuario (id_rol);
CREATE INDEX IF NOT EXISTS ix_area_sucursal       ON organizacion.area (id_sucursal);
CREATE INDEX IF NOT EXISTS ix_detalle_horas_cat   ON asistencia.detalle_horas (cod_categoria_hora);
CREATE INDEX IF NOT EXISTS ix_parametro_valor_cod ON parametrizacion.parametro_valor (cod_parametro, vigente_desde DESC);


-- =====================================================================
--  13. AUDITORÍA COMPLEMENTARIA
--  El cambio de turno se aprueba anulando y creando asignaciones (ya
--  auditadas); aquí se audita además la propia solicitud y su resolución.
-- =====================================================================

CREATE OR REPLACE TRIGGER tg_aud_solicitud_cambio_turno AFTER INSERT OR UPDATE OR DELETE ON turnos.solicitud_cambio_turno
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_solicitud_cambio');
CREATE OR REPLACE TRIGGER tg_aud_periodo_vacacional AFTER INSERT OR UPDATE OR DELETE ON novedades.periodo_vacacional
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_periodo_vacacional');
CREATE OR REPLACE TRIGGER tg_aud_dia_festivo AFTER INSERT OR UPDATE OR DELETE ON parametrizacion.dia_festivo
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('fecha');
CREATE OR REPLACE TRIGGER tg_aud_parametro AFTER INSERT OR UPDATE OR DELETE ON parametrizacion.parametro
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('cod_parametro');
CREATE OR REPLACE TRIGGER tg_aud_sucursal AFTER INSERT OR UPDATE OR DELETE ON organizacion.sucursal
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_sucursal');
CREATE OR REPLACE TRIGGER tg_aud_area AFTER INSERT OR UPDATE OR DELETE ON organizacion.area
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_area');
CREATE OR REPLACE TRIGGER tg_aud_cargo AFTER INSERT OR UPDATE OR DELETE ON organizacion.cargo
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_cargo');
CREATE OR REPLACE TRIGGER tg_aud_rol AFTER INSERT OR UPDATE OR DELETE ON seguridad.rol
    FOR EACH ROW EXECUTE FUNCTION auditoria.fn_registrar_cambio('id_rol');


-- =====================================================================
--  14. LLAVES DE ASP.NET DATA PROTECTION
--  Equivale al modelo de Microsoft.AspNetCore.DataProtection.EntityFrameworkCore
--  usado por DataProtectionKeyContext (PersistKeysToDbContext).
-- =====================================================================

CREATE TABLE IF NOT EXISTS public."DataProtectionKeys" (
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
--  VERIFICACIÓN
-- =====================================================================

DO $$
DECLARE
    v_auditadas integer;
BEGIN
    SELECT count(*) INTO v_auditadas FROM pg_trigger WHERE NOT tgisinternal AND tgname LIKE 'tg_aud_%';
    IF v_auditadas <> 19 THEN
        RAISE EXCEPTION 'Se esperaban 19 disparadores de auditoría y hay %', v_auditadas;
    END IF;
    IF to_regclass('public."DataProtectionKeys"') IS NULL THEN
        RAISE EXCEPTION 'No se creó la tabla DataProtectionKeys';
    END IF;
    RAISE NOTICE 'OK: complementos de la Fase 6 aplicados (% tablas auditadas).', v_auditadas;
END;
$$;

COMMIT;
