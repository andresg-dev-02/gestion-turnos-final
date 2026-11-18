/**
 * Los 13 códigos oficiales de permiso en `seguridad.permiso`.
 * La autorización se realiza siempre por código de permiso, nunca por nombre de rol.
 */
export const PERMISOS = {
  usuariosGestionar: 'usuarios.gestionar',
  registrosAprobar: 'registros.aprobar',
  empleadosGestionar: 'empleados.gestionar',
  organizacionGestionar: 'organizacion.gestionar',
  turnosConfigurar: 'turnos.configurar',
  turnosProgramar: 'turnos.programar',
  asistenciaConsultar: 'asistencia.consultar',
  asistenciaRegistrarManual: 'asistencia.registrar_manual',
  vacacionesAprobar: 'vacaciones.aprobar',
  novedadesAprobar: 'novedades.aprobar',
  parametrosEditar: 'parametros.editar',
  reportesConsultar: 'reportes.consultar',
  auditoriaConsultar: 'auditoria.consultar',
} as const;

export type CodigoPermiso = (typeof PERMISOS)[keyof typeof PERMISOS];
