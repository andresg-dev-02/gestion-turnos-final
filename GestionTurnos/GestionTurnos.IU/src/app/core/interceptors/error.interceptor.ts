import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { AuthService, AuthStore, OMITIR_RENOVACION } from '../auth';
import { ApiClient, NotificationService } from '../services';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const store = inject(AuthStore);
  const auth = inject(AuthService);
  const notificaciones = inject(NotificationService);

  const sesionExpirada = (error: unknown): Observable<never> => {
    auth.finalizarSesionLocal();
    notificaciones.alerta('Tu sesión expiró. Ingresa nuevamente.');
    return throwError(() => error);
  };

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      const esRutaAuth = req.url.includes('/auth/');

      if (error.status === 401 && !esRutaAuth && !req.context.get(OMITIR_RENOVACION)) {
        // El access token venció: se renueva con el refresh token y se reintenta una sola vez.
        if (!store.refreshToken && !store.sincronizarDesdeAlmacenamiento()) {
          return sesionExpirada(error);
        }
        return auth.renovarSesion().pipe(
          catchError(() => sesionExpirada(error)),
          switchMap((token) =>
            next(reintento(req, token)).pipe(
              catchError((errorReintento: unknown) =>
                errorReintento instanceof HttpErrorResponse && errorReintento.status === 401
                  ? sesionExpirada(errorReintento)
                  : throwError(() => errorReintento)
              )
            )
          )
        );
      }

      if (error.status === 403) {
        const { general } = ApiClient.extraerMensajeError(error);
        notificaciones.error(general || 'No tienes permiso para realizar esta acción.');
      } else if (error.status >= 500) {
        notificaciones.error('El servidor encontró un problema. Intenta de nuevo en unos momentos.');
      }
      return throwError(() => error);
    })
  );
};

function reintento(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({
    setHeaders: { Authorization: `Bearer ${token}` },
    context: req.context.set(OMITIR_RENOVACION, true),
  });
}
