import { ChangeDetectionStrategy, Component, OnInit, inject, output, signal } from '@angular/core';
import { ApiClient, NotificationService } from '../../../../app/core';
import { ESTADOS_SOLICITUD, SolicitudCambioTurno } from '../../../../app/shared/data/modelos';
import { SolicitudesAdminApi } from '../../../../app/shared/data/apis';
import {
  UiBadgeComponent,
  UiButtonComponent,
  UiDialogComponent,
  UiNotchedTextareaComponent,
  UiPaginadorComponent,
  UiSkeletonComponent,
} from '../../../../app/shared/ui';
import { formatearFecha, formatearFechaHora } from '../../../../app/shared/utils/formato';

/**
 * Bandeja de turnos.solicitud_cambio_turno (intercambio entre compañeros).
 * Al aprobar, la API intercambia las asignaciones revisando conflictos de ambos.
 */
@Component({
  selector: 'app-cambios-turno-bandeja',
  imports: [
    UiBadgeComponent,
    UiButtonComponent,
    UiDialogComponent,
    UiNotchedTextareaComponent,
    UiPaginadorComponent,
    UiSkeletonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mb-3 flex flex-wrap items-center gap-3">
      <label for="cam-estado" class="text-sm font-semibold text-slate-700">Estado</label>
      <select id="cam-estado" class="filtro-select" (change)="filtrar($any($event.target).value)">
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
              <th scope="col">Solicitante entrega</th>
              <th scope="col">Compañero entrega</th>
              <th scope="col">Motivo</th>
              <th scope="col">Estado</th>
              <th scope="col" class="text-right">Acciones</th>
            </tr>
          </thead>
          <tbody>
            @for (c of items(); track c.idSolicitudCambio) {
              <tr>
                <td>
                  <span class="font-semibold text-slate-900">{{ c.nombreEmpleadoSolicitante }}</span>
                  <span class="block text-xs text-slate-600">{{ c.nombreTurnoSolicitante }} · {{ formatearFecha(c.fechaTurnoSolicitante) }}</span>
                </td>
                <td>
                  <span class="font-semibold text-slate-900">{{ c.nombreEmpleadoCompanero }}</span>
                  <span class="block text-xs text-slate-600">{{ c.nombreTurnoCompanero }} · {{ formatearFecha(c.fechaTurnoCompanero) }}</span>
                </td>
                <td class="max-w-xs text-sm">
                  {{ c.motivo || '—' }}
                  <span class="block text-xs text-slate-500">{{ formatearFechaHora(c.solicitadoEn) }}</span>
                </td>
                <td>
                  <ui-badge [estado]="c.codEstadoSolicitud" />
                  @if (c.motivoRechazo) { <span class="mt-1 block text-xs text-slate-600">{{ c.motivoRechazo }}</span> }
                </td>
                <td class="text-right whitespace-nowrap">
                  @if (c.codEstadoSolicitud === 'PENDIENTE') {
                    <button type="button" class="accion-exito" [disabled]="enviando()" (click)="aprobar(c)">Aprobar</button>
                    <button type="button" class="accion-peligro ml-2" (click)="abrirRechazo(c)">Rechazar</button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="5" class="py-6 text-center text-slate-600">Aún no hay registros con este filtro.</td></tr>
            }
          </tbody>
        </table>
      </div>
      <ui-paginador [pagina]="pagina()" [tamano]="tamano" [total]="total()" (cambio)="irA($event)" />
    }

    <ui-dialog
      id="dlg-rechazo-cambio"
      [abierto]="rechazando() !== null"
      titulo="Rechazar cambio de turno"
      [subtitulo]="(rechazando()?.nombreEmpleadoSolicitante ?? '') + ' ↔ ' + (rechazando()?.nombreEmpleadoCompanero ?? '')"
      (cerrar)="rechazando.set(null)"
    >
      <ui-notched-textarea
        id="cam-motivo"
        etiqueta="Motivo del rechazo"
        [obligatorio]="true"
        [valor]="motivo()"
        [error]="errorMotivo()"
        (valorCambio)="motivo.set($event)"
      />
      <ng-container acciones>
        <ui-button variante="secundario" (click)="rechazando.set(null)">Cancelar</ui-button>
        <ui-button variante="peligro" [cargando]="enviando()" (click)="rechazar()">Rechazar</ui-button>
      </ng-container>
    </ui-dialog>
  `,
})
export class CambiosTurnoBandejaComponent implements OnInit {
  private readonly api = inject(SolicitudesAdminApi);
  private readonly toast = inject(NotificationService);
  readonly cambioPendientes = output<void>();

  protected readonly formatearFecha = formatearFecha;
  protected readonly formatearFechaHora = formatearFechaHora;
  protected readonly estados = ESTADOS_SOLICITUD;
  protected readonly tamano = 15;

  protected readonly estado = signal('PENDIENTE');
  protected readonly pagina = signal(1);
  protected readonly total = signal(0);
  protected readonly items = signal<readonly SolicitudCambioTurno[]>([]);
  protected readonly cargando = signal(true);
  protected readonly enviando = signal(false);
  protected readonly rechazando = signal<SolicitudCambioTurno | null>(null);
  protected readonly motivo = signal('');
  protected readonly errorMotivo = signal<string | null>(null);

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.api
      .listarCambios({ estado: this.estado(), pageNumber: this.pagina(), pageSize: this.tamano })
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

  aprobar(c: SolicitudCambioTurno): void {
    this.resolver(c, true, null);
  }

  abrirRechazo(c: SolicitudCambioTurno): void {
    this.motivo.set('');
    this.errorMotivo.set(null);
    this.rechazando.set(c);
  }

  rechazar(): void {
    const c = this.rechazando();
    if (!c) return;
    const motivo = this.motivo().trim();
    if (motivo.length < 5) {
      this.errorMotivo.set('Explique el motivo (mínimo 5 caracteres).');
      return;
    }
    this.resolver(c, false, motivo);
  }

  private resolver(c: SolicitudCambioTurno, aprobar: boolean, motivo: string | null): void {
    if (this.enviando()) return;
    this.enviando.set(true);
    this.api.resolverCambio(c.idSolicitudCambio, aprobar, motivo).subscribe({
      next: () => {
        this.enviando.set(false);
        this.rechazando.set(null);
        this.toast.exito(aprobar ? 'Cambio aprobado: los turnos fueron intercambiados.' : 'Cambio de turno rechazado.');
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
