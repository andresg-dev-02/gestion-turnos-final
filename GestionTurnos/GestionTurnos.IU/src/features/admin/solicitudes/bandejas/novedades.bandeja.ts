import { ChangeDetectionStrategy, Component, OnInit, inject, output, signal } from '@angular/core';
import { ApiClient, NotificationService } from '../../../../app/core';
import { ESTADOS_SOLICITUD, Novedad } from '../../../../app/shared/data/modelos';
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
import { formatearFechaHora } from '../../../../app/shared/utils/formato';

/** Bandeja de novedades.novedad: permisos, incapacidades, calamidades y ausencias. */
@Component({
  selector: 'app-novedades-bandeja',
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
      <label for="nov-estado" class="text-sm font-semibold text-slate-700">Estado</label>
      <select id="nov-estado" class="filtro-select" (change)="filtrar($any($event.target).value)">
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
              <th scope="col">Tipo</th>
              <th scope="col">Desde / hasta</th>
              <th scope="col">Condiciones</th>
              <th scope="col">Estado</th>
              <th scope="col" class="text-right">Acciones</th>
            </tr>
          </thead>
          <tbody>
            @for (n of items(); track n.idNovedad) {
              <tr>
                <td class="font-semibold text-slate-900">{{ n.nombreEmpleado }}</td>
                <td>
                  {{ n.nombreTipoNovedad }}
                  @if (n.observaciones) { <span class="block text-xs text-slate-600">{{ n.observaciones }}</span> }
                  @if (n.urlSoporte) {
                    <a [href]="n.urlSoporte" target="_blank" rel="noopener noreferrer" class="block text-xs font-semibold text-marca-700 underline">Ver soporte</a>
                  }
                </td>
                <td class="whitespace-nowrap">
                  {{ formatearFechaHora(n.inicio) }}<br />
                  <span class="text-slate-600">{{ formatearFechaHora(n.fin) }}</span>
                </td>
                <td class="text-xs">
                  {{ n.remunerado ? 'Remunerada' : 'No remunerada' }}<br />
                  {{ n.afectaHoras ? 'Afecta horas' : 'No afecta horas' }}
                </td>
                <td>
                  <ui-badge [estado]="n.codEstadoSolicitud" />
                  @if (n.motivoRechazo) { <span class="mt-1 block text-xs text-slate-600">{{ n.motivoRechazo }}</span> }
                </td>
                <td class="text-right whitespace-nowrap">
                  @if (n.codEstadoSolicitud === 'PENDIENTE') {
                    <button type="button" class="accion-primaria" (click)="abrir(n)">Resolver</button>
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
      id="dlg-novedad"
      [abierto]="seleccion() !== null"
      titulo="Resolver novedad"
      [subtitulo]="(seleccion()?.nombreTipoNovedad ?? '') + ' · ' + (seleccion()?.nombreEmpleado ?? '')"
      (cerrar)="seleccion.set(null)"
    >
      @if (seleccion(); as n) {
        <p class="text-sm text-slate-700">
          Del <strong>{{ formatearFechaHora(n.inicio) }}</strong> al <strong>{{ formatearFechaHora(n.fin) }}</strong>.
        </p>
        <div class="mt-3">
          <ui-check
            id="nov-confirmar"
            etiqueta="Anular los turnos que se crucen"
            ayuda="Si el empleado tiene turnos en ese horario, la aprobación los anula."
            [marcado]="confirmarAnulacion()"
            (marcadoCambio)="confirmarAnulacion.set($event)"
          />
        </div>
        <div class="mt-4">
          <ui-notched-textarea
            id="nov-motivo"
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
export class NovedadesBandejaComponent implements OnInit {
  private readonly api = inject(SolicitudesAdminApi);
  private readonly toast = inject(NotificationService);
  readonly cambioPendientes = output<void>();

  protected readonly formatearFechaHora = formatearFechaHora;
  protected readonly estados = ESTADOS_SOLICITUD;
  protected readonly tamano = 15;

  protected readonly estado = signal('PENDIENTE');
  protected readonly pagina = signal(1);
  protected readonly total = signal(0);
  protected readonly items = signal<readonly Novedad[]>([]);
  protected readonly cargando = signal(true);
  protected readonly seleccion = signal<Novedad | null>(null);
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
      .listarNovedades({ estado: this.estado(), pageNumber: this.pagina(), pageSize: this.tamano })
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

  abrir(n: Novedad): void {
    this.motivo.set('');
    this.errorMotivo.set(null);
    this.confirmarAnulacion.set(false);
    this.seleccion.set(n);
  }

  resolver(aprobar: boolean): void {
    const n = this.seleccion();
    if (!n || this.enviando()) return;
    const motivo = this.motivo().trim();
    if (!aprobar && motivo.length < 5) {
      this.errorMotivo.set('Explique el motivo del rechazo (mínimo 5 caracteres).');
      return;
    }
    this.enviando.set(true);
    this.api
      .resolverNovedad(n.idNovedad, {
        aprobar,
        motivoRechazo: aprobar ? null : motivo,
        confirmarAnulacionTurnos: this.confirmarAnulacion(),
      })
      .subscribe({
        next: () => {
          this.enviando.set(false);
          this.seleccion.set(null);
          this.toast.exito(`Novedad de ${n.nombreEmpleado} ${aprobar ? 'aprobada' : 'rechazada'}.`);
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
