-- =====================================================================
--  EMPLEADO DE PRUEBA PARA VACACIONES (más de 1 año laborado)
--  Ingreso: 2025-03-03 → un período vacacional causado (2025-03-03 a 2026-03-02)
--  con 15 días hábiles (VACACIONES_DIAS_POR_ANIO).
--  Acceso: laura.gomez.pruebas@gestionturnos.local / Vacaciones2025*
--  Idempotente: puede ejecutarse varias veces.
-- =====================================================================
BEGIN;

INSERT INTO seguridad.usuario (correo, hash_contrasena, id_rol, cod_estado_usuario)
SELECT 'laura.gomez.pruebas@gestionturnos.local',
       extensions.crypt('Vacaciones2025*', extensions.gen_salt('bf', 11)),
       (SELECT id_rol FROM seguridad.rol WHERE nombre = 'Empleado'),
       'ACTIVO'
WHERE NOT EXISTS (SELECT 1 FROM seguridad.usuario
                  WHERE lower(correo) = 'laura.gomez.pruebas@gestionturnos.local');

INSERT INTO organizacion.empleado
  (id_usuario, cod_tipo_documento, numero_documento, nombres, apellidos, correo_contacto,
   telefono, direccion, id_area, id_cargo, cod_tipo_contrato, fecha_ingreso, cod_estado_empleado)
SELECT u.id_usuario, 'CC', '1019876543', 'Laura Valentina', 'Gómez Rincón',
       'laura.gomez.pruebas@gestionturnos.local', '3157654321', 'Carrera 15 # 82-40, Bogotá',
       (SELECT min(id_area) FROM organizacion.area), (SELECT min(id_cargo) FROM organizacion.cargo),
       'INDEFINIDO', DATE '2025-03-03', 'ACTIVO'
FROM seguridad.usuario u
WHERE lower(u.correo) = 'laura.gomez.pruebas@gestionturnos.local'
ON CONFLICT (cod_tipo_documento, numero_documento) DO NOTHING;

INSERT INTO novedades.periodo_vacacional (id_empleado, fecha_inicio, fecha_fin, dias_otorgados)
SELECT e.id_empleado, DATE '2025-03-03', DATE '2026-03-02', 15
FROM organizacion.empleado e
WHERE e.cod_tipo_documento = 'CC' AND e.numero_documento = '1019876543'
ON CONFLICT (id_empleado, fecha_inicio) DO NOTHING;

COMMIT;
