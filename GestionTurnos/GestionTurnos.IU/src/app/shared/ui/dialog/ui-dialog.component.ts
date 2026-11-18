import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  input,
  output,
  viewChild,
} from '@angular/core';

/**
 * Diálogo modal accesible basado en el elemento nativo <dialog>
 * (gestiona foco, tecla Escape y fondo inerte del navegador).
 */
@Component({
  selector: 'ui-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog
      #dialogo
      [attr.aria-labelledby]="id() + '-titulo'"
      (cancel)="$event.preventDefault(); cerrar.emit()"
      class="m-auto w-[calc(100%-2rem)] rounded-2xl border border-slate-200 bg-white p-0 shadow-2xl backdrop:bg-slate-900/50"
      [class.max-w-lg]="ancho() === 'md'"
      [class.max-w-3xl]="ancho() === 'lg'"
    >
      <header class="flex items-start justify-between gap-4 border-b border-slate-200 px-5 py-4">
        <div>
          <h2 [id]="id() + '-titulo'" class="text-base font-bold text-slate-900">{{ titulo() }}</h2>
          @if (subtitulo()) {
            <p class="mt-0.5 text-xs text-slate-600">{{ subtitulo() }}</p>
          }
        </div>
        <button
          type="button"
          (click)="cerrar.emit()"
          aria-label="Cerrar"
          class="inline-flex h-9 w-9 shrink-0 cursor-pointer items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-900"
        >
          <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
            <path d="M18 6 6 18M6 6l12 12" />
          </svg>
        </button>
      </header>
      <div class="max-h-[70dvh] overflow-y-auto px-5 py-4">
        <ng-content />
      </div>
      <footer class="flex flex-wrap justify-end gap-2 border-t border-slate-200 bg-slate-50 px-5 py-3">
        <ng-content select="[acciones]" />
      </footer>
    </dialog>
  `,
})
export class UiDialogComponent {
  readonly id = input.required<string>();
  readonly titulo = input.required<string>();
  readonly subtitulo = input<string | null>(null);
  readonly abierto = input(false);
  readonly ancho = input<'md' | 'lg'>('md');
  readonly cerrar = output<void>();

  private readonly dialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogo');

  constructor() {
    effect(() => {
      const el = this.dialogo().nativeElement;
      if (this.abierto() && !el.open) {
        el.showModal();
      } else if (!this.abierto() && el.open) {
        el.close();
      }
    });
  }
}
