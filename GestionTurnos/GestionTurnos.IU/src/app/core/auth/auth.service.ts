import { HttpClient, HttpContext, HttpContextToken } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap, throwError } from 'rxjs';
import { ApiResponse } from '../models';
import { API_BASE_URL } from '../tokens';
import {
  LoginRequest,
  LoginResponseDto,
  SolicitarRegistroRequest,
  TipoDocumentoDto,
} from '../models';
import { ApiClient } from '../services';
import { AuthStore } from './auth.store';

const TIPOS_DOC_RESPALDO: readonly TipoDocumentoDto[] = [
  { codTipoDocumento: 'CC', nombre: 'Cédula de Ciudadanía', activo: true },
  { codTipoDocumento: 'CE', nombre: 'Cédula de Extranjería', activo: true },
  { codTipoDocumento: 'PA', nombre: 'Pasaporte', activo: true },
  { codTipoDocumento: 'PPT', nombre: 'Permiso por protección temporal', activo: true },
];

/** Marca solicitudes que no deben disparar la renovación automática de sesión (evita bucles). */
export const OMITIR_RENOVACION = new HttpContextToken<boolean>(() => false);

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly store = inject(AuthStore);
  private readonly router = inject(Router);

  /** Renovación en curso: varias solicitudes con 401 a la vez comparten una sola llamada. */
  private renovacionEnCurso: Observable<string> | null = null;

  iniciarSesion(credenciales: LoginRequest): Observable<LoginResponseDto> {
    const recordar = credenciales.recordar ?? false;
    return this.api
      .post<LoginResponseDto>('/auth/login', { ...credenciales, recordar })
      .pipe(tap((respuesta) => this.store.establecerSesion(respuesta, recordar)));
  }

  /**
   * Obtiene un access token nuevo con el refresh token (que también se reemplaza).
   * Emite el token nuevo o falla si la sesión ya no es válida.
   */
  renovarSesion(): Observable<string> {
    if (this.renovacionEnCurso) return this.renovacionEnCurso;

    // Otra pestaña pudo haber renovado ya: su token rotó el refresh que tenemos en memoria.
    if (this.store.sincronizarDesdeAlmacenamiento()) {
      return of(this.store.token()!);
    }

    const refreshToken = this.store.refreshToken;
    if (!refreshToken) {
      return throwError(() => new Error('Sin sesión renovable'));
    }

    this.renovacionEnCurso = this.http
      .post<ApiResponse<LoginResponseDto>>(
        `${this.baseUrl}/auth/renovar`,
        { refreshToken },
        { context: new HttpContext().set(OMITIR_RENOVACION, true) }
      )
      .pipe(
        map((res) => res.data as LoginResponseDto),
        tap((sesion) => this.store.establecerSesion(sesion)),
        map((sesion) => sesion.accessToken),
        finalize(() => (this.renovacionEnCurso = null)),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    return this.renovacionEnCurso;
  }

  solicitarRegistro(solicitud: SolicitarRegistroRequest): Observable<number> {
    return this.api.post<number>('/auth/solicitar-registro', solicitud);
  }

  listarTiposDocumento(): Observable<readonly TipoDocumentoDto[]> {
    return this.api
      .get<readonly { codigo: string; nombre: string; activo: boolean }[]>(
        '/parametrizacion/tipos-documento',
        { soloActivos: true }
      )
      .pipe(
        // La API responde CatalogoItemDto { codigo, nombre, activo }
        map((lista) =>
          lista.map((t) => ({ codTipoDocumento: t.codigo, nombre: t.nombre, activo: t.activo }))
        ),
        catchError(() => of(TIPOS_DOC_RESPALDO))
      );
  }

  cerrarSesion(): void {
    // Revoca la sesión del dispositivo en el servidor (sin esperar la respuesta).
    const refreshToken = this.store.refreshToken;
    if (refreshToken) {
      this.http
        .post(`${this.baseUrl}/auth/cerrar-sesion`, { refreshToken }, {
          context: new HttpContext().set(OMITIR_RENOVACION, true),
        })
        .subscribe({ error: () => undefined });
    }
    this.finalizarSesionLocal();
  }

  /** Limpia la sesión de este dispositivo y los datos del empleado guardados por el service worker. */
  finalizarSesionLocal(): void {
    this.store.limpiarSesion();
    void AuthService.borrarCacheApi();
    void this.router.navigate(['/ingresar']);
  }

  /**
   * El service worker guarda respuestas de la API (perfil, calendario...) para usarlas sin conexión.
   * Al cerrar sesión se borran para que otro usuario del mismo teléfono no las vea.
   */
  private static async borrarCacheApi(): Promise<void> {
    try {
      if (typeof caches === 'undefined') return;
      const claves = await caches.keys();
      await Promise.all(claves.filter((c) => c.includes(':data:')).map((c) => caches.delete(c)));
    } catch {
      // Sin acceso a Cache Storage: nada que borrar.
    }
  }

  rutaInicialSegunPermisos(): string {
    return this.store.esAdmin() ? '/admin' : '/mi';
  }
}
