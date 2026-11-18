import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiClient } from '../../../app/core';
import { EventoCalendario } from '../../../app/shared/data/modelos';
import { MiApi } from '../../../app/shared/data/apis';
import { UiSkeletonComponent } from '../../../app/shared/ui';
import { fechaBogota, formatearFecha, formatearHora, hoyIso, inicioDiaIso, sumarDias } from '../../../app/shared/utils/formato';

/**
 * Turno de hoy y próximos eventos del empleado, leídos de GET /mi-calendario
 * (turnos.v_calendario: turnos, descansos, vacaciones y novedades aprobadas).
 */
@Component({
  selector: 'app-turno-hoy-card',
  imports: [RouterLink, UiSkeletonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article aria-labelledby="titulo-turno-hoy" class="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs">
      <h2 id="titulo-turno-hoy" class="text-base font-bold text-slate-900">Mi jornada de hoy</h2>
      <p class="text-xs text-slate-600">{{ formatearFecha(hoy) }} · Hora de Bogotá</p>

      @if (cargando()) {
        <div class="mt-4"><ui-skeleton [filas]="2" /></div>
      } @else if (error()) {
        <p class="aviso-alerta mt-4">{{ error() }}</p>
      } @else {
        @if (eventosHoy().length) {
          <ul class="mt-4 space-y-2">
            @for (e of eventosHoy(); track $index) {
              <li class="rounded-xl border-l-4 bg-slate-50 p-3" [class]="claseTipo(e.tipo)">
                <p class="text-xs font-bold tracking-wider text-slate-600 uppercase">{{ etiquetaTipo(e.tipo) }}</p>
                <p class="text-lg font-extrabold text-slate-900">{{ e.detalle }}</p>
                @if (e.tipo === 'TURNO' || e.tipo === 'NOVEDAD') {
                  <p class="text-sm font-semibold text-marca-700">{{ formatearHora(e.inicio) }} – {{ formatearHora(e.fin) }}</p>
                }
              </li>
            }
          </ul>
        } @else {
          <p class="mt-4 rounded-xl bg-slate-50 p-4 text-sm text-slate-700">No tienes turnos programados para hoy.</p>
        }

        <h3 class="mt-5 text-sm font-bold text-slate-900">Próximos 7 días</h3>
        <ul class="mt-2 divide-y divide-slate-100">
          @for (e of proximos(); track $index) {
            <li class="flex items-center justify-between gap-3 py-2 text-sm">
              <span><strong>{{ formatearFecha(fechaBogota(e.inicio)) }}</strong> · {{ e.detalle }}</span>
              <span class="text-xs text-slate-600">
                @if (e.tipo === 'TURNO' || e.tipo === 'NOVEDAD') { {{ formatearHora(e.inicio) }}–{{ formatearHora(e.fin) }} }
              </span>
            </li>
          } @empty {
            <li class="py-2 text-sm text-slate-600">Sin eventos próximos.</li>
          }
        </ul>
        <a routerLink="/mi/calendario" class="mt-3 inline-flex min-h-11 items-center text-xs font-bold text-marca-700 underline underline-offset-4">
          Ver mi calendario completo
        </a>
      }
    </article>
  `,
})
export class TurnoHoyCardComponent implements OnInit {
  private readonly api = inject(MiApi);
  protected readonly formatearFecha = formatearFecha;
  protected readonly formatearHora = formatearHora;
  protected readonly fechaBogota = fechaBogota;
  protected readonly hoy = hoyIso();

  private readonly eventos = signal<readonly EventoCalendario[]>([]);
  protected readonly cargando = signal(true);
  protected readonly error = signal<string | null>(null);

  private readonly inicioHoy = new Date(inicioDiaIso(this.hoy));
  private readonly finHoy = new Date(inicioDiaIso(sumarDias(this.hoy, 1)));
  protected readonly eventosHoy = computed(() =>
    this.eventos().filter((e) => new Date(e.inicio) < this.finHoy && new Date(e.fin) > this.inicioHoy)
  );
  protected readonly proximos = computed(() =>
    this.eventos()
      .filter((e) => new Date(e.inicio) >= this.finHoy)
      .slice(0, 6)
  );

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.api.calendario(inicioDiaIso(this.hoy), inicioDiaIso(sumarDias(this.hoy, 8))).subscribe({
      next: (l) => {
        this.eventos.set(l);
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.error.set(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  etiquetaTipo(tipo: string): string {
    return { TURNO: 'Turno', DESCANSO: 'Descanso', VACACIONES: 'Vacaciones', NOVEDAD: 'Novedad' }[tipo] ?? tipo;
  }

  claseTipo(tipo: string): string {
    const borde = { TURNO: 'border-marca-500', DESCANSO: 'border-slate-400', VACACIONES: 'border-amber-500', NOVEDAD: 'border-indigo-500' }[tipo];
    return `rounded-xl border-l-4 bg-slate-50 p-3 ${borde ?? 'border-slate-300'}`;
  }
}
