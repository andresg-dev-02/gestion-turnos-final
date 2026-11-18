import { Routes } from '@angular/router';
import {
  PERMISOS,
  requiereAlgunPermisoAdminGuard,
  requierePermisoGuard,
  sesionActivaGuard,
} from '../../app/core';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    title: 'Panel Administrativo · Gestión de Turnos',
    canActivate: [sesionActivaGuard, requiereAlgunPermisoAdminGuard],
    loadComponent: () =>
      import('./panel/admin-panel.page').then((m) => m.AdminPanelPageComponent),
  },
  {
    path: 'solicitudes',
    title: 'Solicitudes · Gestión de Turnos',
    canActivate: [sesionActivaGuard, requiereAlgunPermisoAdminGuard],
    loadComponent: () =>
      import('./solicitudes/solicitudes.page').then((m) => m.SolicitudesPageComponent),
  },
  {
    // Ruta anterior: se conserva para enlaces guardados.
    path: 'registros',
    redirectTo: 'solicitudes?tipo=registro',
  },
  {
    path: 'empleados',
    title: 'Gestión de Empleados · Gestión de Turnos',
    canActivate: [sesionActivaGuard, requierePermisoGuard(PERMISOS.empleadosGestionar)],
    loadComponent: () =>
      import('./empleados/empleados.page').then((m) => m.EmpleadosPageComponent),
  },
  {
    path: 'programacion',
    title: 'Programación de Turnos · Gestión de Turnos',
    canActivate: [sesionActivaGuard, requierePermisoGuard(PERMISOS.turnosProgramar)],
    loadComponent: () =>
      import('./programacion/programacion.page').then((m) => m.ProgramacionPageComponent),
  },
  {
    path: 'configuracion',
    title: 'Configuración · Gestión de Turnos',
    canActivate: [sesionActivaGuard, requierePermisoGuard(PERMISOS.parametrosEditar)],
    loadComponent: () =>
      import('./configuracion/configuracion.page').then((m) => m.ConfiguracionPageComponent),
  },
  {
    path: 'asistencia',
    title: 'Control de Asistencia · Gestión de Turnos',
    canActivate: [sesionActivaGuard, requierePermisoGuard(PERMISOS.asistenciaConsultar)],
    loadComponent: () =>
      import('./asistencia/asistencia-admin.page').then(
        (m) => m.AsistenciaAdminPageComponent
      ),
  },
  {
    path: 'auditoria',
    title: 'Bitácora de Auditoría · Gestión de Turnos',
    canActivate: [sesionActivaGuard, requierePermisoGuard(PERMISOS.auditoriaConsultar)],
    data: {
      titulo: 'Bitácora de auditoría',
      descripcion: 'Comparación detallada de campo, valor anterior y valor nuevo.',
    },
    loadComponent: () =>
      import('./auditoria/auditoria.page').then((m) => m.AuditoriaPageComponent),
  },
];
