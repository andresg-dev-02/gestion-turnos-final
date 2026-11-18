import { Injectable, signal } from '@angular/core';

export type TipoNotificacion = 'exito' | 'error' | 'alerta' | 'info';

export interface AvisoToast {
  readonly id: number;
  readonly tipo: TipoNotificacion;
  readonly titulo: string;
  readonly mensaje: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private secuencia = 0;
  private readonly _avisos = signal<readonly AvisoToast[]>([]);
  readonly avisos = this._avisos.asReadonly();

  mostrar(tipo: TipoNotificacion, titulo: string, mensaje: string, duracionMs = 5500): void {
    const id = ++this.secuencia;
    this._avisos.update((lista) => [...lista, { id, tipo, titulo, mensaje }]);
    if (duracionMs > 0 && typeof setTimeout !== 'undefined') {
      setTimeout(() => this.descartar(id), duracionMs);
    }
  }

  exito(mensaje: string, titulo = 'Operación exitosa'): void {
    this.mostrar('exito', titulo, mensaje);
  }

  error(mensaje: string, titulo = 'Atención'): void {
    this.mostrar('error', titulo, mensaje, 7000);
  }

  alerta(mensaje: string, titulo = 'Aviso'): void {
    this.mostrar('alerta', titulo, mensaje);
  }

  descartar(id: number): void {
    this._avisos.update((lista) => lista.filter((item) => item.id !== id));
  }
}
