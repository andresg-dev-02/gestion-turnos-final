import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ApiClient } from '../../../app/core';
import { EventoCalendario, HistorialTurno } from '../../../app/shared/data/modelos';
import { MiApi } from '../../../app/shared/data/apis';
import { UiPaginadorComponent, UiSkeletonComponent, UiTabsComponent } from '../../../app/shared/ui';
import {
  fechaBogota,
  formatearFecha,
  formatearHora,
  horaCorta,
  hoyIso,
  inicioDiaIso,
  inicioSemana,
  sumarDias,
} from '../../../app/shared/utils/formato';

const ETIQUETAS: Record<string, string> = {
  TURNO: 'Turno',
  DESCANSO: 'Descanso',
  VACACIONES: 'Vacaciones',
  NOVEDAD: 'Novedad',
};
const COLORES: Record<string, string> = {
  TURNO: 'border-marca-500 bg-marca-50',
  DESCANSO: 'border-slate-400 bg-slate-50',
  VACACIONES: 'border-amber-500 bg-amber-50',
  NOVEDAD: 'border-indigo-500 bg-indigo-50',
};

/** Mi calendario (/mi/calendario): agenda semanal o mensual e historial de turnos pasados. */
@Component({
  selector: 'app-mi-calendario-page',
  imports: [UiPaginadorComponent, UiSkeletonComponent, UiTabsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-calendario" class="space-y-5">
      <header>
        <h1 id="titulo-calendario" class="text-2xl font-extrabold tracking-tight text-slate-900">Mi calendario</h1>
        <p class="mt-1 text-sm text-slate-600">Tus turnos publicados, descansos, vacaciones y novedades aprobadas.</p>
      </header>

      <ui-tabs etiqueta="Vista" [pestanas]="pestanas" [activa]="vista()" (cambio)="cambiarVista($event)" />

      @if (vista() === 'historial') {
        <div class="tarjeta">
          @if (cargando()) {
            <ui-skeleton />
          } @else {
            <div class="tabla-contenedor">
              <table class="tabla">
                <thead><tr><th scope="col">Fecha</th><th scope="col">Turno</th><th scope="col">Horario</th><th scope="col">Observación</th></tr></thead>
                <tbody>
                  @for (h of historial(); track h.idAsignacion) {
                    <tr>
                      <td class="whitespace-nowrap font-semibold">{{ formatearFecha(h.fecha) }}</td>
                      <td>{{ h.nombreTurno }}</td>
                      <td>{{ horaCorta(h.horaInicio) }}–{{ horaCorta(h.horaFin) }}</td>
                      <td class="text-slate-600">{{ h.observacion || '—' }}</td>
                    </tr>
                  } @empty {
                    <tr><td colspan="4" class="py-6 text-center text-slate-600">Aún no tienes turnos pasados.</td></tr>
                  }
                </tbody>
              </table>
            </div>
            <ui-paginador [pagina]="paginaHistorial()" [tamano]="20" [total]="totalHistorial()" (cambio)="cargarHistorial($event)" />
          }
        </div>
      } @else {
        <div class="tarjeta flex flex-wrap items-center gap-2">
          <button type="button" class="accion-secundaria" aria-label="Anterior" (click)="mover(-1)">‹</button>
          <button type="button" class="accion-secundaria" (click)="irHoy()">Hoy</button>
          <button type="button" class="accion-secundaria" aria-label="Siguiente" (click)="mover(1)">›</button>
          <p class="ml-2 text-sm font-bold text-slate-900">{{ formatearFecha(desde()) }} – {{ formatearFecha(sumarDias(hasta(), -1)) }}</p>
        </div>

        @if (cargando()) {
          <ui-skeleton [filas]="5" />
        } @else if (error()) {
          <p class="aviso-alerta">{{ error() }}</p>
        } @else {
          <ol class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-7">
            @for (d of dias(); track d.fecha) {
              <li class="rounded-2xl border bg-white p-3" [class.border-marca-500]="d.fecha === hoy" [class.border-slate-200]="d.fecha !== hoy">
                <p class="text-xs font-bold tracking-wider uppercase" [class.text-marca-700]="d.fecha === hoy" [class.text-slate-600]="d.fecha !== hoy">
                  {{ formatearFecha(d.fecha) }}
                </p>
                <ul class="mt-2 space-y-1.5">
                  @for (e of d.eventos; track $index) {
                    <li class="rounded-lg border-l-4 px-2 py-1.5 text-xs" [class]="color(e.tipo)">
                      <span class="block font-bold text-slate-900">{{ e.detalle }}</span>
                      @if (e.tipo === 'TURNO' || e.tipo === 'NOVEDAD') {
                        <span class="text-slate-700">{{ formatearHora(e.inicio) }}–{{ formatearHora(e.fin) }}</span>
                      } @else if (etiqueta(e.tipo) !== e.detalle) {
                        <span class="text-slate-700">{{ etiqueta(e.tipo) }}</span>
                      }
                    </li>
                  } @empty {
                    <li class="text-xs text-slate-400">Libre</li>
                  }
                </ul>
              </li>
            }
          </ol>
        }
      }
    </section>
  `,
})
export class MiCalendarioPageComponent implements OnInit {
  private readonly api = inject(MiApi);
  protected readonly formatearFecha = formatearFecha;
  protected readonly formatearHora = formatearHora;
  protected readonly horaCorta = horaCorta;
  protected readonly sumarDias = sumarDias;
  protected readonly hoy = hoyIso();

  protected readonly pestanas = [
    { id: 'semana', etiqueta: 'Semana' },
    { id: 'mes', etiqueta: '4 semanas' },
    { id: 'historial', etiqueta: 'Historial' },
  ];
  protected readonly vista = signal('semana');
  protected readonly desde = signal(inicioSemana(this.hoy));
  protected readonly hasta = computed(() => sumarDias(this.desde(), this.vista() === 'mes' ? 28 : 7));
  private readonly eventos = signal<readonly EventoCalendario[]>([]);
  protected readonly cargando = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly historial = signal<readonly HistorialTurno[]>([]);
  protected readonly paginaHistorial = signal(1);
  protected readonly totalHistorial = signal(0);

  /** Agrupa los eventos por cada día del rango (un evento multi-día aparece en todos sus días). */
  protected readonly dias = computed(() => {
    const total = this.vista() === 'mes' ? 28 : 7;
    return Array.from({ length: total }, (_, i) => {
      const fecha = sumarDias(this.desde(), i);
      const ini = new Date(inicioDiaIso(fecha));
      const fin = new Date(inicioDiaIso(sumarDias(fecha, 1)));
      return {
        fecha,
        eventos: this.eventos().filter((e) =>
          e.tipo === 'TURNO'
            ? fechaBogota(e.inicio) === fecha
            : new Date(e.inicio) < fin && new Date(e.fin) > ini
        ),
      };
    });
  });

  ngOnInit(): void {
    this.cargar();
  }

  etiqueta(tipo: string): string {
    return ETIQUETAS[tipo] ?? tipo;
  }

  color(tipo: string): string {
    return `rounded-lg border-l-4 px-2 py-1.5 text-xs ${COLORES[tipo] ?? 'border-slate-300 bg-slate-50'}`;
  }

  cambiarVista(v: string): void {
    this.vista.set(v);
    if (v === 'historial') this.cargarHistorial(1);
    else this.cargar();
  }

  mover(direccion: number): void {
    this.desde.set(sumarDias(this.desde(), direccion * (this.vista() === 'mes' ? 28 : 7)));
    this.cargar();
  }

  irHoy(): void {
    this.desde.set(inicioSemana(this.hoy));
    this.cargar();
  }

  private cargar(): void {
    this.cargando.set(true);
    this.error.set(null);
    this.api.calendario(inicioDiaIso(this.desde()), inicioDiaIso(this.hasta())).subscribe({
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

  cargarHistorial(pagina: number): void {
    this.cargando.set(true);
    this.paginaHistorial.set(pagina);
    this.api.historial(pagina).subscribe({
      next: (r) => {
        this.historial.set(r.items);
        this.totalHistorial.set(r.totalCount);
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.error.set(ApiClient.extraerMensajeError(err).general);
      },
    });
  }
}
