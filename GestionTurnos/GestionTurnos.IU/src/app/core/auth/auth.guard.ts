import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CodigoPermiso } from '../models';
import { AuthService } from './auth.service';
import { AuthStore } from './auth.store';

export const sesionActivaGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);
  return store.autenticado() ? true : router.createUrlTree(['/ingresar']);
};

export const invitadoGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const authService = inject(AuthService);
  const router = inject(Router);
  return store.autenticado()
    ? router.createUrlTree([authService.rutaInicialSegunPermisos()])
    : true;
};

export const requierePermisoGuard = (codigo: CodigoPermiso): CanActivateFn => {
  return () => {
    const store = inject(AuthStore);
    const router = inject(Router);
    if (!store.autenticado()) {
      return router.createUrlTree(['/ingresar']);
    }
    return store.puede(codigo) ? true : router.createUrlTree(['/sin-permiso']);
  };
};

export const requiereAlgunPermisoAdminGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);
  if (!store.autenticado()) {
    return router.createUrlTree(['/ingresar']);
  }
  return store.tieneAlgunPermisoAdmin() ? true : router.createUrlTree(['/sin-permiso']);
};

export const soloEmpleadoGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);
  if (!store.autenticado()) {
    return router.createUrlTree(['/ingresar']);
  }
  // Si es administrador, únicamente tiene acceso al panel administrativo
  if (store.esAdmin()) {
    return router.createUrlTree(['/admin']);
  }
  return true;
};
