import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'ui-notched-textarea',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block w-full pt-1.5',
  },
  template: `
    <div class="w-full">
      <div class="relative mt-1">
        <label
          [for]="id()"
          class="absolute -top-2.5 left-3 z-10 bg-white px-1.5 text-xs font-semibold text-slate-700"
        >
          {{ etiqueta() }}
          @if (obligatorio()) {
            <span class="text-peligro" aria-hidden="true">*</span>
          }
        </label>
        <textarea
          [id]="id()"
          [value]="valor()"
          [rows]="filas()"
          [attr.maxlength]="maximo()"
          [attr.aria-required]="obligatorio()"
          [attr.aria-invalid]="error() ? true : null"
          [attr.aria-describedby]="error() ? id() + '-msg' : null"
          (input)="valorCambio.emit($any($event.target).value)"
          class="block w-full rounded-lg border px-3.5 py-3 text-sm text-slate-900 transition focus:border-marca-600 focus:outline-none"
          [class.border-peligro]="error()"
          [class.border-slate-300]="!error()"
        ></textarea>
      </div>
      @if (error()) {
        <p [id]="id() + '-msg'" role="alert" class="mt-1.5 text-xs font-medium text-peligro">
          {{ error() }}
        </p>
      }
    </div>
  `,
})
export class UiNotchedTextareaComponent {
  readonly id = input.required<string>();
  readonly etiqueta = input.required<string>();
  readonly valor = input('');
  readonly filas = input(3);
  readonly maximo = input<number | null>(300);
  readonly obligatorio = input(false);
  readonly error = input<string | null | undefined>(null);
  readonly valorCambio = output<string>();
}
