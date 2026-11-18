import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'ui-paginador',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (totalPaginas() > 1) {
      <nav aria-label="Paginación" class="flex items-center justify-between gap-3 pt-3 text-sm">
        <p class="text-slate-600">
          Página <strong>{{ pagina() }}</strong> de <strong>{{ totalPaginas() }}</strong>
          · {{ total() }} registros
        </p>
        <div class="flex gap-2">
          <button
            type="button"
            [disabled]="pagina() <= 1"
            (click)="cambio.emit(pagina() - 1)"
            class="min-h-10 cursor-pointer rounded-lg border border-slate-300 bg-white px-3 font-semibold text-slate-800 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
          >
            Anterior
          </button>
          <button
            type="button"
            [disabled]="pagina() >= totalPaginas()"
            (click)="cambio.emit(pagina() + 1)"
            class="min-h-10 cursor-pointer rounded-lg border border-slate-300 bg-white px-3 font-semibold text-slate-800 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
          >
            Siguiente
          </button>
        </div>
      </nav>
    }
  `,
})
export class UiPaginadorComponent {
  readonly pagina = input.required<number>();
  readonly tamano = input.required<number>();
  readonly total = input.required<number>();
  readonly cambio = output<number>();

  readonly totalPaginas = computed(() => Math.max(1, Math.ceil(this.total() / this.tamano())));
}
