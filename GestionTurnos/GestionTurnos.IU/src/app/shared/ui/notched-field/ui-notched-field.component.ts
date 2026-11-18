import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'ui-notched-field',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block w-full',
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
        <input
          [id]="id()"
          [type]="tipo()"
          [value]="valor()"
          [placeholder]="placeholder()"
          [disabled]="deshabilitado()"
          [attr.autocomplete]="autocomplete() || null"
          [attr.min]="min()"
          [attr.max]="max()"
          [attr.step]="paso()"
          [attr.aria-required]="obligatorio()"
          [attr.aria-invalid]=" Boolean(error())"
          [attr.aria-describedby]="idDescritor()"
          (input)="alEscribir($event)"
          class="block min-h-12 w-full rounded-lg border px-3.5 py-3 text-sm text-slate-900 placeholder:text-slate-500 transition focus:border-marca-600 focus:outline-none disabled:bg-slate-100 disabled:text-slate-600"
          [class.border-peligro]="Boolean(error())"
          [class.border-slate-300]="!error()"
        />
      </div>
      @if (error()) {
        <p [id]="id() + '-msg'" role="alert" class="mt-1.5 text-xs font-medium text-peligro">
          {{ error() }}
        </p>
      } @else if (ayuda()) {
        <p [id]="id() + '-msg'" class="mt-1.5 pb-1 text-xs text-slate-500 leading-normal">
          {{ ayuda() }}
        </p>
      }
    </div>
  `,
})
export class UiNotchedFieldComponent {
  readonly id = input.required<string>();
  readonly etiqueta = input.required<string>();
  readonly tipo = input<
    'text' | 'email' | 'password' | 'tel' | 'date' | 'number' | 'time' | 'datetime-local' | 'color'
  >('text');
  readonly min = input<string | null>(null);
  readonly max = input<string | null>(null);
  readonly paso = input<string | null>(null);
  readonly placeholder = input('');
  readonly valor = input('');
  readonly error = input<string | null | undefined>(null);
  readonly ayuda = input<string | null | undefined>(null);
  readonly obligatorio = input(false);
  readonly deshabilitado = input(false);
  readonly autocomplete = input('');

  readonly valorCambio = output<string>();

  protected readonly Boolean = Boolean;

  readonly idDescritor = computed(() =>
    this.error() || this.ayuda() ? `${this.id()}-msg` : null
  );

  alEscribir(evento: Event): void {
    const objetivo = evento.target as HTMLInputElement;
    this.valorCambio.emit(objetivo.value);
  }
}
