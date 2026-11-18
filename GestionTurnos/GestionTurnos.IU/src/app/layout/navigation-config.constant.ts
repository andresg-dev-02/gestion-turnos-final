import { ItemNavegacion, PERMISOS } from '../core';

export const ITEMS_MENU_EMPLEADO: readonly ItemNavegacion[] = [
  { id: 'mi-inicio', etiqueta: 'Inicio', ruta: '/mi', icono: 'home', seccion: 'empleado' },
  {
    id: 'mi-calendario',
    etiqueta: 'Mi calendario',
    ruta: '/mi/calendario',
    icono: 'calendar',
    seccion: 'empleado',
  },
  {
    id: 'mi-asistencia',
    etiqueta: 'Marcar asistencia',
    ruta: '/mi/asistencia',
    icono: 'clock',
    seccion: 'empleado',
  },
  {
    id: 'mi-vacaciones',
    etiqueta: 'Mis vacaciones',
    ruta: '/mi/vacaciones',
    icono: 'sun',
    seccion: 'empleado',
  },
  {
    id: 'mi-novedades',
    etiqueta: 'Mis novedades',
    ruta: '/mi/novedades',
    icono: 'sliders',
    seccion: 'empleado',
  },
  {
    id: 'mi-cambios',
    etiqueta: 'Cambios de turno',
    ruta: '/mi/cambios-turno',
    icono: 'swap',
    seccion: 'empleado',
  },
  {
    id: 'mi-perfil',
    etiqueta: 'Mi perfil',
    ruta: '/mi/perfil',
    icono: 'user',
    seccion: 'empleado',
  },
];

export const ITEMS_MENU_ADMIN: readonly ItemNavegacion[] = [
  { id: 'adm-panel', etiqueta: 'Panel general', ruta: '/admin', icono: 'chart', seccion: 'admin' },
  {
    id: 'adm-solicitudes',
    etiqueta: 'Solicitudes',
    ruta: '/admin/solicitudes',
    icono: 'inbox',
    seccion: 'admin',
  },
  {
    id: 'adm-empleados',
    etiqueta: 'Empleados',
    ruta: '/admin/empleados',
    icono: 'users',
    permiso: PERMISOS.empleadosGestionar,
    seccion: 'admin',
  },
  {
    id: 'adm-programacion',
    etiqueta: 'Programación',
    ruta: '/admin/programacion',
    icono: 'calendar',
    permiso: PERMISOS.turnosProgramar,
    seccion: 'admin',
  },
  {
    id: 'adm-asistencia',
    etiqueta: 'Control asistencia',
    ruta: '/admin/asistencia',
    icono: 'clock',
    permiso: PERMISOS.asistenciaConsultar,
    seccion: 'admin',
  },
  {
    id: 'adm-configuracion',
    etiqueta: 'Configuración',
    ruta: '/admin/configuracion',
    icono: 'settings',
    permiso: PERMISOS.parametrosEditar,
    seccion: 'admin',
  },
  {
    id: 'adm-auditoria',
    etiqueta: 'Auditoría',
    ruta: '/admin/auditoria',
    icono: 'sliders',
    permiso: PERMISOS.auditoriaConsultar,
    seccion: 'admin',
  },
];

/**
 * Los 5 accesos del dock flotante inferior PWA para el rol Empleado.
 */
export const ITEMS_DOCK_MOVIL: readonly ItemNavegacion[] = [
  { id: 'dock-inicio', etiqueta: 'Inicio', ruta: '/mi', icono: 'home', seccion: 'empleado' },
  {
    id: 'dock-calendario',
    etiqueta: 'Turnos',
    ruta: '/mi/calendario',
    icono: 'calendar',
    seccion: 'empleado',
  },
  {
    id: 'dock-marcar',
    etiqueta: 'Marcar asistencia',
    ruta: '/mi/asistencia',
    icono: 'plus',
    seccion: 'empleado',
    destacadoCentral: true,
  },
  {
    id: 'dock-novedades',
    etiqueta: 'Novedades',
    ruta: '/mi/novedades',
    icono: 'sliders',
    seccion: 'empleado',
  },
  { id: 'dock-perfil', etiqueta: 'Mi perfil', ruta: '/mi/perfil', icono: 'user', seccion: 'empleado' },
];

/**
 * Los 5 accesos del dock flotante inferior PWA exclusivos para el rol Administrador.
 */
export const ITEMS_DOCK_ADMIN: readonly ItemNavegacion[] = [
  { id: 'dock-adm-panel', etiqueta: 'Panel', ruta: '/admin', icono: 'home', seccion: 'admin' },
  {
    id: 'dock-adm-solicitudes',
    etiqueta: 'Solicitudes',
    ruta: '/admin/solicitudes',
    icono: 'inbox',
    seccion: 'admin',
  },
  {
    id: 'dock-adm-programar',
    etiqueta: 'Programar',
    ruta: '/admin/programacion',
    icono: 'plus',
    seccion: 'admin',
    destacadoCentral: true,
  },
  {
    id: 'dock-adm-empleados',
    etiqueta: 'Empleados',
    ruta: '/admin/empleados',
    icono: 'users',
    seccion: 'admin',
  },
  {
    id: 'dock-adm-config',
    etiqueta: 'Ajustes',
    ruta: '/admin/configuracion',
    icono: 'settings',
    seccion: 'admin',
  },
];
