import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type VarianteBoton = 'primario' | 'secundario' | 'peligro' | 'fantasma';

const CLASES_VARIANTE: Record<VarianteBoton, string> = {
  primario:
    'bg-[#1D768A] text-white hover:bg-[#165e6e] shadow-sm focus-visible:outline-[#1D768A]',
  secundario:
    'bg-white text-[#113459] ring-1 ring-inset ring-slate-300 hover:bg-slate-50',
  peligro:
    'bg-peligro text-white hover:opacity-95 focus-visible:outline-peligro',
  fantasma:
    'text-slate-700 hover:bg-slate-100 hover:text-slate-900',
};

@Component({
  selector: 'ui-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [attr.type]="tipo()"
      [disabled]="deshabilitado() || cargando()"
      [attr.aria-busy]="cargando()"
      [class]="clases()"
    >
      @if (cargando()) {
        <span
          class="inline-block h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent"
          aria-hidden="true"
        ></span>
      }
      <ng-content />
    </button>
  `,
})
export class UiButtonComponent {
  readonly variante = input<VarianteBoton>('primario');
  readonly tipo = input<'button' | 'submit' | 'reset'>('button');
  readonly deshabilitado = input(false);
  readonly cargando = input(false);
  readonly anchoCompleto = input(false);

  readonly clases = computed(
    () =>
      `inline-flex min-h-11 items-center justify-center gap-2 rounded-lg px-5 py-2.5 text-sm font-semibold transition cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed ${
        this.anchoCompleto() ? 'w-full' : ''
      } ${CLASES_VARIANTE[this.variante()]}`
  );
}
