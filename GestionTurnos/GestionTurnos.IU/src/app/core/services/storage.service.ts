import { Injectable } from '@angular/core';

/**
 * Almacenamiento de la sesión.
 * - persistente = true  → localStorage (sobrevive al cerrar la app o reiniciar el teléfono).
 * - persistente = false → sessionStorage (se borra al cerrar la pestaña; equipos compartidos).
 * La lectura busca primero en localStorage y luego en sessionStorage.
 */
@Injectable({ providedIn: 'root' })
export class StorageService {
  obtener(clave: string): string | null {
    return this.leer(() => localStorage, clave) ?? this.leer(() => sessionStorage, clave);
  }

  /** True si la clave está guardada en el almacenamiento persistente. */
  esPersistente(clave: string): boolean {
    return this.leer(() => localStorage, clave) !== null;
  }

  guardar(clave: string, valor: string, persistente = false): void {
    try {
      (persistente ? localStorage : sessionStorage).setItem(clave, valor);
      (persistente ? sessionStorage : localStorage).removeItem(clave);
    } catch {
      // Ignorado si el navegador bloquea el almacenamiento
    }
  }

  eliminar(clave: string): void {
    for (const almacen of [() => localStorage, () => sessionStorage]) {
      try {
        almacen().removeItem(clave);
      } catch {
        // Ignorado
      }
    }
  }

  private leer(almacen: () => Storage, clave: string): string | null {
    try {
      return almacen().getItem(clave);
    } catch {
      return null;
    }
  }
}
