import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NotificationService } from '../../../core';

@Component({
  selector: 'ui-toast-container',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      aria-live="polite"
      aria-atomic="true"
      class="pointer-events-none fixed top-16 right-4 z-50 flex w-full max-w-sm flex-col gap-2.5"
    >
      @for (aviso of servicio.avisos(); track aviso.id) {
        <div
          role="status"
          class="pointer-events-auto flex items-start justify-between gap-3 rounded-xl border bg-white p-4 shadow-lg"
          [class.border-emerald-600]="aviso.tipo === 'exito'"
          [class.border-red-600]="aviso.tipo === 'error'"
          [class.border-amber-600]="aviso.tipo === 'alerta'"
        >
          <div>
            <p class="text-sm font-bold text-slate-900">{{ aviso.titulo }}</p>
            <p class="mt-0.5 text-xs text-slate-700">{{ aviso.mensaje }}</p>
          </div>
          <button
            type="button"
            (click)="servicio.descartar(aviso.id)"
            aria-label="Cerrar notificación"
            class="inline-flex min-h-11 min-w-11 items-center justify-center rounded-lg text-slate-600 hover:bg-slate-100 hover:text-slate-900"
          >
            ✕
          </button>
        </div>
      }
    </div>
  `,
})
export class UiToastContainerComponent {
  protected readonly servicio = inject(NotificationService);
}
