import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { ApiClient, AuthStore, NotificationService } from '../../../app/core';
import { SolicitudCambioTurno, TurnoIntercambiable } from '../../../app/shared/data/modelos';
import { MiApi } from '../../../app/shared/data/apis';
import { UiBadgeComponent, UiButtonComponent, UiNotchedTextareaComponent, UiSkeletonComponent } from '../../../app/shared/ui';
import { formatearFecha, formatearFechaHora, horaCorta, hoyIso, sumarDias } from '../../../app/shared/utils/formato';

/**
 * Cambios de turno (/mi/cambios-turno): el empleado elige uno de sus turnos futuros y el
 * turno de un compañero de su área para intercambiarlos; el administrador lo aprueba.
 */
@Component({
  selector: 'app-mis-cambios-turno-page',
  imports: [UiBadgeComponent, UiButtonComponent, UiNotchedTextareaComponent, UiSkeletonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-cambios" class="space-y-6">
      <header>
        <h1 id="titulo-cambios" class="text-2xl font-extrabold tracking-tight text-slate-900">Cambios de turno</h1>
        <p class="mt-1 text-sm text-slate-600">
          Intercambia uno de tus próximos turnos con un compañero de tu área. El cambio se aplica cuando el administrador lo aprueba.
        </p>
      </header>

      @if (cargando()) {
        <ui-skeleton [filas]="3" />
      } @else if (errorCarga()) {
        <p class="aviso-alerta">{{ errorCarga() }}</p>
      } @else {
        <article class="tarjeta" aria-labelledby="titulo-nuevo-cambio">
          <h2 id="titulo-nuevo-cambio" class="text-base font-bold text-slate-900">Nueva solicitud</h2>

          <div class="mt-4 grid grid-cols-1 gap-6 lg:grid-cols-2">
            <fieldset>
              <legend class="mb-2 text-sm font-semibold text-slate-800">1. Tu turno que entregas</legend>
              <div class="max-h-72 space-y-2 overflow-y-auto pr-1">
                @for (t of misTurnos(); track t.idAsignacion) {
                  <label class="flex cursor-pointer items-center gap-3 rounded-xl border p-3 text-sm transition"
                    [class.border-marca-600]="miTurno() === t.idAsignacion" [class.bg-marca-50]="miTurno() === t.idAsignacion"
                    [class.border-slate-200]="miTurno() !== t.idAsignacion">
                    <input type="radio" name="mi-turno" class="h-4 w-4 accent-marca-600" [checked]="miTurno() === t.idAsignacion" (change)="miTurno.set(t.idAsignacion)" />
                    <span class="h-8 w-1.5 rounded-full" [style.background-color]="t.colorTurno || '#64748b'" aria-hidden="true"></span>
                    <span>
                      <strong>{{ formatearFecha(t.fecha) }}</strong> · {{ t.nombreTurno }}
                      <span class="block text-xs text-slate-600">{{ horaCorta(t.horaInicio) }}–{{ horaCorta(t.horaFin) }}</span>
                    </span>
                  </label>
                } @empty {
                  <p class="rounded-xl bg-slate-50 p-4 text-sm text-slate-600">No tienes turnos futuros para intercambiar.</p>
                }
              </div>
            </fieldset>

            <fieldset>
              <legend class="mb-2 text-sm font-semibold text-slate-800">2. Turno del compañero que recibes</legend>
              <input type="search" placeholder="Filtrar por compañero o turno" aria-label="Filtrar turnos de compañeros"
                class="filtro-select mb-2 w-full" (input)="filtro.set($any($event.target).value)" />
              <div class="max-h-64 space-y-2 overflow-y-auto pr-1">
                @for (t of turnosCompanerosFiltrados(); track t.idAsignacion) {
                  <label class="flex cursor-pointer items-center gap-3 rounded-xl border p-3 text-sm transition"
                    [class.border-marca-600]="suTurno() === t.idAsignacion" [class.bg-marca-50]="suTurno() === t.idAsignacion"
                    [class.border-slate-200]="suTurno() !== t.idAsignacion">
                    <input type="radio" name="su-turno" class="h-4 w-4 accent-marca-600" [checked]="suTurno() === t.idAsignacion" (change)="suTurno.set(t.idAsignacion)" />
                    <span class="h-8 w-1.5 rounded-full" [style.background-color]="t.colorTurno || '#64748b'" aria-hidden="true"></span>
                    <span>
                      <strong>{{ t.nombreEmpleado }}</strong>
                      <span class="block text-xs text-slate-600">{{ formatearFecha(t.fecha) }} · {{ t.nombreTurno }} {{ horaCorta(t.horaInicio) }}–{{ horaCorta(t.horaFin) }}</span>
                    </span>
                  </label>
                } @empty {
                  <p class="rounded-xl bg-slate-50 p-4 text-sm text-slate-600">No hay turnos de compañeros de tu área en los próximos 30 días.</p>
                }
              </div>
            </fieldset>
          </div>

          <form class="mt-5 space-y-4" (submit)="solicitar($event)" novalidate>
            <ui-notched-textarea id="cam-motivo" etiqueta="Motivo (opcional)" [valor]="motivo()" (valorCambio)="motivo.set($event)" />
            @if (error()) { <p role="alert" class="aviso-error">{{ error() }}</p> }
            <div class="flex justify-end">
              <ui-button tipo="submit" [cargando]="enviando()">Solicitar cambio</ui-button>
            </div>
          </form>
        </article>

        <article class="tarjeta" aria-labelledby="titulo-mis-cambios">
          <h2 id="titulo-mis-cambios" class="mb-3 text-base font-bold text-slate-900">Mis solicitudes</h2>
          <ul class="divide-y divide-slate-100">
            @for (c of cambios(); track c.idSolicitudCambio) {
              <li class="flex flex-wrap items-start justify-between gap-3 py-3 text-sm">
                <div>
                  <p>
                    <strong>{{ c.nombreEmpleadoSolicitante }}</strong> entrega {{ c.nombreTurnoSolicitante }} del {{ formatearFecha(c.fechaTurnoSolicitante) }}
                  </p>
                  <p>
                    <strong>{{ c.nombreEmpleadoCompanero }}</strong> entrega {{ c.nombreTurnoCompanero }} del {{ formatearFecha(c.fechaTurnoCompanero) }}
                  </p>
                  <p class="text-xs text-slate-500">Solicitado {{ formatearFechaHora(c.solicitadoEn) }}</p>
                  @if (c.motivoRechazo) { <p class="mt-1 text-xs text-peligro">Motivo: {{ c.motivoRechazo }}</p> }
                </div>
                <div class="flex items-center gap-2">
                  <ui-badge [estado]="c.codEstadoSolicitud" />
                  @if (c.codEstadoSolicitud === 'PENDIENTE' && c.idEmpleadoSolicitante === idEmpleado) {
                    <button type="button" class="accion-secundaria" [disabled]="cancelando() === c.idSolicitudCambio" (click)="cancelar(c)">Cancelar</button>
                  }
                </div>
              </li>
            } @empty {
              <li class="py-6 text-center text-sm text-slate-600">No tienes solicitudes de cambio.</li>
            }
          </ul>
        </article>
      }
    </section>
  `,
})
export class MisCambiosTurnoPageComponent implements OnInit {
  private readonly api = inject(MiApi);
  private readonly toast = inject(NotificationService);
  protected readonly idEmpleado = inject(AuthStore).usuario()?.idEmpleado ?? null;
  protected readonly formatearFecha = formatearFecha;
  protected readonly formatearFechaHora = formatearFechaHora;
  protected readonly horaCorta = horaCorta;

  protected readonly misTurnos = signal<readonly TurnoIntercambiable[]>([]);
  private readonly turnosCompaneros = signal<readonly TurnoIntercambiable[]>([]);
  protected readonly cambios = signal<readonly SolicitudCambioTurno[]>([]);
  protected readonly cargando = signal(true);
  protected readonly errorCarga = signal<string | null>(null);
  protected readonly miTurno = signal<number | null>(null);
  protected readonly suTurno = signal<number | null>(null);
  protected readonly filtro = signal('');
  protected readonly motivo = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly enviando = signal(false);
  protected readonly cancelando = signal<number | null>(null);

  protected readonly turnosCompanerosFiltrados = computed(() => {
    const f = this.filtro().trim().toLowerCase();
    return f
      ? this.turnosCompaneros().filter((t) => `${t.nombreEmpleado} ${t.nombreTurno}`.toLowerCase().includes(f))
      : this.turnosCompaneros();
  });

  ngOnInit(): void {
    this.cargar();
  }

  private cargar(): void {
    const hoy = hoyIso();
    forkJoin({
      mios: this.api.proximosTurnos(30),
      companeros: this.api.turnosCompaneros(hoy, sumarDias(hoy, 30)),
      cambios: this.api.misCambios(),
    }).subscribe({
      next: ({ mios, companeros, cambios }) => {
        // Solo turnos que aún no empiezan: la API rechaza los que ya iniciaron.
        this.misTurnos.set(mios.filter((t) => t.fecha > hoy));
        this.turnosCompaneros.set(companeros.filter((t) => t.fecha > hoy));
        this.cambios.set([...cambios].sort((a, b) => b.solicitadoEn.localeCompare(a.solicitadoEn)));
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.errorCarga.set(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  solicitar(evento: Event): void {
    evento.preventDefault();
    const mio = this.miTurno();
    const suyo = this.suTurno();
    if (!mio || !suyo) {
      this.error.set('Selecciona tu turno y el turno del compañero.');
      return;
    }
    this.error.set(null);
    this.enviando.set(true);
    this.api.solicitarCambio(mio, suyo, this.motivo().trim() || null).subscribe({
      next: () => {
        this.enviando.set(false);
        this.toast.exito('Solicitud de cambio enviada. Te avisaremos cuando sea revisada.');
        this.miTurno.set(null);
        this.suTurno.set(null);
        this.motivo.set('');
        this.cargar();
      },
      error: (err: unknown) => {
        this.enviando.set(false);
        this.error.set(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  cancelar(c: SolicitudCambioTurno): void {
    this.cancelando.set(c.idSolicitudCambio);
    this.api.cancelarCambio(c.idSolicitudCambio).subscribe({
      next: () => {
        this.cancelando.set(null);
        this.toast.exito('Solicitud cancelada.');
        this.cargar();
      },
      error: (err: unknown) => {
        this.cancelando.set(null);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }
}
