import { InjectionToken } from '@angular/core';

export const ZONA_HORARIA_EMPRESA = 'America/Bogota';
export const LOCALE_EMPRESA = 'es-CO';

export interface ConfiguracionEntorno {
  readonly produccion: boolean;
  readonly zonaHoraria: string;
  readonly locale: string;
}

export const ENV_CONFIG = new InjectionToken<ConfiguracionEntorno>('ENV_CONFIG', {
  providedIn: 'root',
  factory: () => ({
    produccion: false,
    zonaHoraria: ZONA_HORARIA_EMPRESA,
    locale: LOCALE_EMPRESA,
  }),
});
