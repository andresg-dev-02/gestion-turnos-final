import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { AuthStore, sesionActivaGuard, soloEmpleadoGuard } from './core';
import { AppShellComponent } from './layout';

/**
 * Enrutador general recopilatorio.
 * Delega de forma diferida (lazy loading) las rutas de Autenticación, Empleado (/mi) y Administrador (/admin),
 * y captura rutas no reconocidas redirigiendo a la pantalla 404 personalizada.
 */
export const routes: Routes = [
  {
    path: 'ingresar',
    title: 'Ingresar · Gestión de Turnos',
    loadComponent: () =>
      import('../features/auth/login/login.page').then((m) => m.LoginPageComponent),
  },
  {
    path: 'registro',
    title: 'Solicitar Registro · Gestión de Turnos',
    loadComponent: () =>
      import('../features/auth/register/register.page').then((m) => m.RegisterPageComponent),
  },
  {
    path: '',
    component: AppShellComponent,
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: () => {
          const store = inject(AuthStore);
          return store.esAdmin() ? 'admin' : 'mi';
        },
      },
      {
        path: 'mi',
        canActivate: [sesionActivaGuard, soloEmpleadoGuard],
        loadChildren: () => import('../features/mi/mi.routes').then((m) => m.MI_ROUTES),
      },
      {
        path: 'admin',
        loadChildren: () => import('../features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
      {
        path: 'sin-permiso',
        title: 'Acceso Restringido · Gestión de Turnos',
        loadComponent: () =>
          import('../features/sin-permiso/sin-permiso.page').then((m) => m.SinPermisoPageComponent),
      },
    ],
  },
  {
    path: 'totem-qr',
    title: 'Tótem de Asistencia · Gestión de Turnos',
    loadComponent: () =>
      import('../features/totem/totem-qr.page').then((m) => m.TotemQrPageComponent),
  },
  {
    path: '404',
    title: 'Página no encontrada · Gestión de Turnos',
    loadComponent: () =>
      import('../features/not-found/not-found.page').then((m) => m.NotFoundPageComponent),
  },
  {
    path: '**',
    loadComponent: () =>
      import('../features/not-found/not-found.page').then((m) => m.NotFoundPageComponent),
  },
];
