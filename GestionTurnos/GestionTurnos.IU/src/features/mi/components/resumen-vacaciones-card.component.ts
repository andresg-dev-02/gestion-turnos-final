import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { SaldoVacaciones } from '../../../app/shared/data/modelos';
import { MiApi } from '../../../app/shared/data/apis';
import { UiSkeletonComponent } from '../../../app/shared/ui';

/** Saldo de vacaciones (v_saldo_vacaciones) y solicitudes pendientes del empleado. */
@Component({
  selector: 'app-resumen-vacaciones-card',
  imports: [RouterLink, UiSkeletonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article aria-labelledby="titulo-vacaciones" class="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs">
      <h2 id="titulo-vacaciones" class="text-base font-bold text-slate-900">Vacaciones y solicitudes</h2>

      @if (cargando()) {
        <div class="mt-4"><ui-skeleton [filas]="1" /></div>
      } @else {
        <dl class="mt-4 grid grid-cols-2 gap-4">
          <div class="rounded-xl bg-slate-50 p-3.5">
            <dt class="text-xs font-medium text-slate-600">Días disponibles</dt>
            <dd class="mt-1 text-2xl font-extrabold text-slate-900">{{ saldo()?.diasDisponiblesParaNuevaSolicitud ?? 0 }}</dd>
          </div>
          <div class="rounded-xl bg-slate-50 p-3.5">
            <dt class="text-xs font-medium text-slate-600">Solicitudes pendientes</dt>
            <dd class="mt-1 text-2xl font-extrabold text-slate-900">{{ pendientes() }}</dd>
          </div>
        </dl>
      }

      <div class="mt-4 flex flex-wrap gap-3">
        <a routerLink="/mi/vacaciones" class="inline-flex min-h-11 items-center rounded-lg bg-marca-50 px-4 py-2 text-xs font-bold text-marca-700 hover:bg-marca-100">
          Solicitar vacaciones
        </a>
        <a routerLink="/mi/novedades" class="inline-flex min-h-11 items-center rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-800 hover:bg-slate-50">
          Solicitar permiso
        </a>
        <a routerLink="/mi/cambios-turno" class="inline-flex min-h-11 items-center rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-800 hover:bg-slate-50">
          Cambiar un turno
        </a>
      </div>
    </article>
  `,
})
export class ResumenVacacionesCardComponent implements OnInit {
  private readonly api = inject(MiApi);
  protected readonly saldo = signal<SaldoVacaciones | null>(null);
  protected readonly pendientes = signal(0);
  protected readonly cargando = signal(true);

  ngOnInit(): void {
    forkJoin({
      saldo: this.api.saldoVacaciones(),
      vacaciones: this.api.misVacaciones(),
      novedades: this.api.misNovedades(),
      cambios: this.api.misCambios(),
    }).subscribe({
      next: ({ saldo, vacaciones, novedades, cambios }) => {
        this.saldo.set(saldo);
        this.pendientes.set(
          [...vacaciones, ...novedades, ...cambios].filter((s) => s.codEstadoSolicitud === 'PENDIENTE').length
        );
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false),
    });
  }
}
