import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { PwaUpdateService } from '../../core';

@Component({
  selector: 'app-pwa-update-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (pwa.actualizacionDisponible()) {
      <div
        role="region"
        aria-label="Actualización de aplicación disponible"
        class="flex flex-wrap items-center justify-between gap-3 bg-indigo-950 px-4 py-2.5 text-xs text-white"
      >
        <span class="font-medium">Hay una versión nueva disponible de Gestión de Turnos.</span>
        <div class="flex items-center gap-2">
          <button
            type="button"
            (click)="pwa.aplicarActualizacion()"
            class="min-h-11 rounded-lg bg-white px-3.5 py-1.5 font-bold text-indigo-950 hover:bg-indigo-100"
          >
            Actualizar ahora
          </button>
          <button
            type="button"
            (click)="pwa.posponerAvisoActualizacion()"
            class="min-h-11 rounded-lg px-3 py-1.5 text-indigo-200 hover:text-white"
          >
            Más tarde
          </button>
        </div>
      </div>
    }
  `,
})
export class PwaUpdateBannerComponent {
  protected readonly pwa = inject(PwaUpdateService);
}
