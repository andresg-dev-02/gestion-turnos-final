import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiResponse, ProblemDetails } from '../models';
import { API_BASE_URL } from '../tokens';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  get<T>(ruta: string, params?: Record<string, string | number | boolean>): Observable<T> {
    return this.http
      .get<ApiResponse<T>>(`${this.baseUrl}${ruta}`, { params: this.crearParams(params) })
      .pipe(map((res) => res.data as T));
  }

  post<T>(ruta: string, cuerpo: unknown): Observable<T> {
    return this.http
      .post<ApiResponse<T>>(`${this.baseUrl}${ruta}`, cuerpo)
      .pipe(map((res) => res.data as T));
  }

  put<T>(ruta: string, cuerpo: unknown): Observable<T> {
    return this.http
      .put<ApiResponse<T>>(`${this.baseUrl}${ruta}`, cuerpo)
      .pipe(map((res) => res.data as T));
  }

  delete<T>(ruta: string): Observable<T> {
    return this.http
      .delete<ApiResponse<T>>(`${this.baseUrl}${ruta}`)
      .pipe(map((res) => res.data as T));
  }

  static extraerMensajeError(err: unknown): { general: string; campos: Record<string, string> } {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 0) {
        return {
          general: 'No hay conexión con el servidor. Verifica tu señal de red.',
          campos: {},
        };
      }
      const detalle = err.error as ProblemDetails | undefined;
      const campos: Record<string, string> = {};
      if (detalle?.errors) {
        for (const [campo, mensajes] of Object.entries(detalle.errors)) {
          if (mensajes?.length) {
            campos[campo.charAt(0).toLowerCase() + campo.slice(1)] = mensajes[0];
          }
        }
      }
      return {
        general: detalle?.detail || detalle?.title || 'No fue posible procesar la solicitud.',
        campos,
      };
    }
    return { general: 'Ocurrió un error inesperado.', campos: {} };
  }

  private crearParams(params?: Record<string, string | number | boolean>): HttpParams {
    let httpParams = new HttpParams();
    if (!params) return httpParams;
    for (const [k, v] of Object.entries(params)) {
      if (v !== undefined && v !== null && v !== '') {
        httpParams = httpParams.set(k, String(v));
      }
    }
    return httpParams;
  }
}
