import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../../app/core';
import { HorasPorTurno } from '../../../app/shared/data/modelos';
import { MiApi } from '../../../app/shared/data/apis';
import { UiSkeletonComponent } from '../../../app/shared/ui';
import { formatearFecha, formatearHora, hoyIso, minutosAHoras, sumarDias } from '../../../app/shared/utils/formato';
import { MarcajeRapidoCardComponent } from '../components/marcaje-rapido-card.component';

/** Mi asistencia (/mi/asistencia): marcaje, horas trabajadas y seguimiento de días asistidos vs faltantes. */
@Component({
  selector: 'app-mi-asistencia-page',
  imports: [FormsModule, MarcajeRapidoCardComponent, UiSkeletonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-asistencia" class="space-y-6">
      <header>
        <h1 id="titulo-asistencia" class="text-2xl font-extrabold tracking-tight text-slate-900">Mi asistencia</h1>
        <p class="mt-1 text-sm text-slate-600">Marca tu entrada y salida, revisa tus horas y monitorea días faltantes de marcaje.</p>
      </header>

      <div class="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <app-marcaje-rapido-card (marcado)="cargar()" />
        <dl class="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:col-span-2">
          <div class="tarjeta">
            <dt class="text-xs font-semibold text-slate-600">Programadas</dt>
            <dd class="mt-1 text-lg font-extrabold text-slate-900">{{ minutosAHoras(totales().programados) }}</dd>
          </div>
          <div class="tarjeta">
            <dt class="text-xs font-semibold text-slate-600">Trabajadas</dt>
            <dd class="mt-1 text-lg font-extrabold text-slate-900">{{ minutosAHoras(totales().trabajados) }}</dd>
          </div>
          <div class="tarjeta">
            <dt class="text-xs font-semibold text-slate-600">Llegadas tarde</dt>
            <dd class="mt-1 text-lg font-extrabold text-amber-600">{{ totales().tardes }}</dd>
          </div>
          <div
            class="tarjeta border-l-4 transition-colors"
            [class.border-l-rose-500]="totales().sinMarcaje > 0"
            [class.bg-rose-50/30]="totales().sinMarcaje > 0"
            [class.border-l-slate-300]="totales().sinMarcaje === 0"
          >
            <dt class="text-xs font-semibold text-slate-600">Días sin marcaje</dt>
            <dd
              class="mt-1 text-lg font-extrabold"
              [class.text-rose-600]="totales().sinMarcaje > 0"
              [class.text-slate-800]="totales().sinMarcaje === 0"
            >
              {{ totales().sinMarcaje }}
            </dd>
          </div>
        </dl>
      </div>

      <article class="tarjeta" aria-labelledby="titulo-horas">
        <div class="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 id="titulo-horas" class="text-base font-bold text-slate-900">Horas por turno</h2>
            <p class="text-xs text-slate-500">Historial de asistencia detallado por turno asignado.</p>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <select
              aria-label="Filtro de estado"
              class="filtro-select text-xs"
              [ngModel]="filtroEstado()"
              (ngModelChange)="filtroEstado.set($event)"
            >
              <option value="TODOS">Todos los turnos ({{ horas().length }})</option>
              <option value="ASISTIDOS">Con asistencia ({{ totalAsistidos() }})</option>
              <option value="JUSTIFICADOS">Incapacidades / Justificados ({{ totales().justificados }})</option>
              <option value="FALTANTES">Días sin marcaje ({{ totales().sinMarcaje }})</option>
            </select>
            <select aria-label="Período" class="filtro-select text-xs" (change)="cambiarDias(+$any($event.target).value)">
              <option value="7">Últimos 7 días</option>
              <option value="14" selected>Últimos 14 días</option>
              <option value="30">Últimos 30 días</option>
            </select>
          </div>
        </div>

        @if (cargando()) {
          <ui-skeleton />
        } @else if (error()) {
          <p class="aviso-alerta">{{ error() }}</p>
        } @else {
          <div class="tabla-contenedor">
            <table class="tabla">
              <thead>
                <tr>
                  <th scope="col">Fecha</th>
                  <th scope="col">Turno</th>
                  <th scope="col">Entrada</th>
                  <th scope="col">Salida</th>
                  <th scope="col">Trabajado</th>
                  <th scope="col">Diferencia</th>
                  <th scope="col">Estado</th>
                </tr>
              </thead>
              <tbody>
                @for (h of horasFiltradas(); track h.idAsignacion) {
                  <tr
                    [class.bg-rose-50/40]="esFaltante(h)"
                    [class.bg-indigo-50/30]="esJustificado(h)"
                    class="transition-colors hover:bg-slate-50"
                  >
                    <td class="whitespace-nowrap font-semibold">{{ formatearFecha(h.fecha) }}</td>
                    <td>{{ h.nombreTurno }}</td>
                    <td>
                      @if (h.horaEntrada) {
                        <span class="font-mono text-xs">{{ formatearHora(h.horaEntrada) }}</span>
                        @if (h.minutosTarde) {
                          <span class="block text-xs font-semibold text-amber-600">{{ h.minutosTarde }} min tarde</span>
                        }
                      } @else if (esJustificado(h)) {
                        <span class="inline-flex items-center rounded bg-indigo-100/80 px-2 py-0.5 text-xs font-semibold text-indigo-700">
                          {{ h.novedad || 'Incapacidad médica' }}
                        </span>
                      } @else {
                        <span class="inline-flex items-center rounded bg-rose-100/80 px-2 py-0.5 text-xs font-semibold text-rose-700">
                          Sin entrada
                        </span>
                      }
                    </td>
                    <td>
                      @if (h.horaSalida) {
                        <span class="font-mono text-xs">{{ formatearHora(h.horaSalida) }}</span>
                      } @else if (esJustificado(h)) {
                        <span class="inline-flex items-center rounded bg-indigo-100/80 px-2 py-0.5 text-xs font-semibold text-indigo-700">
                          {{ h.novedad || 'Incapacidad médica' }}
                        </span>
                      } @else if (h.horaEntrada) {
                        <span class="inline-flex items-center rounded bg-amber-100/80 px-2 py-0.5 text-xs font-medium text-amber-800">
                          Sin salida
                        </span>
                      } @else {
                        <span class="inline-flex items-center rounded bg-rose-100/80 px-2 py-0.5 text-xs font-semibold text-rose-700">
                          Sin salida
                        </span>
                      }
                    </td>
                    <td class="font-mono text-xs">{{ minutosAHoras(h.minutosTrabajados) }}</td>
                    <td
                      class="font-semibold text-xs"
                      [class.text-peligro]="!esJustificado(h) && h.diferenciaMinutos < 0"
                      [class.text-exito]="!esJustificado(h) && h.diferenciaMinutos >= 0"
                      [class.text-slate-500]="esJustificado(h)"
                    >
                      {{ esJustificado(h) ? '0 h 00 min' : minutosAHoras(h.diferenciaMinutos) }}
                    </td>
                    <td>
                      @if (esJustificado(h)) {
                        <span class="inline-flex items-center gap-1.5 rounded-full border border-indigo-200 bg-indigo-100 px-2.5 py-0.5 text-xs font-bold text-indigo-800">
                          <span class="h-1.5 w-1.5 rounded-full bg-indigo-600" aria-hidden="true"></span>
                          {{ h.novedad || 'Incapacidad médica' }}
                        </span>
                      } @else if (esFaltante(h)) {
                        <span class="inline-flex items-center gap-1.5 rounded-full border border-rose-200 bg-rose-100 px-2.5 py-0.5 text-xs font-bold text-rose-800">
                          <span class="h-1.5 w-1.5 rounded-full bg-rose-500" aria-hidden="true"></span>
                          Día faltante
                        </span>
                      } @else if (!h.horaSalida) {
                        <span class="inline-flex items-center gap-1.5 rounded-full border border-amber-200 bg-amber-100 px-2.5 py-0.5 text-xs font-bold text-amber-800">
                          <span class="h-1.5 w-1.5 rounded-full bg-amber-500" aria-hidden="true"></span>
                          En curso
                        </span>
                      } @else {
                        <span class="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-100 px-2.5 py-0.5 text-xs font-bold text-emerald-800">
                          <span class="h-1.5 w-1.5 rounded-full bg-emerald-500" aria-hidden="true"></span>
                          Completado
                        </span>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="py-6 text-center text-slate-600">
                      @if (filtroEstado() === 'FALTANTES') {
                        ¡Excelente! No tienes días faltantes ni inasistencias en este período.
                      } @else if (filtroEstado() === 'JUSTIFICADOS') {
                        No tienes novedades ni incapacidades en este período.
                      } @else {
                        No hay turnos registrados en el período.
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </article>
    </section>
  `,
})
export class MiAsistenciaPageComponent implements OnInit {
  private readonly api = inject(MiApi);
  protected readonly formatearFecha = formatearFecha;
  protected readonly formatearHora = formatearHora;
  protected readonly minutosAHoras = minutosAHoras;

  private readonly dias = signal(14);
  protected readonly horas = signal<readonly HorasPorTurno[]>([]);
  protected readonly cargando = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly filtroEstado = signal<'TODOS' | 'FALTANTES' | 'JUSTIFICADOS' | 'ASISTIDOS'>('TODOS');

  protected readonly totales = computed(() =>
    this.horas().reduce(
      (t, h) => {
        const justificado = this.esJustificado(h);
        const faltante = this.esFaltante(h);
        return {
          programados: t.programados + h.minutosProgramados,
          trabajados: t.trabajados + h.minutosTrabajados,
          tardes: t.tardes + (h.minutosTarde ? 1 : 0),
          sinMarcaje: t.sinMarcaje + (faltante ? 1 : 0),
          justificados: t.justificados + (justificado ? 1 : 0),
        };
      },
      { programados: 0, trabajados: 0, tardes: 0, sinMarcaje: 0, justificados: 0 }
    )
  );

  protected readonly totalAsistidos = computed(
    () => this.horas().filter((h) => !this.esFaltante(h) && !this.esJustificado(h)).length
  );

  protected readonly horasFiltradas = computed(() => {
    const lista = this.horas();
    const filtro = this.filtroEstado();
    if (filtro === 'FALTANTES') {
      return lista.filter((h) => this.esFaltante(h));
    }
    if (filtro === 'JUSTIFICADOS') {
      return lista.filter((h) => this.esJustificado(h));
    }
    if (filtro === 'ASISTIDOS') {
      return lista.filter((h) => !this.esFaltante(h) && !this.esJustificado(h));
    }
    return lista;
  });

  ngOnInit(): void {
    this.cargar();
  }

  cambiarDias(d: number): void {
    this.dias.set(d);
    this.cargar();
  }

  cargar(): void {
    const hoy = hoyIso();
    this.cargando.set(true);
    this.api.horasPorTurno(sumarDias(hoy, -this.dias()), hoy).subscribe({
      next: (l) => {
        this.horas.set([...l].sort((a, b) => b.fecha.localeCompare(a.fecha)));
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.error.set(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  protected esJustificado(h: HorasPorTurno): boolean {
    return Boolean(h.esJustificado || h.novedad);
  }

  protected esFaltante(h: HorasPorTurno): boolean {
    if (this.esJustificado(h)) {
      return false;
    }
    return (!h.horaEntrada && !h.horaSalida) || Boolean(h.sinAsistencia);
  }
}
