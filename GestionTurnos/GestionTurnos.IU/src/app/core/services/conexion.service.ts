import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ConexionService {
  private readonly _enLinea = signal<boolean>(
    typeof navigator !== 'undefined' ? navigator.onLine : true
  );

  readonly enLinea = this._enLinea.asReadonly();

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('online', () => this._enLinea.set(true));
      window.addEventListener('offline', () => this._enLinea.set(false));
    }
  }
}
