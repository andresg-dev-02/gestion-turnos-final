import { HttpInterceptorFn } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class LoadingStateService {
  private readonly _peticionesActivas = signal(0);
  readonly cargando = computed(() => this._peticionesActivas() > 0);

  iniciar(): void {
    this._peticionesActivas.update((n) => n + 1);
  }

  finalizar(): void {
    this._peticionesActivas.update((n) => Math.max(0, n - 1));
  }
}

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  const estado = inject(LoadingStateService);
  estado.iniciar();
  return next(req).pipe(finalize(() => estado.finalizar()));
};
