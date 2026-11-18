import { Injectable, computed, inject, signal } from '@angular/core';
import { LoginResponseDto, PERMISOS, UsuarioSesion } from '../models';
import { StorageService } from '../services';

const CLAVE_TOKEN = 'gt_access_token';
const CLAVE_USUARIO = 'gt_usuario_sesion';
const CLAVE_REFRESH = 'gt_refresh_token';

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly storage = inject(StorageService);

  private readonly _token = signal<string | null>(this.storage.obtener(CLAVE_TOKEN));
  private readonly _usuario = signal<UsuarioSesion | null>(this.leerUsuarioInicial());
  private _refreshToken: string | null = this.storage.obtener(CLAVE_REFRESH);
  private _recordar = this.storage.esPersistente(CLAVE_TOKEN);

  readonly token = this._token.asReadonly();
  readonly usuario = this._usuario.asReadonly();
  readonly autenticado = computed(() => Boolean(this._token() && this._usuario()));
  readonly permisos = computed(() => new Set<string>(this._usuario()?.permisos ?? []));
  readonly tieneAlgunPermisoAdmin = computed(() =>
    Object.values(PERMISOS).some((codigo) => this.permisos().has(codigo))
  );
  readonly esAdmin = computed(() => {
    const rol = this._usuario()?.rol?.toLowerCase() ?? '';
    return rol === 'administrador' || rol === 'admin' || this.tieneAlgunPermisoAdmin();
  });

  /** Refresh token vigente (no se expone como señal: no debe llegar a las plantillas). */
  get refreshToken(): string | null {
    return this._refreshToken;
  }

  /** La sesión se guardó con "Mantener la sesión iniciada". */
  get recordar(): boolean {
    return this._recordar;
  }

  puede(codigoPermiso: string): boolean {
    return this.permisos().has(codigoPermiso);
  }

  /**
   * Guarda la sesión. `recordar` decide si sobrevive al cierre de la app (localStorage);
   * en una renovación se omite y se conserva la elección del inicio de sesión.
   */
  establecerSesion(respuesta: LoginResponseDto, recordar = this._recordar): void {
    const sesion: UsuarioSesion = {
      idUsuario: respuesta.idUsuario,
      correo: respuesta.correo,
      rol: respuesta.rol,
      idEmpleado: respuesta.idEmpleado,
      nombreCompleto: respuesta.nombreCompleto || respuesta.correo,
      permisos: respuesta.permisos ?? [],
      expiraEn: respuesta.expiraEn,
    };
    this._recordar = recordar;
    this._refreshToken = respuesta.refreshToken ?? null;
    this._token.set(respuesta.accessToken);
    this._usuario.set(sesion);
    this.storage.guardar(CLAVE_TOKEN, respuesta.accessToken, recordar);
    this.storage.guardar(CLAVE_USUARIO, JSON.stringify(sesion), recordar);
    if (this._refreshToken) {
      this.storage.guardar(CLAVE_REFRESH, this._refreshToken, recordar);
    } else {
      this.storage.eliminar(CLAVE_REFRESH);
    }
  }

  /**
   * Otra pestaña pudo haber renovado la sesión: adopta lo guardado si difiere de lo que hay en memoria.
   * Devuelve true si el access token cambió.
   */
  sincronizarDesdeAlmacenamiento(): boolean {
    const token = this.storage.obtener(CLAVE_TOKEN);
    if (!token || token === this._token()) {
      return false;
    }
    this._token.set(token);
    this._refreshToken = this.storage.obtener(CLAVE_REFRESH);
    this._usuario.set(this.leerUsuarioInicial());
    return true;
  }

  limpiarSesion(): void {
    this._token.set(null);
    this._usuario.set(null);
    this._refreshToken = null;
    this.storage.eliminar(CLAVE_TOKEN);
    this.storage.eliminar(CLAVE_USUARIO);
    this.storage.eliminar(CLAVE_REFRESH);
  }

  private leerUsuarioInicial(): UsuarioSesion | null {
    const crudo = this.storage.obtener(CLAVE_USUARIO);
    if (!crudo) return null;
    try {
      return JSON.parse(crudo) as UsuarioSesion;
    } catch {
      return null;
    }
  }
}
