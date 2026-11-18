/* ============================================================
   DATO SEMILLA: ADMINISTRADOR DE PRUEBAS
   PostgreSQL 14+ / Supabase - archivo en UTF-8 (sin BOM)
   Requiere: gestion_turnos_completo.sql ya ejecutado.

   Credenciales (SOLO PARA PRUEBAS):
     Correo:      admin@gestionturnos.local
     Contraseña:  Admin.Turnos2026*

   SQL plano: sin bloques DO, sin DECLARE y sin comentarios de
   línea, para que funcione igual al pegarlo en el SQL Editor
   de Supabase. Idempotente: puede ejecutarse varias veces.
   La verificación en dos pasos queda desactivada (sin fila en
   seguridad.usuario_dos_pasos).
   ============================================================ */

SET client_encoding = 'UTF8';

BEGIN;

/* 1. Rol Administrador */
INSERT INTO seguridad.rol (nombre, descripcion)
VALUES ('Administrador', 'Gestión general de la plataforma')
ON CONFLICT (nombre) DO UPDATE SET activo = true;

/* 2. Los 13 permisos del sistema */
INSERT INTO seguridad.permiso (codigo, descripcion) VALUES
  ('usuarios.gestionar',
   'Crear, activar y desactivar usuarios; administrar roles'),
  ('registros.aprobar',
   'Aprobar o rechazar solicitudes de registro'),
  ('empleados.gestionar',
   'Crear, actualizar, desactivar y reactivar empleados'),
  ('organizacion.gestionar',
   'Sucursales, áreas y cargos'),
  ('turnos.configurar',
   'Tipos de turno'),
  ('turnos.programar',
   'Asignar, modificar, anular y publicar turnos'),
  ('asistencia.consultar',
   'Ver la asistencia de todos los empleados'),
  ('asistencia.registrar_manual',
   'Registrar o corregir marcaciones'),
  ('vacaciones.aprobar',
   'Registrar días causados y resolver vacaciones'),
  ('novedades.aprobar',
   'Resolver y registrar permisos y ausencias'),
  ('parametros.editar',
   'Parámetros de negocio y catálogos'),
  ('reportes.consultar',
   'Calendario general y reportes'),
  ('auditoria.consultar',
   'Consultar la bitácora de auditoría')
ON CONFLICT (codigo) DO NOTHING;

/* 3. Asignar los 13 permisos al rol Administrador */
INSERT INTO seguridad.rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
  FROM seguridad.rol r
 CROSS JOIN seguridad.permiso p
 WHERE r.nombre = 'Administrador'
   AND p.codigo IN (
     'usuarios.gestionar', 'registros.aprobar',
     'empleados.gestionar', 'organizacion.gestionar',
     'turnos.configurar', 'turnos.programar',
     'asistencia.consultar', 'asistencia.registrar_manual',
     'vacaciones.aprobar', 'novedades.aprobar',
     'parametros.editar', 'reportes.consultar',
     'auditoria.consultar')
ON CONFLICT (id_rol, id_permiso) DO NOTHING;

/* 4. Sucursal, área y cargo */
INSERT INTO organizacion.sucursal (nombre, direccion, ciudad)
VALUES ('Sede Principal', 'Calle 10 # 5-23', 'Bogotá')
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO organizacion.area (id_sucursal, nombre)
SELECT s.id_sucursal, 'Administración'
  FROM organizacion.sucursal s
 WHERE s.nombre = 'Sede Principal'
ON CONFLICT (id_sucursal, nombre) DO NOTHING;

INSERT INTO organizacion.cargo (nombre)
VALUES ('Administrador del sistema')
ON CONFLICT (nombre) DO NOTHING;

/* 5. Usuario administrador (crea o restablece contraseña).
      Hash bcrypt, costo 11, de: Admin.Turnos2026*          */
INSERT INTO seguridad.usuario
  (correo, hash_contrasena, id_rol, cod_estado_usuario)
SELECT 'admin@gestionturnos.local',
       '$2a$11$45jexQwq2p47LttgVLoz7.VFhvy6x37ivRkeXmTjwg33nVQWT6uUi',
       r.id_rol,
       'ACTIVO'
  FROM seguridad.rol r
 WHERE r.nombre = 'Administrador'
ON CONFLICT ((lower(correo))) DO UPDATE
   SET hash_contrasena    = EXCLUDED.hash_contrasena,
       id_rol             = EXCLUDED.id_rol,
       cod_estado_usuario = 'ACTIVO';

/* 6. Empleado vinculado al usuario */
INSERT INTO organizacion.empleado
  (id_usuario, cod_tipo_documento, numero_documento,
   nombres, apellidos, correo_contacto, telefono, direccion,
   id_area, id_cargo, cod_tipo_contrato, fecha_ingreso,
   cod_estado_empleado)
SELECT u.id_usuario, 'CC', '1000000001',
       'Andrés Julián', 'Muñoz Peña',
       'admin@gestionturnos.local', '3001234567',
       'Calle 10 # 5-23, Bogotá',
       a.id_area, c.id_cargo, 'INDEFINIDO', DATE '2026-01-01',
       'ACTIVO'
  FROM seguridad.usuario u
  JOIN organizacion.sucursal s ON s.nombre = 'Sede Principal'
  JOIN organizacion.area a
    ON a.id_sucursal = s.id_sucursal
   AND a.nombre = 'Administración'
  JOIN organizacion.cargo c
    ON c.nombre = 'Administrador del sistema'
 WHERE lower(u.correo) = 'admin@gestionturnos.local'
   AND NOT EXISTS (
     SELECT 1 FROM organizacion.empleado e
      WHERE e.id_usuario = u.id_usuario)
ON CONFLICT (cod_tipo_documento, numero_documento) DO UPDATE
   SET id_usuario          = EXCLUDED.id_usuario,
       id_area             = EXCLUDED.id_area,
       id_cargo            = EXCLUDED.id_cargo,
       fecha_retiro        = NULL,
       cod_estado_empleado = 'ACTIVO';

/* Si el empleado ya existía vinculado, se asegura que esté activo */
UPDATE organizacion.empleado e
   SET cod_estado_empleado = 'ACTIVO',
       fecha_retiro        = NULL
  FROM seguridad.usuario u
 WHERE e.id_usuario = u.id_usuario
   AND lower(u.correo) = 'admin@gestionturnos.local'
   AND e.cod_estado_empleado <> 'ACTIVO';

COMMIT;

/* 7. Comprobación: debe mostrar 1 fila con permisos = 13 */
SELECT u.id_usuario,
       u.correo,
       r.nombre AS rol,
       e.nombres || ' ' || e.apellidos AS empleado,
       a.nombre AS area,
       s.nombre AS sucursal,
       (SELECT count(*)
          FROM seguridad.rol_permiso rp
         WHERE rp.id_rol = u.id_rol) AS permisos
  FROM seguridad.usuario u
  JOIN seguridad.rol r ON r.id_rol = u.id_rol
  JOIN organizacion.empleado e ON e.id_usuario = u.id_usuario
  JOIN organizacion.area a ON a.id_area = e.id_area
  JOIN organizacion.sucursal s ON s.id_sucursal = a.id_sucursal
 WHERE lower(u.correo) = 'admin@gestionturnos.local';
