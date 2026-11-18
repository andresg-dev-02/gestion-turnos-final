import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import QRCode from 'qrcode';
import { QrAsistenciaService, RelojServidorService, TokenQrEstacion } from '../../app/core';

@Component({
  selector: 'app-totem-qr-page',
  imports: [CommonModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen lg:h-screen lg:max-h-screen w-full bg-[#f8fafc] text-slate-800 flex flex-col justify-between select-none overflow-x-hidden lg:overflow-hidden">
      
      <!-- 1. Encabezado Institucional Blanco Proporcionado para TV -->
      <header class="bg-white border-b border-slate-200 shadow-xs shrink-0 relative z-10">
        <div class="px-5 sm:px-8 xl:px-12 py-3.5 xl:py-4 flex flex-col md:flex-row items-center justify-between gap-4">
          <!-- Logo y Título -->
          <div class="flex items-center gap-4 xl:gap-5">
            <img
              src="/Logo_APP.png"
              alt="Logo Gestión de Turnos"
              class="h-14 w-14 sm:h-16 sm:w-16 xl:h-20 xl:w-20 object-contain shrink-0 drop-shadow-xs"
            />
            <div class="h-12 xl:h-14 w-px bg-slate-200 hidden sm:block"></div>
            <div>
              <p class="text-xs sm:text-sm xl:text-base font-extrabold tracking-wider text-[#1D768A] uppercase">
                GESTIÓN DE TURNOS · CONTROL DE ACCESO
              </p>
              <h1 class="text-xl sm:text-2xl xl:text-3xl font-black tracking-tight text-[#113459] uppercase leading-tight">
                REGISTRO DE ASISTENCIA DE PERSONAL
              </h1>
            </div>
          </div>

          <!-- Reloj Digital y Controles -->
          <div class="flex items-center gap-4 sm:gap-6 xl:gap-8">
            <div class="flex items-center gap-2.5 rounded-2xl bg-slate-50 border border-slate-200 px-3.5 xl:px-5 py-2 xl:py-2.5 text-xs sm:text-sm xl:text-base font-bold text-slate-700 shadow-2xs">
              <span class="h-3 w-3 rounded-full bg-emerald-500 animate-pulse"></span>
              <span>En línea</span>
              <span class="text-slate-300">|</span>
              <span class="text-[#113459] font-extrabold">{{ datosToken()?.nombreEstacion || 'Estación Principal' }}</span>
            </div>

            <div class="text-right">
              <p class="text-2xl sm:text-3xl xl:text-4xl font-mono font-black tracking-tight text-[#113459] leading-none">
                {{ horaActual() }}
              </p>
              <p class="text-xs sm:text-sm xl:text-base font-bold text-slate-500 capitalize mt-1">
                {{ fechaActual() }}
              </p>
            </div>

            <div class="flex items-center gap-2">
              <button
                type="button"
                (click)="togglePantallaCompleta()"
                class="rounded-2xl border border-slate-200 bg-white p-2.5 xl:p-3 text-slate-600 hover:bg-slate-100 hover:text-[#113459] transition-colors shadow-2xs cursor-pointer"
                [title]="enPantallaCompleta() ? 'Salir de pantalla completa' : 'Pantalla completa'"
                aria-label="Pantalla completa"
              >
                <svg class="h-6 w-6 xl:h-7 xl:w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  @if (enPantallaCompleta()) {
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 9L4 4m0 0l5 0m-5 0l0 5m11-5l5 0m0 0l0 5m0-5l-5 5m5 11l0-5m0 5l-5 0m5 0l-5-5m-11 5l0-5m0 5l5 0m-5 0l5-5" />
                  } @else {
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 8V4m0 0h4M4 4l5 5m11-5h-4m4 0v4m0-4l-5 5M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4" />
                  }
                </svg>
              </button>
              <a
                routerLink="/admin/asistencia"
                class="rounded-2xl border border-slate-200 bg-white p-2.5 xl:p-3 text-slate-600 hover:bg-slate-100 hover:text-[#113459] transition-colors shadow-2xs cursor-pointer"
                title="Volver al panel administrativo"
                aria-label="Volver al panel"
              >
                <svg class="h-6 w-6 xl:h-7 xl:w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
                </svg>
              </a>
            </div>
          </div>
        </div>
        <!-- Línea decorativa de marca -->
        <div class="h-1.5 w-full bg-linear-to-r from-[#113459] via-[#1D768A] to-[#31BBA1]"></div>
      </header>

      <!-- 2. Área Central Hero: Código QR Grande en el Centro para TV -->
      <main class="flex-1 w-full px-4 sm:px-6 xl:px-8 py-4 sm:py-6 flex flex-col items-center justify-center overflow-hidden">
        
        <div
          class="rounded-3xl xl:rounded-4xl bg-white border-2 xl:border-3 transition-all duration-300 shadow-md p-6 sm:p-8 xl:p-10 flex flex-col items-center justify-center text-center relative max-w-2xl xl:max-w-3xl w-full"
          [class.border-[#1D768A]]="segundosRestantes() > 3"
          [class.border-amber-400]="segundosRestantes() <= 3"
        >
          <!-- Encabezado de la Tarjeta Hero -->
          <div class="flex flex-col items-center">
            <span class="flex h-12 w-12 xl:h-14 xl:w-14 items-center justify-center rounded-2xl bg-[#f0fdfa] text-[#1D768A] border border-[#ccfbf1] mb-2 shadow-xs">
              <svg class="h-6 w-6 xl:h-8 xl:w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </span>
            <p class="text-xs sm:text-sm xl:text-base font-extrabold tracking-widest text-[#1D768A] uppercase">
              REGISTRO DE ENTRADA Y SALIDA
            </p>
            <h2 class="text-2xl sm:text-3xl xl:text-4xl font-black text-[#113459] tracking-tight uppercase mt-0.5 leading-tight">
              EN ESPERA DE MARCAJE
            </h2>
            <p class="text-xs sm:text-sm xl:text-base text-slate-500 font-medium mt-1">
              Acerque su dispositivo y escanee el código para confirmar su presencia física
            </p>
          </div>

          <!-- Contenedor del Código QR Grande con Logo Oficial en el Centro -->
          <div class="my-4 xl:my-6 relative flex flex-col items-center">
            <div class="relative p-3.5 xl:p-5 rounded-3xl bg-white border-2 border-slate-200 shadow-lg">
              <!-- Canvas QR generado a alta resolución para pantallas grandes/TV -->
              <canvas
                #qrCanvas
                class="h-60 w-60 sm:h-72 sm:w-72 md:h-80 md:w-80 xl:h-[340px] xl:w-[340px] 2xl:h-[380px] 2xl:w-[380px] rounded-2xl"
              ></canvas>

              <!-- Logo oficial de la aplicación en el centro exacto del código QR -->
              <div class="absolute inset-0 flex items-center justify-center pointer-events-none">
                <div class="flex h-18 w-18 sm:h-20 sm:w-20 xl:h-24 xl:w-24 items-center justify-center rounded-2xl bg-white p-2.5 shadow-2xl border-2 border-slate-200 ring-4 ring-[#1D768A]/25">
                  <img
                    src="/Logo_APP.png"
                    alt="Logo Gestión de Turnos"
                    class="h-full w-full object-contain"
                  />
                </div>
              </div>
            </div>
          </div>

          <!-- Barra de Temporizador y Vigencia -->
          <div class="w-full max-w-lg rounded-2xl xl:rounded-3xl bg-slate-50 border border-slate-200 p-3.5 xl:p-4 flex items-center justify-between gap-4 shadow-2xs">
            <div class="flex items-center gap-3.5">
              <!-- Anillo circular SVG -->
              <div class="relative flex h-12 w-12 xl:h-14 xl:w-14 shrink-0 items-center justify-center">
                <svg class="h-full w-full -rotate-90 transform" viewBox="0 0 36 36">
                  <path
                    class="text-slate-200"
                    stroke-width="3.5"
                    stroke="currentColor"
                    fill="none"
                    d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                  />
                  <path
                    [class.text-[#1D768A]]="segundosRestantes() > 3"
                    [class.text-amber-500]="segundosRestantes() <= 3"
                    [style.strokeDasharray]="strokeDasharray()"
                    stroke-width="3.8"
                    stroke-linecap="round"
                    stroke="currentColor"
                    fill="none"
                    class="transition-all duration-1000 ease-linear"
                    d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                  />
                </svg>
                <span
                  class="absolute font-mono text-xs xl:text-sm font-black"
                  [class.text-[#113459]]="segundosRestantes() > 3"
                  [class.text-amber-600]="segundosRestantes() <= 3"
                >
                  {{ segundosRestantes() }}s
                </span>
              </div>

              <div class="text-left">
                <p class="text-xs sm:text-sm xl:text-base font-bold text-slate-800 leading-snug">
                  Código dinámico de un solo uso
                </p>
                <p class="text-[11px] xl:text-xs text-slate-500 font-medium">
                  Se refresca automáticamente cada {{ intervaloSegundos() }}s (protección contra fotos)
                </p>
              </div>
            </div>

            <span
              class="rounded-xl px-3.5 py-1 text-xs xl:text-sm font-black uppercase tracking-wider"
              [class.bg-[#f0fdfa]]="segundosRestantes() > 3"
              [class.text-[#1D768A]]="segundosRestantes() > 3"
              [class.border]="true"
              [class.border-[#99f6e4]]="segundosRestantes() > 3"
              [class.bg-amber-50]="segundosRestantes() <= 3"
              [class.text-amber-700]="segundosRestantes() <= 3"
              [class.border-amber-200]="segundosRestantes() <= 3"
            >
              {{ segundosRestantes() > 3 ? 'Válido' : 'Rotando' }}
            </span>
          </div>

        </div>
      </main>

      <!-- 3. Instrucciones Horizontales Abajo (3 Columnas Proporcionadas) -->
      <section class="w-full max-w-7xl mx-auto px-5 sm:px-8 xl:px-12 pb-4 xl:pb-6 shrink-0">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4 xl:gap-6">
          
          <div class="flex items-center gap-4 rounded-2xl bg-white border border-slate-200 p-4 xl:p-5 shadow-xs">
            <span class="flex h-11 w-11 xl:h-13 xl:w-13 shrink-0 items-center justify-center rounded-xl bg-[#113459] text-white font-black text-base xl:text-lg shadow-2xs">
              1
            </span>
            <div>
              <strong class="text-slate-900 block font-bold text-sm xl:text-base leading-snug">
                Abre la app en tu celular
              </strong>
              <span class="text-slate-600 text-xs xl:text-sm font-medium">
                Inicia sesión en la plataforma o abre la aplicación PWA instalada.
              </span>
            </div>
          </div>

          <div class="flex items-center gap-4 rounded-2xl bg-white border border-slate-200 p-4 xl:p-5 shadow-xs">
            <span class="flex h-11 w-11 xl:h-13 xl:w-13 shrink-0 items-center justify-center rounded-xl bg-[#1D768A] text-white font-black text-base xl:text-lg shadow-2xs">
              2
            </span>
            <div>
              <strong class="text-slate-900 block font-bold text-sm xl:text-base leading-snug">
                Toca "Escanear QR de Estación"
              </strong>
              <span class="text-slate-600 text-xs xl:text-sm font-medium">
                Encuentra el botón destacado en tu tarjeta de asistencia principal.
              </span>
            </div>
          </div>

          <div class="flex items-center gap-4 rounded-2xl bg-white border border-slate-200 p-4 xl:p-5 shadow-xs">
            <span class="flex h-11 w-11 xl:h-13 xl:w-13 shrink-0 items-center justify-center rounded-xl bg-[#31BBA1] text-slate-950 font-black text-base xl:text-lg shadow-2xs">
              3
            </span>
            <div>
              <strong class="text-slate-900 block font-bold text-sm xl:text-base leading-snug">
                Apunta tu cámara aquí
              </strong>
              <span class="text-slate-600 text-xs xl:text-sm font-medium">
                Tu entrada o salida se registrará y validará al instante sin contacto.
              </span>
            </div>
          </div>

        </div>
      </section>

      <!-- 4. Franja Inferior Institucional para TV (Legible a Distancia) -->
      <footer class="bg-[#113459] text-white py-3.5 xl:py-4 px-6 xl:px-12 shadow-md shrink-0 flex items-center justify-center gap-3.5 text-center sm:text-left">
        <span class="flex h-8 w-8 xl:h-10 xl:w-10 shrink-0 items-center justify-center rounded-xl bg-white/10 text-[#5eead4]">
          <svg class="h-5 w-5 xl:h-6 xl:w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v1m6 11h2m-6 0h-2v4m0-11v3m0 0h.01M12 12h4.01M16 20h4M4 12h4m12 0h.01M5 8h2a1 1 0 001-1V5a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1zm12 0h2a1 1 0 001-1V5a1 1 0 00-1-1h-2a1 1 0 00-1 1v2a1 1 0 001 1zM5 20h2a1 1 0 001-1v-2a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1z" />
          </svg>
        </span>
        <p class="text-xs sm:text-sm xl:text-base font-medium tracking-wide text-slate-100 max-w-5xl">
          Tenga a la mano su dispositivo móvil con la aplicación abierta para registrar su asistencia. Ante cualquier inconsistencia con su turno asignado, consulte a su supervisor o al área de talento humano.
        </p>
      </footer>
    </div>
  `,
})
export class TotemQrPageComponent implements OnInit, OnDestroy {
  @ViewChild('qrCanvas') private qrCanvasRef?: ElementRef<HTMLCanvasElement>;

  private readonly qrService = inject(QrAsistenciaService);
  private readonly reloj = inject(RelojServidorService);

  readonly datosToken = signal<TokenQrEstacion | null>(null);
  readonly segundosRestantes = signal<number>(20);
  readonly intervaloSegundos = signal<number>(20);
  readonly enPantallaCompleta = signal<boolean>(false);

  readonly horaActual = signal<string>('');
  readonly fechaActual = signal<string>('');

  readonly strokeDasharray = computed(() => {
    const total = this.intervaloSegundos() || 20;
    const actual = this.segundosRestantes();
    const porcentaje = Math.max(0, Math.min(100, (actual / total) * 100));
    return `${porcentaje}, 100`;
  });

  private timerInterval: any = null;
  private relojInterval: any = null;
  private rotando = false;

  ngOnInit(): void {
    this.actualizarReloj();
    this.relojInterval = setInterval(() => this.actualizarReloj(), 1000);

    this.cargarToken();
    this.iniciarTemporizador();

    if (typeof document !== 'undefined') {
      document.addEventListener('fullscreenchange', this.onFullscreenChange);
    }
  }

  ngOnDestroy(): void {
    if (this.timerInterval) clearInterval(this.timerInterval);
    if (this.relojInterval) clearInterval(this.relojInterval);
    if (typeof document !== 'undefined') {
      document.removeEventListener('fullscreenchange', this.onFullscreenChange);
    }
  }

  private onFullscreenChange = (): void => {
    this.enPantallaCompleta.set(Boolean(document.fullscreenElement));
  };

  private actualizarReloj(): void {
    const ahora = new Date();
    this.horaActual.set(
      ahora.toLocaleTimeString('es-CO', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: true,
      })
    );
    this.fechaActual.set(
      ahora.toLocaleDateString('es-CO', {
        weekday: 'long',
        day: 'numeric',
        month: 'long',
        year: 'numeric',
      })
    );
  }

  private iniciarTemporizador(): void {
    this.timerInterval = setInterval(() => {
      const rest = this.segundosRestantes();
      if (rest <= 1) {
        if (!this.rotando) {
          this.rotando = true;
          this.cargarToken();
        }
      } else {
        this.segundosRestantes.set(rest - 1);
      }
    }, 1000);
  }

  private cargarToken(): void {
    this.qrService.obtenerTokenEstacion().subscribe({
      next: (dto) => {
        this.rotando = false;
        this.datosToken.set(dto);
        this.segundosRestantes.set(dto.expiraEnSegundos);
        this.intervaloSegundos.set(dto.intervaloSegundos);
        this.renderizarQr(dto.token);
      },
      error: () => {
        this.rotando = false;
        // Reintentar en 3 segundos si la red titiló
        setTimeout(() => this.cargarToken(), 3000);
      },
    });
  }

  private renderizarQr(token: string): void {
    setTimeout(() => {
      if (!this.qrCanvasRef?.nativeElement) return;
      const canvas = this.qrCanvasRef.nativeElement;
      // Generación a alta resolución (480px nativo) para máxima nitidez en TV
      QRCode.toCanvas(canvas, token, {
        width: 480,
        margin: 1,
        color: {
          dark: '#113459', // Color Azul Marino oficial de la marca
          light: '#ffffff',
        },
        errorCorrectionLevel: 'H', // Nivel Alto (30% redundancia) para el logo central
      });
    }, 50);
  }

  togglePantallaCompleta(): void {
    if (typeof document === 'undefined') return;
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen().catch(() => {});
    } else {
      document.exitFullscreen().catch(() => {});
    }
  }
}
