-- =====================================================================
--  PERMISOS DEL ROL ADMINISTRADOR (Sección 2)
--  Motor: PostgreSQL 14+   ·   Requiere: gestion_turnos.sql ya ejecutado
--
--  Carga los 13 códigos de permiso en seguridad.permiso y los asigna
--  todos al rol Administrador en seguridad.rol_permiso.
--
--  Idempotente: puede ejecutarse varias veces sin duplicar filas.
--  Mantener sincronizado con Application/Common/Security/PermisosSistema.cs
--
--  Uso:  psql -h <host> -U <usuario> -d <base> -v ON_ERROR_STOP=1 \
--             -f sql/seed_permisos_administrador.sql
-- =====================================================================

BEGIN;

-- Código antiguo reemplazado por 'turnos.configurar' (se renombra si existiera).
UPDATE seguridad.permiso
   SET codigo = 'turnos.configurar'
 WHERE codigo = 'turnos.gestionar'
   AND NOT EXISTS (SELECT 1 FROM seguridad.permiso WHERE codigo = 'turnos.configurar');

-- 1. Catálogo de permisos (inserta los que falten y actualiza la descripción)
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
ON CONFLICT (codigo) DO UPDATE
    SET descripcion = EXCLUDED.descripcion
  WHERE seguridad.permiso.descripcion IS DISTINCT FROM EXCLUDED.descripcion;

-- 2. Asignación de los 13 permisos al rol Administrador
INSERT INTO seguridad.rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
  FROM seguridad.rol r
 CROSS JOIN seguridad.permiso p
 WHERE r.nombre = 'Administrador'
   AND p.codigo IN (
        'usuarios.gestionar', 'registros.aprobar', 'empleados.gestionar',
        'organizacion.gestionar', 'turnos.configurar', 'turnos.programar',
        'asistencia.consultar', 'asistencia.registrar_manual', 'vacaciones.aprobar',
        'novedades.aprobar', 'parametros.editar', 'reportes.consultar',
        'auditoria.consultar')
ON CONFLICT (id_rol, id_permiso) DO NOTHING;

-- 3. Verificación: aborta (y revierte) si el resultado no es el esperado
DO $$
DECLARE
    v_permisos integer;
    v_admin    integer;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM seguridad.rol WHERE nombre = 'Administrador') THEN
        RAISE EXCEPTION 'No existe el rol Administrador. Ejecute primero gestion_turnos.sql';
    END IF;

    SELECT count(*) INTO v_permisos FROM seguridad.permiso;

    SELECT count(*) INTO v_admin
      FROM seguridad.rol_permiso rp
      JOIN seguridad.rol r USING (id_rol)
     WHERE r.nombre = 'Administrador';

    IF v_permisos <> 13 OR v_admin <> 13 THEN
        RAISE EXCEPTION 'Resultado inesperado: % permisos, % asignados al Administrador (se esperaban 13 y 13)',
            v_permisos, v_admin;
    END IF;

    RAISE NOTICE 'OK: 13 permisos cargados y asignados al rol Administrador';
END;
$$;

COMMIT;
