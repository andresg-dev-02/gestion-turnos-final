import { ChangeDetectionStrategy, Component, OnInit, computed, inject, output, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { ApiClient, NotificationService } from '../../../../app/core';
import { Area, Cargo, CatalogoItem, ESTADOS_SOLICITUD, SolicitudRegistro } from '../../../../app/shared/data/modelos';
import { OrganizacionApi, ParametrizacionApi, SolicitudesAdminApi } from '../../../../app/shared/data/apis';
import {
  OpcionSelect,
  UiBadgeComponent,
  UiButtonComponent,
  UiDialogComponent,
  UiNotchedFieldComponent,
  UiNotchedSelectComponent,
  UiNotchedTextareaComponent,
  UiPaginadorComponent,
  UiSkeletonComponent,
} from '../../../../app/shared/ui';
import { formatearFechaHora, hoyIso } from '../../../../app/shared/utils/formato';

/**
 * Bandeja de seguridad.solicitud_registro. Aprobar crea el usuario (rol Empleado)
 * y su empleado en la misma transacción; por eso se piden área, cargo y fecha de ingreso.
 */
@Component({
  selector: 'app-registro-bandeja',
  imports: [
    UiBadgeComponent,
    UiButtonComponent,
    UiDialogComponent,
    UiNotchedFieldComponent,
    UiNotchedSelectComponent,
    UiNotchedTextareaComponent,
    UiPaginadorComponent,
    UiSkeletonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mb-3 flex flex-wrap items-center gap-3">
      <label for="reg-estado" class="text-sm font-semibold text-slate-700">Estado</label>
      <select id="reg-estado" class="filtro-select" (change)="filtrar($any($event.target).value)">
        @for (e of estados; track e.valor) {
          <option [value]="e.valor" [selected]="e.valor === estado()">{{ e.etiqueta }}</option>
        }
      </select>
      <input
        type="search"
        aria-label="Buscar por nombre, documento o correo"
        placeholder="Buscar nombre, documento o correo"
        class="filtro-select min-w-64 flex-1"
        (keyup.enter)="buscar($any($event.target).value)"
        (search)="buscar($any($event.target).value)"
      />
    </div>

    @if (cargando()) {
      <ui-skeleton />
    } @else {
      <div class="tabla-contenedor">
        <table class="tabla">
          <thead>
            <tr>
              <th scope="col">Solicitante</th>
              <th scope="col">Documento</th>
              <th scope="col">Contacto</th>
              <th scope="col">Solicitado</th>
              <th scope="col">Estado</th>
              <th scope="col" class="text-right">Acciones</th>
            </tr>
          </thead>
          <tbody>
            @for (s of items(); track s.idSolicitudRegistro) {
              <tr>
                <td class="font-semibold text-slate-900">{{ s.nombres }} {{ s.apellidos }}</td>
                <td class="whitespace-nowrap">{{ s.codTipoDocumento }} {{ s.numeroDocumento }}</td>
                <td>
                  {{ s.correo }}
                  @if (s.telefono) { <span class="block text-xs text-slate-600">{{ s.telefono }}</span> }
                </td>
                <td class="whitespace-nowrap">{{ formatearFechaHora(s.solicitadoEn) }}</td>
                <td>
                  <ui-badge [estado]="s.codEstadoSolicitud" />
                  @if (s.motivoRechazo) { <span class="mt-1 block text-xs text-slate-600">{{ s.motivoRechazo }}</span> }
                </td>
                <td class="text-right whitespace-nowrap">
                  @if (s.codEstadoSolicitud === 'PENDIENTE') {
                    <button type="button" class="accion-exito" (click)="abrirAprobar(s)">Aprobar</button>
                    <button type="button" class="accion-peligro ml-2" (click)="abrirRechazar(s)">Rechazar</button>
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
      id="dlg-aprobar-registro"
      [abierto]="aprobando() !== null"
      titulo="Aprobar solicitud de registro"
      [subtitulo]="(aprobando()?.nombres ?? '') + ' ' + (aprobando()?.apellidos ?? '') + ' · ' + (aprobando()?.correo ?? '')"
      (cerrar)="aprobando.set(null)"
    >
      <p class="mb-4 text-sm text-slate-700">
        Se creará el usuario con rol Empleado y su ficha de empleado. Se le notificará por correo.
      </p>
      <div class="space-y-5">
        <ui-notched-select id="apr-area" etiqueta="Área" [opciones]="opcionesArea()" [valor]="idArea()" [obligatorio]="true" [error]="errores()['idArea']" (valorCambio)="idArea.set($event)" />
        <ui-notched-select id="apr-cargo" etiqueta="Cargo" [opciones]="opcionesCargo()" [valor]="idCargo()" [obligatorio]="true" [error]="errores()['idCargo']" (valorCambio)="idCargo.set($event)" />
        <ui-notched-select id="apr-contrato" etiqueta="Tipo de contrato" [opciones]="opcionesContrato()" [valor]="codTipoContrato()" (valorCambio)="codTipoContrato.set($event)" />
        <ui-notched-field id="apr-ingreso" etiqueta="Fecha de ingreso" tipo="date" [valor]="fechaIngreso()" [obligatorio]="true" [error]="errores()['fechaIngreso']" (valorCambio)="fechaIngreso.set($event)" />
      </div>
      <ng-container acciones>
        <ui-button variante="secundario" (click)="aprobando.set(null)">Cancelar</ui-button>
        <ui-button [cargando]="enviando()" (click)="aprobar()">Aprobar y crear usuario</ui-button>
      </ng-container>
    </ui-dialog>

    <ui-dialog
      id="dlg-rechazar-registro"
      [abierto]="rechazando() !== null"
      titulo="Rechazar solicitud de registro"
      [subtitulo]="(rechazando()?.nombres ?? '') + ' ' + (rechazando()?.apellidos ?? '')"
      (cerrar)="rechazando.set(null)"
    >
      <ui-notched-textarea
        id="rec-motivo"
        etiqueta="Motivo del rechazo"
        [valor]="motivo()"
        [obligatorio]="true"
        [error]="errores()['motivoRechazo']"
        (valorCambio)="motivo.set($event)"
      />
      <p class="mt-2 text-xs text-slate-600">El solicitante verá este motivo al intentar ingresar.</p>
      <ng-container acciones>
        <ui-button variante="secundario" (click)="rechazando.set(null)">Cancelar</ui-button>
        <ui-button variante="peligro" [cargando]="enviando()" (click)="rechazar()">Rechazar</ui-button>
      </ng-container>
    </ui-dialog>
  `,
})
export class RegistroBandejaComponent implements OnInit {
  private readonly api = inject(SolicitudesAdminApi);
  private readonly org = inject(OrganizacionApi);
  private readonly param = inject(ParametrizacionApi);
  private readonly toast = inject(NotificationService);
  readonly cambioPendientes = output<void>();

  protected readonly formatearFechaHora = formatearFechaHora;
  protected readonly estados = ESTADOS_SOLICITUD;
  protected readonly tamano = 15;

  protected readonly estado = signal('PENDIENTE');
  protected readonly busqueda = signal('');
  protected readonly pagina = signal(1);
  protected readonly total = signal(0);
  protected readonly items = signal<readonly SolicitudRegistro[]>([]);
  protected readonly cargando = signal(true);
  protected readonly enviando = signal(false);
  protected readonly errores = signal<Record<string, string>>({});

  private readonly areas = signal<readonly Area[]>([]);
  private readonly cargos = signal<readonly Cargo[]>([]);
  private readonly contratos = signal<readonly CatalogoItem[]>([]);
  protected readonly opcionesArea = computed<readonly OpcionSelect[]>(() => [
    { valor: '', etiqueta: 'Seleccione un área' },
    ...this.areas().map((a) => ({ valor: String(a.idArea), etiqueta: `${a.nombre} · ${a.nombreSucursal}` })),
  ]);
  protected readonly opcionesCargo = computed<readonly OpcionSelect[]>(() => [
    { valor: '', etiqueta: 'Seleccione un cargo' },
    ...this.cargos().map((c) => ({ valor: String(c.idCargo), etiqueta: c.nombre })),
  ]);
  protected readonly opcionesContrato = computed<readonly OpcionSelect[]>(() => [
    { valor: '', etiqueta: 'Sin especificar' },
    ...this.contratos().map((c) => ({ valor: c.codigo, etiqueta: c.nombre })),
  ]);

  protected readonly aprobando = signal<SolicitudRegistro | null>(null);
  protected readonly rechazando = signal<SolicitudRegistro | null>(null);
  protected readonly idArea = signal('');
  protected readonly idCargo = signal('');
  protected readonly codTipoContrato = signal('');
  protected readonly fechaIngreso = signal(hoyIso());
  protected readonly motivo = signal('');

  ngOnInit(): void {
    this.cargar();
    forkJoin({
      areas: this.org.listarAreas(),
      cargos: this.org.listarCargos(),
      contratos: this.param.listarTiposContrato(true),
    }).subscribe({
      next: ({ areas, cargos, contratos }) => {
        this.areas.set(areas);
        this.cargos.set(cargos);
        this.contratos.set(contratos);
      },
      error: () => this.toast.alerta('No se pudieron cargar áreas, cargos o tipos de contrato.'),
    });
  }

  cargar(): void {
    this.cargando.set(true);
    this.api
      .listarRegistros({
        estado: this.estado(),
        busqueda: this.busqueda(),
        pageNumber: this.pagina(),
        pageSize: this.tamano,
      })
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

  buscar(texto: string): void {
    this.busqueda.set(texto.trim());
    this.pagina.set(1);
    this.cargar();
  }

  irA(p: number): void {
    this.pagina.set(p);
    this.cargar();
  }

  abrirAprobar(s: SolicitudRegistro): void {
    this.idArea.set('');
    this.idCargo.set('');
    this.codTipoContrato.set('');
    this.fechaIngreso.set(hoyIso());
    this.errores.set({});
    this.aprobando.set(s);
  }

  abrirRechazar(s: SolicitudRegistro): void {
    this.motivo.set('');
    this.errores.set({});
    this.rechazando.set(s);
  }

  aprobar(): void {
    const s = this.aprobando();
    if (!s || this.enviando()) return;
    const errores: Record<string, string> = {};
    if (!this.idArea()) errores['idArea'] = 'Seleccione el área.';
    if (!this.idCargo()) errores['idCargo'] = 'Seleccione el cargo.';
    if (!this.fechaIngreso()) errores['fechaIngreso'] = 'La fecha de ingreso es obligatoria.';
    this.errores.set(errores);
    if (Object.keys(errores).length) return;

    this.enviando.set(true);
    this.api
      .aprobarRegistro(s.idSolicitudRegistro, {
        idArea: Number(this.idArea()),
        idCargo: Number(this.idCargo()),
        fechaIngreso: this.fechaIngreso(),
        codTipoContrato: this.codTipoContrato() || null,
      })
      .subscribe({
        next: () => this.terminar(`Solicitud de ${s.nombres} aprobada. Usuario creado.`),
        error: (err: unknown) => this.fallo(err),
      });
  }

  rechazar(): void {
    const s = this.rechazando();
    if (!s || this.enviando()) return;
    const motivo = this.motivo().trim();
    if (motivo.length < 5) {
      this.errores.set({ motivoRechazo: 'Explique el motivo (mínimo 5 caracteres).' });
      return;
    }
    this.enviando.set(true);
    this.api.rechazarRegistro(s.idSolicitudRegistro, motivo).subscribe({
      next: () => this.terminar(`Solicitud de ${s.nombres} rechazada.`),
      error: (err: unknown) => this.fallo(err),
    });
  }

  private terminar(mensaje: string): void {
    this.enviando.set(false);
    this.aprobando.set(null);
    this.rechazando.set(null);
    this.toast.exito(mensaje);
    this.cambioPendientes.emit();
    this.cargar();
  }

  private fallo(err: unknown): void {
    this.enviando.set(false);
    const e = ApiClient.extraerMensajeError(err);
    this.errores.set(e.campos);
    this.toast.error(e.general);
  }
}
