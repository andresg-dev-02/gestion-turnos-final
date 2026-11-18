import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ApiClient, NotificationService } from '../../../../app/core';
import { TipoNovedad } from '../../../../app/shared/data/modelos';
import { ParametrizacionApi } from '../../../../app/shared/data/apis';
import {
  UiBadgeComponent,
  UiButtonComponent,
  UiCheckComponent,
  UiDialogComponent,
  UiNotchedFieldComponent,
  UiSkeletonComponent,
} from '../../../../app/shared/ui';

const VACIO: TipoNovedad = {
  codTipoNovedad: '',
  nombre: '',
  remuneradoDefecto: false,
  afectaHorasDefecto: true,
  requiereAprobacion: true,
  requiereSoporte: false,
  activo: true,
};

/** CRUD de parametrizacion.tipo_novedad (permisos, incapacidades, ausencias...). */
@Component({
  selector: 'app-tipos-novedad-section',
  imports: [
    UiBadgeComponent,
    UiButtonComponent,
    UiCheckComponent,
    UiDialogComponent,
    UiNotchedFieldComponent,
    UiSkeletonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mb-3 flex flex-wrap items-center justify-between gap-3">
      <p class="text-sm text-slate-600">
        Define cómo se comporta cada novedad por defecto. Los valores pueden ajustarse al aprobar.
      </p>
      <ui-button (click)="abrir(null)">Nuevo tipo de novedad</ui-button>
    </div>

    @if (cargando()) {
      <ui-skeleton />
    } @else {
      <div class="tabla-contenedor">
        <table class="tabla">
          <thead>
            <tr>
              <th scope="col">Código</th>
              <th scope="col">Nombre</th>
              <th scope="col">Remunerado</th>
              <th scope="col">Afecta horas</th>
              <th scope="col">Requiere aprobación</th>
              <th scope="col">Requiere soporte</th>
              <th scope="col">Estado</th>
              <th scope="col" class="text-right">Acciones</th>
            </tr>
          </thead>
          <tbody>
            @for (t of items(); track t.codTipoNovedad) {
              <tr>
                <td class="font-mono font-semibold">{{ t.codTipoNovedad }}</td>
                <td>{{ t.nombre }}</td>
                <td>{{ t.remuneradoDefecto ? 'Sí' : 'No' }}</td>
                <td>{{ t.afectaHorasDefecto ? 'Sí' : 'No' }}</td>
                <td>{{ t.requiereAprobacion ? 'Sí' : 'No' }}</td>
                <td>{{ t.requiereSoporte ? 'Sí' : 'No' }}</td>
                <td><ui-badge [estado]="t.activo ? 'ACTIVO' : 'INACTIVO'" /></td>
                <td class="text-right whitespace-nowrap">
                  <button type="button" class="accion-secundaria" (click)="abrir(t)">Editar</button>
                  <button
                    type="button"
                    [class]="t.activo ? 'accion-peligro ml-2' : 'accion-exito ml-2'"
                    (click)="enviar({ ...t, activo: !t.activo }, false)"
                  >
                    {{ t.activo ? 'Desactivar' : 'Activar' }}
                  </button>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="8" class="py-6 text-center text-slate-600">Aún no hay registros.</td></tr>
            }
          </tbody>
        </table>
      </div>
    }

    <ui-dialog
      id="dlg-tipo-novedad"
      [abierto]="abierto()"
      [titulo]="edicion() ? 'Editar tipo de novedad' : 'Nuevo tipo de novedad'"
      (cerrar)="abierto.set(false)"
    >
      <form class="space-y-4" (submit)="guardar($event)">
        <ui-notched-field
          id="tn-codigo"
          etiqueta="Código"
          placeholder="PERMISO_ESTUDIO"
          [valor]="form().codTipoNovedad"
          [deshabilitado]="edicion()"
          [obligatorio]="true"
          [error]="errores()['codTipoNovedad']"
          ayuda="Máximo 30 caracteres en mayúsculas, sin espacios."
          (valorCambio)="set('codTipoNovedad', normalizarCodigo($event))"
        />
        <ui-notched-field
          id="tn-nombre"
          etiqueta="Nombre"
          [valor]="form().nombre"
          [obligatorio]="true"
          [error]="errores()['nombre']"
          (valorCambio)="set('nombre', $event)"
        />
        <div class="grid grid-cols-1 gap-x-4 sm:grid-cols-2">
          <ui-check id="tn-rem" etiqueta="Remunerado por defecto" [marcado]="form().remuneradoDefecto" (marcadoCambio)="set('remuneradoDefecto', $event)" />
          <ui-check id="tn-afe" etiqueta="Afecta horas por defecto" [marcado]="form().afectaHorasDefecto" (marcadoCambio)="set('afectaHorasDefecto', $event)" />
          <ui-check id="tn-apr" etiqueta="Requiere aprobación" [marcado]="form().requiereAprobacion" (marcadoCambio)="set('requiereAprobacion', $event)" />
          <ui-check id="tn-sop" etiqueta="Requiere soporte" [marcado]="form().requiereSoporte" (marcadoCambio)="set('requiereSoporte', $event)" />
          <ui-check id="tn-act" etiqueta="Activo" [marcado]="form().activo" (marcadoCambio)="set('activo', $event)" />
        </div>
      </form>
      <ng-container acciones>
        <ui-button variante="secundario" (click)="abierto.set(false)">Cancelar</ui-button>
        <ui-button [cargando]="guardando()" (click)="guardar($event)">Guardar</ui-button>
      </ng-container>
    </ui-dialog>
  `,
})
export class TiposNovedadSectionComponent implements OnInit {
  private readonly api = inject(ParametrizacionApi);
  private readonly toast = inject(NotificationService);

  protected readonly items = signal<readonly TipoNovedad[]>([]);
  protected readonly cargando = signal(true);
  protected readonly abierto = signal(false);
  protected readonly edicion = signal(false);
  protected readonly guardando = signal(false);
  protected readonly form = signal<TipoNovedad>(VACIO);
  protected readonly errores = signal<Record<string, string>>({});

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.api.listarTiposNovedad().subscribe({
      next: (l) => {
        this.items.set(l);
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  set<K extends keyof TipoNovedad>(campo: K, valor: TipoNovedad[K]): void {
    this.form.update((f) => ({ ...f, [campo]: valor }));
  }

  normalizarCodigo(valor: string): string {
    return valor.toUpperCase().replace(/\s+/g, '_');
  }

  abrir(t: TipoNovedad | null): void {
    this.edicion.set(t !== null);
    this.form.set(t ?? VACIO);
    this.errores.set({});
    this.abierto.set(true);
  }

  guardar(evento: Event): void {
    evento.preventDefault();
    const f = this.form();
    const errores: Record<string, string> = {};
    if (!f.codTipoNovedad.trim()) errores['codTipoNovedad'] = 'El código es obligatorio.';
    else if (f.codTipoNovedad.length > 30) errores['codTipoNovedad'] = 'Máximo 30 caracteres.';
    else if (!this.edicion() && this.items().some((i) => i.codTipoNovedad === f.codTipoNovedad.trim()))
      errores['codTipoNovedad'] = 'Ya existe un tipo con ese código.';
    if (!f.nombre.trim()) errores['nombre'] = 'El nombre es obligatorio.';
    else if (f.nombre.length > 80) errores['nombre'] = 'Máximo 80 caracteres.';
    this.errores.set(errores);
    if (Object.keys(errores).length) return;
    this.enviar({ ...f, codTipoNovedad: f.codTipoNovedad.trim(), nombre: f.nombre.trim() }, true);
  }

  enviar(t: TipoNovedad, desdeDialogo: boolean): void {
    this.guardando.set(true);
    this.api.guardarTipoNovedad(t).subscribe({
      next: () => {
        this.guardando.set(false);
        if (desdeDialogo) this.abierto.set(false);
        this.toast.exito(`Tipo de novedad "${t.nombre}" guardado.`);
        this.cargar();
      },
      error: (err: unknown) => {
        this.guardando.set(false);
        const e = ApiClient.extraerMensajeError(err);
        this.errores.set(e.campos);
        this.toast.error(e.general);
      },
    });
  }
}
