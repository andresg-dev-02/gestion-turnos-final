import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ConexionService } from '../../core';

@Component({
  selector: 'app-offline-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!conexion.enLinea()) {
      <div
        role="status"
        aria-live="polite"
        class="flex items-center justify-center gap-2 bg-amber-900 px-4 py-2 text-center text-xs font-semibold text-amber-50"
      >
        <span class="h-2 w-2 rounded-full bg-amber-300" aria-hidden="true"></span>
        <span>
          Sin conexión a internet · Estás viendo datos guardados en el dispositivo. El marcaje de
          asistencia requiere conexión en vivo.
        </span>
      </div>
    }
  `,
})
export class OfflineBannerComponent {
  protected readonly conexion = inject(ConexionService);
}
