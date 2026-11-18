import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'ui-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="status" [attr.aria-label]="etiquetaAria()" class="animate-pulse space-y-3">
      @for (fila of filasArray(); track $index) {
        <div class="h-12 w-full rounded-xl bg-slate-200"></div>
      }
      <span class="sr-only">{{ etiquetaAria() }}</span>
    </div>
  `,
})
export class UiSkeletonComponent {
  readonly filas = input(3);
  readonly etiquetaAria = input('Cargando información...');

  filasArray(): number[] {
    return Array.from({ length: this.filas() }, (_, i) => i);
  }
}
