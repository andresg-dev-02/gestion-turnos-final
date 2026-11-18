import { ChangeDetectionStrategy, Component, inject, output, signal } from '@angular/core';
import {
  ApiClient,
  ConexionService,
  NotificationService,
  QrAsistenciaService,
  RelojServidorService,
  ResultadoMarcacionQr,
} from '../../../app/core';
import { UiButtonComponent } from '../../../app/shared/ui';
import { QrScannerDialogComponent } from './qr-scanner-dialog.component';

@Component({
  selector: 'app-marcaje-rapido-card',
  imports: [UiButtonComponent, QrScannerDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article
      aria-labelledby="titulo-marcaje"
      class="flex flex-col justify-between rounded-2xl border border-slate-200 bg-white p-6 shadow-xs"
    >
      <div>
        <div class="flex items-center justify-between">
          <h2 id="titulo-marcaje" class="text-base font-bold text-slate-900">
            Marcaje de asistencia
          </h2>
          @if (esPwa()) {
            <span class="inline-flex items-center gap-1 rounded-md bg-teal-50 px-2 py-0.5 text-[11px] font-semibold text-teal-700 border border-teal-200">
              <svg class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 18h.01M8 21h8a2 2 0 002-2V5a2 2 0 00-2-2H8a2 2 0 00-2 2v14a2 2 0 002 2z" />
              </svg>
              Modo PWA
            </span>
          }
        </div>
        <p class="mt-1 text-xs text-slate-600">
          Hora oficial del servidor:
          <strong class="font-mono text-slate-900">{{ reloj.horaBogota() }}</strong>
        </p>

        @if (ultimoMarcaje(); as ult) {
          <div
            role="status"
            class="mt-3 flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50/80 p-2.5 text-xs text-emerald-950"
          >
            <span class="inline-block h-2 w-2 rounded-full bg-emerald-500 shrink-0" aria-hidden="true"></span>
            <span>
              {{ ult.tipo === 'mi-entrada' ? 'Entrada' : 'Salida' }} registrada con éxito a las
              <strong class="font-mono font-semibold">{{ ult.hora }}</strong>
            </span>
          </div>
        }

        @if (!conexion.enLinea()) {
          <div
            role="alert"
            class="mt-3 rounded-xl border border-amber-300 bg-amber-50 p-3 text-xs font-medium text-amber-950"
          >
            Necesitas conexión para marcar. Si no puedes, pídele a tu supervisor que registre tu
            marcaje.
          </div>
        }
      </div>

      <!-- Acciones de Marcaje -->
      <div class="mt-5 space-y-3">
        <!-- Botón Principal: Escaneo de QR de Estación (Destacado en PWA / Móvil) -->
        <button
          type="button"
          (click)="mostrarScanner.set(true)"
          [disabled]="!conexion.enLinea()"
          class="flex w-full items-center justify-center gap-2.5 rounded-xl bg-linear-to-r from-teal-600 to-emerald-600 px-4 py-3 text-sm font-bold text-white shadow-md shadow-teal-700/15 hover:from-teal-700 hover:to-emerald-700 active:scale-[0.99] disabled:opacity-50 disabled:cursor-not-allowed transition-all cursor-pointer"
        >
          <svg class="h-5 w-5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M12 4v1m6 11h2m-6 0h-2v4m0-11v3m0 0h.01M12 12h4.01M16 20h4M4 12h4m12 0h.01M5 8h2a1 1 0 001-1V5a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1zm12 0h2a1 1 0 001-1V5a1 1 0 00-1-1h-2a1 1 0 00-1 1v2a1 1 0 001 1zM5 20h2a1 1 0 001-1v-2a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1z"
            />
          </svg>
          <span>Escanear QR de Estación</span>
          @if (esPwa()) {
            <span class="rounded-full bg-white/20 px-2 py-0.5 text-[10px] font-extrabold uppercase tracking-wide">
              Recomendado
            </span>
          }
        </button>

        <div class="relative flex items-center justify-center">
          <div class="border-t border-slate-200 w-full"></div>
          <span class="bg-white px-2 text-[10px] uppercase font-semibold text-slate-400 absolute">o marcaje web</span>
        </div>

        <!-- Botones secundarios de marcaje manual web -->
        <div class="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
          <ui-button
            variante="secundario"
            [anchoCompleto]="true"
            [deshabilitado]="!conexion.enLinea()"
            [cargando]="marcandoEntrada()"
            (click)="marcar('mi-entrada')"
          >
            Entrada web
          </ui-button>

          <ui-button
            variante="secundario"
            [anchoCompleto]="true"
            [deshabilitado]="!conexion.enLinea()"
            [cargando]="marcandoSalida()"
            (click)="marcar('mi-salida')"
          >
            Salida web
          </ui-button>
        </div>
      </div>

      <!-- Diálogo de Cámara Escáner QR -->
      @if (mostrarScanner()) {
        <app-qr-scanner-dialog
          (cerrado)="mostrarScanner.set(false)"
          (marcado)="onMarcadoQr($event)"
        />
      }
    </article>
  `,
})
export class MarcajeRapidoCardComponent {
  readonly marcado = output<'mi-entrada' | 'mi-salida'>();

  protected readonly conexion = inject(ConexionService);
  protected readonly reloj = inject(RelojServidorService);
  private readonly qrService = inject(QrAsistenciaService);
  private readonly api = inject(ApiClient);
  private readonly toast = inject(NotificationService);

  readonly marcandoEntrada = signal(false);
  readonly marcandoSalida = signal(false);
  readonly mostrarScanner = signal(false);
  readonly esPwa = signal(this.qrService.esModoPwa());
  readonly ultimoMarcaje = signal<{ tipo: 'mi-entrada' | 'mi-salida'; hora: string } | null>(null);

  marcar(tipo: 'mi-entrada' | 'mi-salida'): void {
    if (!this.conexion.enLinea()) return;
    const estadoCarga = tipo === 'mi-entrada' ? this.marcandoEntrada : this.marcandoSalida;
    estadoCarga.set(true);

    this.api.post(`/asistencia/${tipo}`, { observacion: null }).subscribe({
      next: () => {
        estadoCarga.set(false);
        this.ultimoMarcaje.set({ tipo, hora: this.reloj.horaBogota() });
        this.toast.exito(
          tipo === 'mi-entrada' ? 'Entrada registrada con éxito.' : 'Salida registrada con éxito.'
        );
        this.marcado.emit(tipo);
      },
      error: (err: unknown) => {
        estadoCarga.set(false);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  onMarcadoQr(res: ResultadoMarcacionQr): void {
    const tipo = res.tipoAccion === 'ENTRADA' ? 'mi-entrada' : 'mi-salida';
    this.ultimoMarcaje.set({ tipo, hora: this.reloj.horaBogota() });
    this.marcado.emit(tipo);
  }
}
