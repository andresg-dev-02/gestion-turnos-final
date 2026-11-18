import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
  inject,
  output,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import jsQR from 'jsqr';
import { ApiClient, NotificationService, QrAsistenciaService, ResultadoMarcacionQr } from '../../../app/core';

@Component({
  selector: 'app-qr-scanner-dialog',
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-xs animate-in fade-in duration-200"
      role="dialog"
      aria-modal="true"
      aria-labelledby="titulo-scanner"
    >
      <div
        class="relative flex w-full max-w-md flex-col overflow-hidden rounded-3xl bg-slate-900 text-white shadow-2xl border border-slate-800"
      >
        <!-- Encabezado -->
        <div class="flex items-center justify-between border-b border-slate-800/80 px-5 py-4">
          <div class="flex items-center gap-2.5">
            <span class="flex h-8 w-8 items-center justify-center rounded-xl bg-teal-500/20 text-teal-400">
              <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  d="M12 4v1m6 11h2m-6 0h-2v4m0-11v3m0 0h.01M12 12h4.01M16 20h4M4 12h4m12 0h.01M5 8h2a1 1 0 001-1V5a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1zm12 0h2a1 1 0 001-1V5a1 1 0 00-1-1h-2a1 1 0 00-1 1v2a1 1 0 001 1zM5 20h2a1 1 0 001-1v-2a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1z"
                />
              </svg>
            </span>
            <div>
              <h2 id="titulo-scanner" class="text-sm font-bold text-white">Lector QR de Asistencia</h2>
              <p class="text-xs text-slate-400">Enfoca el código de la pantalla tótem</p>
            </div>
          </div>
          <button
            type="button"
            (click)="cerrar()"
            class="rounded-full p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white transition-colors"
            aria-label="Cerrar lector"
          >
            <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <!-- Área de Cámara y Visor -->
        <div class="relative aspect-4/3 w-full overflow-hidden bg-black flex items-center justify-center">
          <video
            #videoElement
            playsinline
            muted
            autoplay
            class="h-full w-full object-cover"
            [class.opacity-40]="procesando() || errorCamara()"
          ></video>
          <canvas #canvasElement class="hidden"></canvas>

          <!-- Retícula y Láser de Escaneo -->
          @if (!errorCamara() && !resultado() && !errorMarcacion()) {
            <div class="pointer-events-none absolute inset-0 flex items-center justify-center p-6">
              <div
                class="relative h-56 w-56 rounded-2xl border-2 transition-colors duration-200"
                [class.border-teal-400]="!procesando()"
                [class.border-emerald-400]="procesando()"
              >
                <!-- Esquinas de mira -->
                <div class="absolute -top-1 -left-1 h-5 w-5 border-t-4 border-l-4 border-teal-400 rounded-tl"></div>
                <div class="absolute -top-1 -right-1 h-5 w-5 border-t-4 border-r-4 border-teal-400 rounded-tr"></div>
                <div class="absolute -bottom-1 -left-1 h-5 w-5 border-b-4 border-l-4 border-teal-400 rounded-bl"></div>
                <div class="absolute -bottom-1 -right-1 h-5 w-5 border-b-4 border-r-4 border-teal-400 rounded-br"></div>

                <!-- Línea láser animada -->
                @if (!procesando()) {
                  <div
                    class="absolute inset-x-2 h-0.5 bg-gradient-to-r from-transparent via-teal-400 to-transparent shadow-[0_0_12px_#2dd4bf] animate-[scan_2s_ease-in-out_infinite]"
                  ></div>
                }
              </div>
            </div>
          }

          <!-- Estado de procesando -->
          @if (procesando()) {
            <div class="absolute inset-0 flex flex-col items-center justify-center bg-slate-950/70 backdrop-blur-xs gap-3">
              <div class="h-10 w-10 animate-spin rounded-full border-3 border-teal-500 border-t-transparent"></div>
              <p class="text-sm font-semibold text-white">Validando marcaje con el servidor...</p>
            </div>
          }

          <!-- Estado de Éxito: verde para entrada, azul petróleo para salida -->
          @if (resultado(); as res) {
            <div
              class="absolute inset-0 flex flex-col items-center justify-center gap-3 p-6 text-center animate-in zoom-in-95 duration-200"
              [class.bg-[#31BBA1]]="res.tipoAccion === 'ENTRADA'"
              [class.bg-[#1D768A]]="res.tipoAccion !== 'ENTRADA'"
              role="status"
            >
              <span class="flex h-20 w-20 items-center justify-center rounded-full bg-white/20 ring-8 ring-white/10">
                @if (res.tipoAccion === 'ENTRADA') {
                  <svg class="h-11 w-11" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M5 13l4 4L19 7" />
                  </svg>
                } @else {
                  <svg class="h-11 w-11" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.2" d="M15 12H3m12 0-4-4m4 4-4 4M14 4h5a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-5" />
                  </svg>
                }
              </span>
              <p class="text-2xl font-extrabold">
                {{ res.tipoAccion === 'ENTRADA' ? '¡Entrada registrada!' : '¡Salida registrada!' }}
              </p>
              <p class="font-mono text-4xl font-black tracking-tight">{{ horaLocal(res.hora) }}</p>
              <p class="text-sm font-semibold text-white/90">
                {{ res.tipoAccion === 'ENTRADA' ? 'Buen turno' : 'Hasta pronto' }} · Estación {{ res.codigoEstacion }}
              </p>
              @if (aviso(res.mensaje); as av) {
                <p class="mt-1 rounded-xl bg-white/15 px-3 py-2 text-xs font-medium">{{ av }}</p>
              }
            </div>
          }

          <!-- Error de marcación: se detiene el escaneo para no repetir el mismo error en bucle -->
          @if (errorMarcacion(); as errM) {
            <div class="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-slate-950/95 p-6 text-center" role="alert">
              <span class="flex h-12 w-12 items-center justify-center rounded-full bg-amber-500/20 text-amber-300">
                <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v4m0 4h.01M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                </svg>
              </span>
              <p class="text-sm font-semibold text-amber-100">{{ errM }}</p>
              <button
                type="button"
                (click)="reanudarEscaneo()"
                class="min-h-11 rounded-xl bg-slate-800 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700"
              >
                Escanear de nuevo
              </button>
            </div>
          }

          <!-- Error de Cámara -->
          @if (errorCamara(); as err) {
            <div class="absolute inset-0 flex flex-col items-center justify-center bg-slate-950/95 p-6 text-center gap-3">
              <span class="flex h-12 w-12 items-center justify-center rounded-full bg-rose-500/20 text-rose-400">
                <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                </svg>
              </span>
              <p class="text-sm font-semibold text-rose-300">{{ err }}</p>
              <button
                type="button"
                (click)="iniciarCamara()"
                class="rounded-xl bg-slate-800 px-4 py-2 text-xs font-semibold text-white hover:bg-slate-700"
              >
                Reintentar acceso a cámara
              </button>
            </div>
          }
        </div>

        <!-- Controles Inferiores -->
        <div class="flex items-center justify-between bg-slate-900 px-5 py-3 border-t border-slate-800">
          <div class="flex items-center gap-2">
            @if (tieneLinterna()) {
              <button
                type="button"
                (click)="toggleLinterna()"
                class="flex items-center gap-1.5 rounded-xl border border-slate-700 bg-slate-800/80 px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-slate-700"
                [class.text-amber-400]="linternaActiva()"
              >
                <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
                </svg>
                <span>{{ linternaActiva() ? 'Luz apagada' : 'Luz encendida' }}</span>
              </button>
            }

            @if (multiplesCamaras()) {
              <button
                type="button"
                (click)="cambiarCamara()"
                class="flex items-center gap-1.5 rounded-xl border border-slate-700 bg-slate-800/80 px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-slate-700"
              >
                <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                <span>Cambiar</span>
              </button>
            }
          </div>

          <button
            type="button"
            (click)="cerrar()"
            class="rounded-xl px-4 py-1.5 text-xs font-semibold text-slate-400 hover:text-white"
          >
            {{ resultado() ? 'Finalizar' : 'Cancelar' }}
          </button>
        </div>
      </div>
    </div>
  `,
  styles: `
    @keyframes scan {
      0%, 100% { top: 8%; opacity: 0.3; }
      50% { top: 90%; opacity: 1; }
    }
  `,
})
export class QrScannerDialogComponent implements OnInit, OnDestroy {
  readonly cerrado = output<void>();
  readonly marcado = output<ResultadoMarcacionQr>();

  @ViewChild('videoElement') private videoRef?: ElementRef<HTMLVideoElement>;
  @ViewChild('canvasElement') private canvasRef?: ElementRef<HTMLCanvasElement>;

  private readonly qrService = inject(QrAsistenciaService);
  private readonly toast = inject(NotificationService);

  readonly procesando = signal(false);
  readonly errorCamara = signal<string | null>(null);
  readonly resultado = signal<ResultadoMarcacionQr | null>(null);
  readonly errorMarcacion = signal<string | null>(null);
  readonly linternaActiva = signal(false);
  readonly tieneLinterna = signal(false);
  readonly multiplesCamaras = signal(false);

  private stream: MediaStream | null = null;
  private animFrameId: number | null = null;
  private escaneando = false;
  private trackVideo: MediaStreamTrack | null = null;
  private camaraFacing: 'environment' | 'user' = 'environment';
  private barcodeDetector: any = null;
  private audio: AudioContext | null = null;

  /** Hora local (Bogotá) de la marcación devuelta por el servidor. */
  horaLocal(iso: string): string {
    return new Date(iso).toLocaleTimeString('es-CO', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: true,
      timeZone: 'America/Bogota',
    });
  }

  /** Texto adicional del servidor (p. ej. una jornada anterior cerrada sin salida). */
  aviso(mensaje: string): string | null {
    const corte = mensaje.indexOf('!');
    const resto = corte >= 0 ? mensaje.slice(corte + 1).trim() : '';
    return resto || null;
  }

  reanudarEscaneo(): void {
    this.errorMarcacion.set(null);
    this.escaneando = true;
    this.iniciarBucleEscaneo();
  }

  ngOnInit(): void {
    // El audio se prepara dentro del toque que abrió el lector: iOS bloquea sonidos creados después.
    try {
      const AudioCtx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      this.audio = AudioCtx ? new AudioCtx() : null;
    } catch {
      this.audio = null;
    }
    if (typeof window !== 'undefined' && 'BarcodeDetector' in window) {
      try {
        this.barcodeDetector = new (window as any).BarcodeDetector({ formats: ['qr_code'] });
      } catch {
        this.barcodeDetector = null;
      }
    }
    this.iniciarCamara();
  }

  ngOnDestroy(): void {
    this.detenerCamara();
    void this.audio?.close().catch(() => undefined);
    if (this.temporizadorCierre !== null) clearTimeout(this.temporizadorCierre);
  }

  private temporizadorCierre: ReturnType<typeof setTimeout> | null = null;

  async iniciarCamara(): Promise<void> {
    this.detenerCamara();
    this.errorCamara.set(null);

    try {
      if (!navigator.mediaDevices?.getUserMedia) {
        this.errorCamara.set('Tu navegador o dispositivo no soporta acceso a la cámara.');
        return;
      }

      const dispositivos = await navigator.mediaDevices.enumerateDevices();
      const videoInputs = dispositivos.filter((d) => d.kind === 'videoinput');
      this.multiplesCamaras.set(videoInputs.length > 1);

      this.stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: { ideal: this.camaraFacing },
          width: { ideal: 1280 },
          height: { ideal: 720 },
        },
        audio: false,
      });

      this.trackVideo = this.stream.getVideoTracks()[0] || null;

      // Verificar linterna (torch)
      if (this.trackVideo && typeof (this.trackVideo as any).getCapabilities === 'function') {
        const capabilities = (this.trackVideo as any).getCapabilities();
        this.tieneLinterna.set(Boolean(capabilities?.torch));
      }

      setTimeout(() => {
        if (this.videoRef?.nativeElement && this.stream) {
          const video = this.videoRef.nativeElement;
          video.srcObject = this.stream;
          video.setAttribute('playsinline', 'true');
          video.play().then(() => {
            this.escaneando = true;
            this.iniciarBucleEscaneo();
          }).catch((e) => {
            this.errorCamara.set('No se pudo reproducir el video de la cámara: ' + (e?.message || e));
          });
        }
      }, 50);
    } catch (err: unknown) {
      const msg = (err as Error)?.message || 'Permiso de cámara denegado o no disponible.';
      this.errorCamara.set('Permiso de cámara requerido para leer el código QR de la estación.');
    }
  }

  private iniciarBucleEscaneo(): void {
    const loop = async () => {
      if (!this.escaneando || this.procesando() || this.resultado() || this.errorMarcacion()) {
        return;
      }

      const video = this.videoRef?.nativeElement;
      if (video && video.readyState === video.HAVE_ENOUGH_DATA) {
        try {
          let codigoEncontrado: string | null = null;

          // 1. Intento primario acelerado por hardware: BarcodeDetector
          if (this.barcodeDetector) {
            try {
              const barcodes = await this.barcodeDetector.detect(video);
              if (barcodes && barcodes.length > 0) {
                codigoEncontrado = barcodes[0].rawValue;
              }
            } catch {
              // Fallback a canvas si falla temporalmente el detector
            }
          }

          // 2. Fallback por software: jsQR con canvas
          if (!codigoEncontrado && this.canvasRef?.nativeElement) {
            const canvas = this.canvasRef.nativeElement;
            const ctx = canvas.getContext('2d', { willReadFrequently: true });
            if (ctx) {
              canvas.width = video.videoWidth;
              canvas.height = video.videoHeight;
              ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
              const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
              const code = jsQR(imgData.data, imgData.width, imgData.height, {
                inversionAttempts: 'dontInvert',
              });
              if (code && code.data) {
                codigoEncontrado = code.data;
              }
            }
          }

          if (codigoEncontrado && codigoEncontrado.startsWith('GTQR|v1|')) {
            this.procesarCodigoEscaneado(codigoEncontrado);
            return;
          }
        } catch {
          // Continuar escaneo
        }
      }

      this.animFrameId = requestAnimationFrame(loop);
    };

    this.animFrameId = requestAnimationFrame(loop);
  }

  private procesarCodigoEscaneado(token: string): void {
    // Detener la lectura de inmediato: un segundo cuadro con el mismo QR no debe generar otra marcación.
    this.escaneando = false;
    this.procesando.set(true);

    this.emitirBeepExito();
    this.vibrar([60, 40, 60]);

    void this.obtenerUbicacionSiPermitida().then((coordenadas) => {
      this.qrService.marcarPorQr(token, 'AUTO', coordenadas ?? undefined).subscribe({
        next: (res) => {
          this.procesando.set(false);
          this.resultado.set(res);
          this.marcado.emit(res);
          this.detenerCamara();

          // Confirmación visual y cierre automático para volver a la vista principal actualizada.
          const tieneAviso = this.aviso(res.mensaje) !== null;
          this.temporizadorCierre = setTimeout(() => this.cerrar(), tieneAviso ? 6000 : 2200);
        },
        error: (err: unknown) => {
          this.procesando.set(false);
          const http = err as { error?: { code?: string } };
          const mensaje = ApiClient.extraerMensajeError(err).general || 'El código QR no pudo ser validado.';
          this.vibrar([200]);

          // Un QR vencido se resuelve solo con el siguiente código de la pantalla: se sigue leyendo.
          if (http?.error?.code === 'Asistencia.QrInvalido') {
            this.toast.alerta(mensaje);
            setTimeout(() => {
              if (!this.resultado()) {
                this.escaneando = true;
                this.iniciarBucleEscaneo();
              }
            }, 1600);
            return;
          }

          // Otros errores (salida demasiado pronto, sin turno...) se muestran y esperan al usuario.
          this.errorMarcacion.set(mensaje);
        },
      });
    });
  }

  /** Ubicación para auditoría solo si el permiso ya fue concedido (no interrumpe con una solicitud nueva). */
  private async obtenerUbicacionSiPermitida(): Promise<{ latitud: number; longitud: number } | null> {
    try {
      if (!navigator.geolocation || !navigator.permissions?.query) return null;
      const permiso = await navigator.permissions.query({ name: 'geolocation' as PermissionName });
      if (permiso.state !== 'granted') return null;
      return await new Promise((resolve) => {
        navigator.geolocation.getCurrentPosition(
          (pos) => resolve({ latitud: pos.coords.latitude, longitud: pos.coords.longitude }),
          () => resolve(null),
          { enableHighAccuracy: false, timeout: 2500, maximumAge: 60000 }
        );
      });
    } catch {
      return null;
    }
  }

  private vibrar(patron: number[]): void {
    // iPhone no implementa la vibración web.
    try {
      if (typeof navigator !== 'undefined' && typeof navigator.vibrate === 'function') {
        navigator.vibrate(patron);
      }
    } catch {
      // Ignorado
    }
  }

  private emitirBeepExito(): void {
    try {
      const ctx = this.audio;
      if (!ctx) return;
      void ctx.resume().catch(() => undefined);
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(880, ctx.currentTime); // Nota A5
      gain.gain.setValueAtTime(0.2, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.18);

      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.18);
    } catch {
      // Audio no permitido por política de reproducción automática
    }
  }

  toggleLinterna(): void {
    if (!this.trackVideo || !this.tieneLinterna()) return;
    const nuevoEstado = !this.linternaActiva();
    try {
      (this.trackVideo as any).applyConstraints({
        advanced: [{ torch: nuevoEstado }],
      }).then(() => {
        this.linternaActiva.set(nuevoEstado);
      }).catch(() => {
        this.tieneLinterna.set(false);
      });
    } catch {
      this.tieneLinterna.set(false);
    }
  }

  cambiarCamara(): void {
    this.camaraFacing = this.camaraFacing === 'environment' ? 'user' : 'environment';
    this.iniciarCamara();
  }

  cerrar(): void {
    if (this.temporizadorCierre !== null) {
      clearTimeout(this.temporizadorCierre);
      this.temporizadorCierre = null;
    }
    this.detenerCamara();
    this.cerrado.emit();
  }

  private detenerCamara(): void {
    this.escaneando = false;
    if (this.animFrameId !== null) {
      cancelAnimationFrame(this.animFrameId);
      this.animFrameId = null;
    }
    if (this.stream) {
      this.stream.getTracks().forEach((t) => t.stop());
      this.stream = null;
    }
    this.trackVideo = null;
  }
}
