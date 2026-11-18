import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from './api.client';

export interface TokenQrEstacion {
  token: string;
  codigoEstacion: string;
  nombreEstacion: string;
  expiraEnSegundos: number;
  intervaloSegundos: number;
  timestampUnix: number;
}

export interface ResultadoMarcacionQr {
  idRegistro: number;
  tipoAccion: 'ENTRADA' | 'SALIDA';
  mensaje: string;
  hora: string;
  codigoEstacion: string;
}

@Injectable({
  providedIn: 'root',
})
export class QrAsistenciaService {
  private readonly api = inject(ApiClient);

  /**
   * Obtiene el token rotativo actual generado por la API para una estación.
   */
  obtenerTokenEstacion(codigoEstacion?: string): Observable<TokenQrEstacion> {
    const params = codigoEstacion ? `?estacion=${encodeURIComponent(codigoEstacion)}` : '';
    return this.api.get<TokenQrEstacion>(`/asistencia/estacion-qr/token${params}`);
  }

  /**
   * Registra la asistencia (Entrada, Salida o detección automática) enviando el token escaneado.
   */
  marcarPorQr(
    tokenQr: string,
    accion: 'AUTO' | 'ENTRADA' | 'SALIDA' = 'AUTO',
    coordenadas?: { latitud: number; longitud: number }
  ): Observable<ResultadoMarcacionQr> {
    return this.api.post<ResultadoMarcacionQr>('/asistencia/mi-marcar-qr', {
      tokenQr,
      accion,
      latitud: coordenadas?.latitud,
      longitud: coordenadas?.longitud,
    });
  }

  /**
   * Detecta si la aplicación se está ejecutando actualmente en modo PWA instalada / standalone.
   */
  esModoPwa(): boolean {
    if (typeof window === 'undefined') return false;
    const nav = window.navigator as unknown as { standalone?: boolean };
    const standaloneMatch = window.matchMedia('(display-mode: standalone)').matches;
    return Boolean(standaloneMatch || nav.standalone || document.referrer.includes('android-app://'));
  }
}
