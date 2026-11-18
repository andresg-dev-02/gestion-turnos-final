import { Injectable, signal } from '@angular/core';
import { LOCALE_EMPRESA, ZONA_HORARIA_EMPRESA } from '../tokens';

@Injectable({ providedIn: 'root' })
export class RelojServidorService {
  private readonly formateadorHora = new Intl.DateTimeFormat(LOCALE_EMPRESA, {
    timeZone: ZONA_HORARIA_EMPRESA,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  });

  private readonly formateadorFecha = new Intl.DateTimeFormat(LOCALE_EMPRESA, {
    timeZone: ZONA_HORARIA_EMPRESA,
    weekday: 'short',
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });

  private readonly _horaBogota = signal(this.formateadorHora.format(new Date()));
  private readonly _fechaBogota = signal(this.formateadorFecha.format(new Date()));

  readonly horaBogota = this._horaBogota.asReadonly();
  readonly fechaBogota = this._fechaBogota.asReadonly();

  constructor() {
    if (typeof setInterval !== 'undefined') {
      setInterval(() => {
        const ahora = new Date();
        this._horaBogota.set(this.formateadorHora.format(ahora));
        this._fechaBogota.set(this.formateadorFecha.format(ahora));
      }, 1000);
    }
  }
}
