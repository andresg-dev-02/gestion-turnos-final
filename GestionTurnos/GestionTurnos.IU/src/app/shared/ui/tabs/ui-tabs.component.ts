import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export interface PestanaItem {
  readonly id: string;
  readonly etiqueta: string;
  readonly contador?: number | null;
}

/**
 * Barra de pestañas accesible (role="tablist"). El contenido lo pinta el padre
 * según la pestaña activa.
 */
@Component({
  selector: 'ui-tabs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      role="tablist"
      [attr.aria-label]="etiqueta()"
      class="flex gap-1 overflow-x-auto rounded-xl border border-slate-200 bg-white p-1"
    >
      @for (p of pestanas(); track p.id) {
        <button
          type="button"
          role="tab"
          [id]="'tab-' + p.id"
          [attr.aria-selected]="p.id === activa()"
          (click)="cambio.emit(p.id)"
          class="inline-flex min-h-10 shrink-0 cursor-pointer items-center gap-2 rounded-lg px-3.5 py-2 text-sm font-semibold whitespace-nowrap transition"
          [class.bg-marca-600]="p.id === activa()"
          [class.text-white]="p.id === activa()"
          [class.text-slate-700]="p.id !== activa()"
          [class.hover:bg-slate-100]="p.id !== activa()"
        >
          {{ p.etiqueta }}
          @if (p.contador) {
            <span
              class="rounded-full px-2 py-0.5 text-xs font-bold"
              [class.bg-white]="p.id === activa()"
              [class.text-marca-700]="p.id === activa()"
              [class.bg-amber-100]="p.id !== activa()"
              [class.text-amber-900]="p.id !== activa()"
            >
              {{ p.contador }}
            </span>
          }
        </button>
      }
    </div>
  `,
})
export class UiTabsComponent {
  readonly etiqueta = input.required<string>();
  readonly pestanas = input.required<readonly PestanaItem[]>();
  readonly activa = input.required<string>();
  readonly cambio = output<string>();
}
