import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { ApiClient, NotificationService } from '../../../app/core';
import { SaldoVacaciones, SolicitudVacaciones } from '../../../app/shared/data/modelos';
import { MiApi } from '../../../app/shared/data/apis';
import {
  UiBadgeComponent,
  UiButtonComponent,
  UiNotchedFieldComponent,
  UiNotchedTextareaComponent,
  UiSkeletonComponent,
} from '../../../app/shared/ui';
import { formatearFecha, formatearFechaHora, hoyIso } from '../../../app/shared/utils/formato';

/** Mis vacaciones (/mi/vacaciones): saldo, nueva solicitud e historial con cancelación. */
@Component({
  selector: 'app-mis-vacaciones-page',
  imports: [UiBadgeComponent, UiButtonComponent, UiNotchedFieldComponent, UiNotchedTextareaComponent, UiSkeletonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-mis-vacaciones" class="space-y-6">
      <header>
        <h1 id="titulo-mis-vacaciones" class="text-2xl font-extrabold tracking-tight text-slate-900">Mis vacaciones</h1>
        <p class="mt-1 text-sm text-slate-600">Consulta tu saldo y solicita nuevos períodos de descanso.</p>
      </header>

      @if (cargando()) {
        <ui-skeleton [filas]="3" />
      } @else if (errorCarga()) {
        <p class="aviso-alerta">{{ errorCarga() }}</p>
      } @else {
        @if (saldo(); as s) {
          <dl class="grid grid-cols-2 gap-3 md:grid-cols-4">
            <div class="tarjeta"><dt class="text-xs font-semibold text-slate-600">Días causados</dt><dd class="mt-1 text-2xl font-extrabold">{{ s.diasOtorgados }}</dd></div>
            <div class="tarjeta"><dt class="text-xs font-semibold text-slate-600">Días disfrutados</dt><dd class="mt-1 text-2xl font-extrabold">{{ s.diasUtilizados }}</dd></div>
            <div class="tarjeta"><dt class="text-xs font-semibold text-slate-600">En solicitudes pendientes</dt><dd class="mt-1 text-2xl font-extrabold">{{ s.diasEnSolicitud }}</dd></div>
            <div class="tarjeta border-marca-500 bg-marca-50"><dt class="text-xs font-semibold text-marca-800">Disponibles para solicitar</dt><dd class="mt-1 text-2xl font-extrabold text-marca-700">{{ s.diasDisponiblesParaNuevaSolicitud }}</dd></div>
          </dl>
        }

        <div class="grid grid-cols-1 gap-6 lg:grid-cols-5">
          <article class="tarjeta lg:col-span-2" aria-labelledby="titulo-nueva-vac">
            <h2 id="titulo-nueva-vac" class="text-base font-bold text-slate-900">Nueva solicitud</h2>
            <form class="mt-5 space-y-5" (submit)="solicitar($event)" novalidate>
              <ui-notched-field id="vac-inicio" etiqueta="Desde" tipo="date" [min]="hoy" [valor]="inicio()" [obligatorio]="true"
                [error]="errores()['fechaInicio']" (valorCambio)="inicio.set($event)" />
              <ui-notched-field id="vac-fin" etiqueta="Hasta" tipo="date" [min]="inicio() || hoy" [valor]="fin()" [obligatorio]="true"
                [error]="errores()['fechaFin']" (valorCambio)="fin.set($event)" />
              <ui-notched-textarea id="vac-obs" etiqueta="Observación (opcional)" [valor]="observacion()" (valorCambio)="observacion.set($event)" />
              <p class="text-xs text-slate-600">Se cuentan días hábiles: no se descuentan sábados, domingos ni festivos.</p>
              @if (advertencia()) { <p class="aviso-alerta">{{ advertencia() }}</p> }
              <ui-button tipo="submit" [anchoCompleto]="true" [cargando]="enviando()">Enviar solicitud</ui-button>
            </form>
          </article>

          <article class="tarjeta lg:col-span-3" aria-labelledby="titulo-hist-vac">
            <h2 id="titulo-hist-vac" class="mb-3 text-base font-bold text-slate-900">Mis solicitudes</h2>
            <ul class="divide-y divide-slate-100">
              @for (v of solicitudes(); track v.idSolicitudVacaciones) {
                <li class="flex flex-wrap items-start justify-between gap-3 py-3">
                  <div>
                    <p class="font-semibold text-slate-900">{{ formatearFecha(v.fechaInicio) }} → {{ formatearFecha(v.fechaFin) }}</p>
                    <p class="text-xs text-slate-600">{{ v.diasSolicitados }} día(s) · solicitado {{ formatearFechaHora(v.solicitadoEn) }}</p>
                    @if (v.motivoRechazo) { <p class="mt-1 text-xs text-peligro">Motivo: {{ v.motivoRechazo }}</p> }
                  </div>
                  <div class="flex items-center gap-2">
                    <ui-badge [estado]="v.codEstadoSolicitud" />
                    @if (v.codEstadoSolicitud === 'PENDIENTE') {
                      <button type="button" class="accion-secundaria" [disabled]="cancelando() === v.idSolicitudVacaciones" (click)="cancelar(v)">Cancelar</button>
                    }
                  </div>
                </li>
              } @empty {
                <li class="py-6 text-center text-sm text-slate-600">Aún no has solicitado vacaciones.</li>
              }
            </ul>
          </article>
        </div>
      }
    </section>
  `,
})
export class MisVacacionesPageComponent implements OnInit {
  private readonly api = inject(MiApi);
  private readonly toast = inject(NotificationService);
  protected readonly formatearFecha = formatearFecha;
  protected readonly formatearFechaHora = formatearFechaHora;
  protected readonly hoy = hoyIso();

  protected readonly saldo = signal<SaldoVacaciones | null>(null);
  protected readonly solicitudes = signal<readonly SolicitudVacaciones[]>([]);
  protected readonly cargando = signal(true);
  protected readonly errorCarga = signal<string | null>(null);
  protected readonly inicio = signal('');
  protected readonly fin = signal('');
  protected readonly observacion = signal('');
  protected readonly errores = signal<Record<string, string>>({});
  protected readonly advertencia = signal<string | null>(null);
  protected readonly enviando = signal(false);
  protected readonly cancelando = signal<number | null>(null);

  ngOnInit(): void {
    this.cargar();
  }

  private cargar(): void {
    forkJoin({ saldo: this.api.saldoVacaciones(), solicitudes: this.api.misVacaciones() }).subscribe({
      next: ({ saldo, solicitudes }) => {
        this.saldo.set(saldo);
        this.solicitudes.set([...solicitudes].sort((a, b) => b.solicitadoEn.localeCompare(a.solicitadoEn)));
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
    const errores: Record<string, string> = {};
    if (!this.inicio()) errores['fechaInicio'] = 'Indica la fecha de inicio.';
    else if (this.inicio() < this.hoy) errores['fechaInicio'] = 'No puede ser una fecha pasada.';
    if (!this.fin()) errores['fechaFin'] = 'Indica la fecha final.';
    else if (this.inicio() && this.fin() < this.inicio()) errores['fechaFin'] = 'Debe ser igual o posterior a la fecha de inicio.';
    this.errores.set(errores);
    this.advertencia.set(null);
    if (Object.keys(errores).length) return;

    this.enviando.set(true);
    this.api.solicitarVacaciones(this.inicio(), this.fin(), this.observacion().trim() || null).subscribe({
      next: (r) => {
        this.enviando.set(false);
        this.toast.exito(`Solicitud enviada por ${r.diasSolicitados} día(s). Te avisaremos cuando sea revisada.`);
        this.advertencia.set(r.advertenciaConflictos);
        this.inicio.set('');
        this.fin.set('');
        this.observacion.set('');
        this.cargar();
      },
      error: (err: unknown) => {
        this.enviando.set(false);
        const e = ApiClient.extraerMensajeError(err);
        this.errores.set(e.campos);
        this.toast.error(e.general);
      },
    });
  }

  cancelar(v: SolicitudVacaciones): void {
    this.cancelando.set(v.idSolicitudVacaciones);
    this.api.cancelarVacaciones(v.idSolicitudVacaciones).subscribe({
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
