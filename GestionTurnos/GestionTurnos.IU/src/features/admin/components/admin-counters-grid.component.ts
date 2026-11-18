import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

export interface ContadorAdminItem {
  readonly etiqueta: string;
  readonly valor: number;
  readonly ruta: string;
  readonly query?: Record<string, string>;
}

@Component({
  selector: 'app-admin-counters-grid',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      @for (item of contadores(); track item.etiqueta) {
        <article class="flex flex-col justify-between rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
          <div>
            <h2 class="text-xs font-bold tracking-wider text-slate-600 uppercase">
              {{ item.etiqueta }}
            </h2>
            <p class="mt-2 text-3xl font-extrabold text-slate-900">{{ item.valor }}</p>
          </div>
          <div class="mt-4">
            <a
              [routerLink]="item.ruta"
              [queryParams]="item.query"
              class="inline-flex min-h-11 items-center text-xs font-bold text-marca-700 underline underline-offset-4 hover:text-marca-800"
            >
              Gestionar {{ item.etiqueta.toLowerCase() }}
            </a>
          </div>
        </article>
      }
    </div>
  `,
})
export class AdminCountersGridComponent {
  readonly contadores = input.required<readonly ContadorAdminItem[]>();
}
