import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

export interface OpcionSelect {
  readonly valor: string;
  readonly etiqueta: string;
}

@Component({
  selector: 'ui-notched-select',
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
        <select
          [id]="id()"
          [value]="valor()"
          [disabled]="deshabilitado()"
          [attr.aria-required]="obligatorio()"
          [attr.aria-invalid]="Boolean(error())"
          [attr.aria-describedby]="idDescritor()"
          (change)="alCambiar($event)"
          class="block min-h-12 w-full rounded-lg border bg-white px-3.5 py-3 text-sm text-slate-900 transition focus:border-marca-600 focus:outline-none"
          [class.border-peligro]="Boolean(error())"
          [class.border-slate-300]="!error()"
        >
          @for (op of opciones(); track op.valor) {
            <option [value]="op.valor" [selected]="op.valor === valor()">
              {{ op.etiqueta }}
            </option>
          }
        </select>
      </div>
      @if (error()) {
        <p [id]="id() + '-msg'" role="alert" class="mt-1.5 text-xs font-medium text-peligro">
          {{ error() }}
        </p>
      }
    </div>
  `,
})
export class UiNotchedSelectComponent {
  readonly id = input.required<string>();
  readonly etiqueta = input.required<string>();
  readonly opciones = input.required<readonly OpcionSelect[]>();
  readonly valor = input('');
  readonly error = input<string | null | undefined>(null);
  readonly obligatorio = input(false);
  readonly deshabilitado = input(false);

  readonly valorCambio = output<string>();

  protected readonly Boolean = Boolean;

  readonly idDescritor = computed(() => (this.error() ? `${this.id()}-msg` : null));

  alCambiar(evento: Event): void {
    const objetivo = evento.target as HTMLSelectElement;
    this.valorCambio.emit(objetivo.value);
  }
}
