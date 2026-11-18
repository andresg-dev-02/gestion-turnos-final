import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { PwaUpdateService } from '../../core';

const CLAVE_POSPUESTO = 'gt_pwa_aviso_pospuesto_hasta';
const DIAS_POSPONER = 7;

/**
 * Aviso para instalar la app en el celular. Aparece en cualquier pantalla (también en el login) cuando:
 * - es un teléfono, la app no está instalada y el usuario no lo pospuso en los últimos 7 días; y
 * - Android/Chrome ya ofreció la instalación (botón directo) o es iPhone/iPad (instrucciones paso a paso).
 */
@Component({
  selector: 'app-pwa-install-prompt',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (visible()) {
      <section
        role="dialog"
        aria-labelledby="pwa-instalar-titulo"
        class="fixed inset-x-0 bottom-0 z-[60] px-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]"
      >
        <div class="mx-auto max-w-md rounded-2xl border border-slate-200 bg-white p-4 shadow-2xl">
          <div class="flex items-start gap-3">
            <img src="/icons/icon-96x96.png" alt="" width="48" height="48" class="h-12 w-12 shrink-0 rounded-xl" />
            <div class="min-w-0 flex-1">
              <h2 id="pwa-instalar-titulo" class="text-base font-bold text-[#113459]">Instala Gestión de Turnos</h2>
              <p class="mt-0.5 text-sm text-slate-600">
                Ábrela desde tu pantalla de inicio como una app y marca tu asistencia más rápido.
              </p>
            </div>
          </div>

          @if (pwa.esIos) {
            <ol class="mt-3 space-y-2 rounded-xl bg-slate-50 p-3 text-sm text-slate-700">
              <li class="flex items-center gap-2">
                <span class="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-[#1D768A] text-xs font-bold text-white">1</span>
                <span>
                  Toca
                  <svg viewBox="0 0 24 24" class="mx-0.5 inline h-5 w-5 align-text-bottom text-[#1D768A]" fill="none" stroke="currentColor" stroke-width="2" aria-label="Compartir">
                    <path d="M12 3v12M8 7l4-4 4 4M5 12v7a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-7" />
                  </svg>
                  <strong>Compartir</strong> en la barra del navegador.
                </span>
              </li>
              <li class="flex items-center gap-2">
                <span class="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-[#1D768A] text-xs font-bold text-white">2</span>
                <span>Elige <strong>Agregar a inicio</strong> y confirma con <strong>Agregar</strong>.</span>
              </li>
            </ol>
            <p class="mt-2 text-xs text-slate-500">
              Al abrir la app instalada tendrás que ingresar una vez más: iPhone no comparte la sesión de Safari.
            </p>
          }

          <div class="mt-3 flex justify-end gap-2">
            <button
              type="button"
              (click)="posponer()"
              class="min-h-11 rounded-lg px-4 text-sm font-semibold text-slate-700 hover:bg-slate-100"
            >
              {{ pwa.esIos ? 'Entendido' : 'Ahora no' }}
            </button>
            @if (!pwa.esIos) {
              <button
                type="button"
                (click)="instalar()"
                class="min-h-11 rounded-lg bg-[#1D768A] px-4 text-sm font-bold text-white shadow-sm hover:bg-[#165e6e]"
              >
                Instalar app
              </button>
            }
          </div>
        </div>
      </section>
    }
  `,
})
export class PwaInstallPromptComponent {
  protected readonly pwa = inject(PwaUpdateService);
  private readonly pospuesto = signal(PwaInstallPromptComponent.leerPospuesto());

  protected readonly visible = computed(
    () =>
      this.pwa.esMovil &&
      !this.pwa.instalada() &&
      !this.pospuesto() &&
      (this.pwa.instalable() || this.pwa.esIos)
  );

  protected async instalar(): Promise<void> {
    await this.pwa.instalarPwa();
  }

  protected posponer(): void {
    this.pospuesto.set(true);
    try {
      localStorage.setItem(CLAVE_POSPUESTO, String(Date.now() + DIAS_POSPONER * 24 * 60 * 60 * 1000));
    } catch {
      // Sin almacenamiento: solo se oculta por esta visita.
    }
  }

  private static leerPospuesto(): boolean {
    try {
      const hasta = Number(localStorage.getItem(CLAVE_POSPUESTO));
      return Number.isFinite(hasta) && hasta > Date.now();
    } catch {
      return false;
    }
  }
}
