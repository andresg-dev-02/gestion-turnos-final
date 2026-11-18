import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'ui-check',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label [for]="id()" class="flex min-h-11 cursor-pointer items-start gap-3 py-1">
      <input
        [id]="id()"
        type="checkbox"
        [checked]="marcado()"
        [disabled]="deshabilitado()"
        (change)="marcadoCambio.emit($any($event.target).checked)"
        class="mt-0.5 h-5 w-5 shrink-0 cursor-pointer rounded border-slate-400 accent-marca-600"
      />
      <span class="text-sm text-slate-800">
        <span class="font-semibold">{{ etiqueta() }}</span>
        @if (ayuda()) {
          <span class="block text-xs text-slate-600">{{ ayuda() }}</span>
        }
      </span>
    </label>
  `,
})
export class UiCheckComponent {
  readonly id = input.required<string>();
  readonly etiqueta = input.required<string>();
  readonly ayuda = input<string | null>(null);
  readonly marcado = input(false);
  readonly deshabilitado = input(false);
  readonly marcadoCambio = output<boolean>();
}
