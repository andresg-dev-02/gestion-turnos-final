import { CodigoPermiso } from './permisos.constant';

export type IconoNavegacion =
  | 'home'
  | 'calendar'
  | 'plus'
  | 'clock'
  | 'sun'
  | 'sliders'
  | 'user'
  | 'shield'
  | 'users'
  | 'chart'
  | 'settings'
  | 'inbox'
  | 'swap';

export interface ItemNavegacion {
  readonly id: string;
  readonly etiqueta: string;
  readonly ruta: string;
  readonly icono: IconoNavegacion;
  readonly permiso?: CodigoPermiso;
  readonly seccion: 'empleado' | 'admin';
  readonly destacadoCentral?: boolean;
}
