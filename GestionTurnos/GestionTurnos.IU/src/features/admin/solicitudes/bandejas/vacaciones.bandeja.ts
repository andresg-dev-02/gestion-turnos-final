import { ChangeDetectionStrategy, Component, OnInit, inject, output, signal } from '@angular/core';
import { ApiClient, NotificationService } from '../../../../app/core';
import {
  ConflictosSolicitudVacaciones,
  ESTADOS_SOLICITUD,
  SolicitudVacacionesBandeja,
} from '../../../../app/shared/data/modelos';
import { SolicitudesAdminApi } from '../../../../app/shared/data/apis';
import {
  UiBadgeComponent,
  UiButtonComponent,
  UiCheckComponent,
  UiDialogComponent,
  UiNotchedTextareaComponent,
  UiPaginadorComponent,
  UiSkeletonComponent,
} from '../../../../app/shared/ui';
import { formatearFecha, formatearFechaHora, formatearHora } from '../../../../app/shared/utils/formato';

/**
 * Bandeja de novedades.solicitud_vacaciones con el saldo del empleado
 * (leído de v_saldo_vacaciones) y los turnos que chocan con el período.
 */
@Component({
  selector: 'app-vacaciones-bandeja',
  imports: [
    UiBadgeComponent,
    UiButtonComponent,
    UiCheckComponent,
    UiDialogComponent,
    UiNotchedTextareaComponent,
    UiPaginadorComponent,
    UiSkeletonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mb-3 flex flex-wrap items-center gap-3">
      <label for="vac-estado" class="text-sm font-semibold text-slate-700">Estado</label>
      <select id="vac-estado" class="filtro-select" (change)="filtrar($any($event.target).value)">
        @for (e of estados; track e.valor) {
          <option [value]="e.valor" [selected]="e.valor === estado()">{{ e.etiqueta }}</option>
        }
      </select>
    </div>

    @if (cargando()) {
      <ui-skeleton />
    } @else {
      <div class="tabla-contenedor">
        <table class="tabla">
          <thead>
            <tr>
              <th scope="col">Empleado</th>
              <th scope="col">Período</th>
              <th scope="col">Días</th>
              <th scope="col">Saldo efectivo</th>
              <th scope="col">Estado</th>
              <th scope="col" class="text-right">Acciones</th>
            </tr>
          </thead>
          <tbody>
            @for (b of items(); track b.solicitud.idSolicitudVacaciones) {
              <tr>
                <td>
                  <span class="font-semibold text-slate-900">{{ b.solicitud.nombreEmpleado }}</span>
                  <span class="block text-xs text-slate-600">Solicitado {{ formatearFechaHora(b.solicitud.solicitadoEn) }}</span>
                </td>
                <td class="whitespace-nowrap">
                  {{ formatearFecha(b.solicitud.fechaInicio) }}<br />
                  <span class="text-slate-600">al {{ formatearFecha(b.solicitud.fechaFin) }}</span>
                </td>
                <td class="font-bold">{{ b.solicitud.diasSolicitados }}</td>
                <td [class.text-peligro]="b.saldoEfectivo < 0" class="font-semibold">{{ b.saldoEfectivo }}</td>
                <td>
                  <ui-badge [estado]="b.solicitud.codEstadoSolicitud" />
                  @if (b.solicitud.motivoRechazo) {
                    <span class="mt-1 block text-xs text-slate-600">{{ b.solicitud.motivoRechazo }}</span>
                  }
                </td>
                <td class="text-right whitespace-nowrap">
                  @if (b.solicitud.codEstadoSolicitud === 'PENDIENTE') {
                    <button type="button" class="accion-primaria" (click)="revisar(b)">Revisar</button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-6 text-center text-slate-600">Aún no hay registros con este filtro.</td></tr>
            }
          </tbody>
        </table>
      </div>
      <ui-paginador [pagina]="pagina()" [tamano]="tamano" [total]="total()" (cambio)="irA($event)" />
    }

    <ui-dialog
      id="dlg-vacaciones"
      ancho="lg"
      [abierto]="seleccion() !== null"
      titulo="Resolver solicitud de vacaciones"
      [subtitulo]="seleccion()?.solicitud?.nombreEmpleado ?? ''"
      (cerrar)="seleccion.set(null)"
    >
      @if (seleccion(); as b) {
        <dl class="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div class="rounded-xl bg-slate-50 p-3"><dt class="text-xs text-slate-600">Desde</dt><dd class="font-bold">{{ formatearFecha(b.solicitud.fechaInicio) }}</dd></div>
          <div class="rounded-xl bg-slate-50 p-3"><dt class="text-xs text-slate-600">Hasta</dt><dd class="font-bold">{{ formatearFecha(b.solicitud.fechaFin) }}</dd></div>
          <div class="rounded-xl bg-slate-50 p-3"><dt class="text-xs text-slate-600">Días solicitados</dt><dd class="text-xl font-extrabold">{{ b.solicitud.diasSolicitados }}</dd></div>
          <div class="rounded-xl bg-slate-50 p-3"><dt class="text-xs text-slate-600">Disponibles</dt><dd class="text-xl font-extrabold" [class.text-peligro]="b.diasDisponibles < b.solicitud.diasSolicitados">{{ b.diasDisponibles }}</dd></div>
        </dl>
        @if (b.solicitud.observacion) {
          <p class="mt-3 text-sm text-slate-700"><strong>Observación:</strong> {{ b.solicitud.observacion }}</p>
        }

        <h3 class="mt-5 mb-2 text-sm font-bold text-slate-900">Turnos programados en el período</h3>
        @if (cargandoConflictos()) {
          <ui-skeleton [filas]="1" />
        } @else if (conflictos()?.turnos?.length) {
          <ul class="divide-y divide-slate-100 rounded-xl border border-slate-200">
            @for (t of conflictos()!.turnos; track t.idAsignacion) {
              <li class="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                <span>{{ formatearFecha(t.fecha) }} · {{ t.nombreTurno }} ({{ formatearHora(t.periodoInicio) }}–{{ formatearHora(t.periodoFin) }})</span>
                @if (t.tieneAsistencia) {
                  <span class="text-xs font-bold text-peligro">Con asistencia: impide aprobar</span>
                }
              </li>
            }
          </ul>
          <div class="mt-3">
            <ui-check
              id="vac-confirmar"
              etiqueta="Anular estos turnos al aprobar"
              ayuda="Requerido para aprobar cuando hay turnos en el período."
              [marcado]="confirmarAnulacion()"
              (marcadoCambio)="confirmarAnulacion.set($event)"
            />
          </div>
        } @else {
          <p class="aviso-info">No hay turnos programados que choquen con estas vacaciones.</p>
        }

        <div class="mt-5">
          <ui-notched-textarea
            id="vac-motivo"
            etiqueta="Motivo (obligatorio solo para rechazar)"
            [valor]="motivo()"
            [error]="errorMotivo()"
            (valorCambio)="motivo.set($event)"
          />
        </div>
      }
      <ng-container acciones>
        <ui-button variante="secundario" (click)="seleccion.set(null)">Cancelar</ui-button>
        <ui-button variante="peligro" [cargando]="enviando()" (click)="resolver(false)">Rechazar</ui-button>
        <ui-button [cargando]="enviando()" (click)="resolver(true)">Aprobar</ui-button>
      </ng-container>
    </ui-dialog>
  `,
})
export class VacacionesBandejaComponent implements OnInit {
  private readonly api = inject(SolicitudesAdminApi);
  private readonly toast = inject(NotificationService);
  readonly cambioPendientes = output<void>();

  protected readonly formatearFecha = formatearFecha;
  protected readonly formatearFechaHora = formatearFechaHora;
  protected readonly formatearHora = formatearHora;
  protected readonly estados = ESTADOS_SOLICITUD;
  protected readonly tamano = 15;

  protected readonly estado = signal('PENDIENTE');
  protected readonly pagina = signal(1);
  protected readonly total = signal(0);
  protected readonly items = signal<readonly SolicitudVacacionesBandeja[]>([]);
  protected readonly cargando = signal(true);

  protected readonly seleccion = signal<SolicitudVacacionesBandeja | null>(null);
  protected readonly conflictos = signal<ConflictosSolicitudVacaciones | null>(null);
  protected readonly cargandoConflictos = signal(false);
  protected readonly confirmarAnulacion = signal(false);
  protected readonly motivo = signal('');
  protected readonly errorMotivo = signal<string | null>(null);
  protected readonly enviando = signal(false);

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.api
      .listarVacaciones({ estado: this.estado(), pageNumber: this.pagina(), pageSize: this.tamano })
      .subscribe({
        next: (r) => {
          this.items.set(r.items);
          this.total.set(r.totalCount);
          this.cargando.set(false);
        },
        error: (err: unknown) => {
          this.cargando.set(false);
          this.toast.error(ApiClient.extraerMensajeError(err).general);
        },
      });
  }

  filtrar(estado: string): void {
    this.estado.set(estado);
    this.pagina.set(1);
    this.cargar();
  }

  irA(p: number): void {
    this.pagina.set(p);
    this.cargar();
  }

  revisar(b: SolicitudVacacionesBandeja): void {
    this.seleccion.set(b);
    this.motivo.set('');
    this.errorMotivo.set(null);
    this.confirmarAnulacion.set(false);
    this.conflictos.set(null);
    this.cargandoConflictos.set(true);
    this.api.conflictosVacaciones(b.solicitud.idSolicitudVacaciones).subscribe({
      next: (c) => {
        this.conflictos.set(c);
        this.cargandoConflictos.set(false);
      },
      error: () => this.cargandoConflictos.set(false),
    });
  }

  resolver(aprobar: boolean): void {
    const b = this.seleccion();
    if (!b || this.enviando()) return;
    const motivo = this.motivo().trim();
    if (!aprobar && motivo.length < 5) {
      this.errorMotivo.set('Explique el motivo del rechazo (mínimo 5 caracteres).');
      return;
    }
    if (aprobar && (this.conflictos()?.turnos?.length ?? 0) > 0 && !this.confirmarAnulacion()) {
      this.toast.alerta('Marque "Anular estos turnos al aprobar" para continuar.');
      return;
    }
    this.errorMotivo.set(null);
    this.enviando.set(true);
    this.api
      .resolverVacaciones(b.solicitud.idSolicitudVacaciones, {
        aprobar,
        motivoRechazo: aprobar ? null : motivo,
        confirmarAnulacionTurnos: this.confirmarAnulacion(),
      })
      .subscribe({
        next: () => {
          this.enviando.set(false);
          this.seleccion.set(null);
          this.toast.exito(`Vacaciones de ${b.solicitud.nombreEmpleado} ${aprobar ? 'aprobadas' : 'rechazadas'}.`);
          this.cambioPendientes.emit();
          this.cargar();
        },
        error: (err: unknown) => {
          this.enviando.set(false);
          this.toast.error(ApiClient.extraerMensajeError(err).general);
        },
      });
  }
}
