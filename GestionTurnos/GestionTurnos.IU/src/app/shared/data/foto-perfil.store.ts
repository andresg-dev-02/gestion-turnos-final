import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiClient, AuthStore } from '../../core';

export interface FotoPerfil {
  url: string | null;
  actualizadoEn: string | null;
}

/** Formatos y tamaño aceptados por el backend (mi-perfil/foto). */
export const FOTO_PERFIL_TIPOS = ['image/jpeg', 'image/png', 'image/webp'] as const;
export const FOTO_PERFIL_MAX_BYTES = 5 * 1024 * 1024;

/**
 * Foto de perfil del usuario en sesión (empleado o administrador).
 * Se carga sola al iniciar sesión y se limpia al cerrarla.
 */
@Injectable({ providedIn: 'root' })
export class FotoPerfilStore {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthStore);

  private readonly _url = signal<string | null>(null);
  readonly url = this._url.asReadonly();

  // Solo cambia al entrar o salir otro usuario, no en cada renovación del token.
  private readonly idUsuario = computed(() => this.auth.usuario()?.idUsuario ?? null);

  constructor() {
    effect(() => {
      const idUsuario = this.idUsuario();
      untracked(() => {
        this._url.set(null);
        if (idUsuario && this.auth.token()) {
          this.api.get<FotoPerfil>('/mi-perfil/foto').subscribe({
            next: (foto) => this._url.set(foto.url),
            error: () => this._url.set(null),
          });
        }
      });
    });
  }

  subir(archivo: File): Observable<FotoPerfil> {
    const formulario = new FormData();
    formulario.append('archivo', archivo, archivo.name);
    return this.api
      .put<FotoPerfil>('/mi-perfil/foto', formulario)
      .pipe(tap((foto) => this._url.set(foto.url)));
  }

  eliminar(): Observable<void> {
    return this.api.delete<void>('/mi-perfil/foto').pipe(tap(() => this._url.set(null)));
  }

  /** Mensaje de error si el archivo no cumple las reglas, o null si es válido. */
  static validar(archivo: File): string | null {
    if (!(FOTO_PERFIL_TIPOS as readonly string[]).includes(archivo.type)) {
      return 'Formato no permitido. Use una imagen JPG, PNG o WEBP.';
    }
    if (archivo.size > FOTO_PERFIL_MAX_BYTES) {
      return 'La imagen no puede superar 5 MB.';
    }
    return null;
  }
}
