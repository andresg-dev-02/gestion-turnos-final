import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient, NotificationService } from '../../../../app/core';
import { CatalogoItem } from '../../../../app/shared/data/modelos';
import { ParametrizacionApi } from '../../../../app/shared/data/apis';
import {
  UiBadgeComponent,
  UiButtonComponent,
  UiCheckComponent,
  UiDialogComponent,
  UiNotchedFieldComponent,
  UiSkeletonComponent,
} from '../../../../app/shared/ui';

type TipoCatalogo = 'documento' | 'contrato';

const CONFIG: Record<TipoCatalogo, { titulo: string; maxCodigo: number; ejemplo: string }> = {
  documento: { titulo: 'tipo de documento', maxCodigo: 10, ejemplo: 'CC' },
  contrato: { titulo: 'tipo de contrato', maxCodigo: 20, ejemplo: 'INDEFINIDO' },
};

/**
 * CRUD de catálogos simples (código, nombre, activo):
 * parametrizacion.tipo_documento y parametrizacion.tipo_contrato.
 * La API hace upsert por código; "eliminar" = desactivar (no se borra por integridad referencial).
 */
@Component({
  selector: 'app-catalogo-simple-section',
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
        {{ items().length }} registros. Los registros en uso no se borran: se desactivan.
      </p>
      <ui-button (click)="nuevo()">Nuevo {{ cfg().titulo }}</ui-button>
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
              <th scope="col">Estado</th>
              <th scope="col" class="text-right">Acciones</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items(); track item.codigo) {
              <tr>
                <td class="font-mono font-semibold">{{ item.codigo }}</td>
                <td>{{ item.nombre }}</td>
                <td><ui-badge [estado]="item.activo ? 'ACTIVO' : 'INACTIVO'" /></td>
                <td class="text-right whitespace-nowrap">
                  <button type="button" class="accion-secundaria" (click)="editar(item)">Editar</button>
                  <button
                    type="button"
                    [class]="item.activo ? 'accion-peligro ml-2' : 'accion-exito ml-2'"
                    (click)="alternarActivo(item)"
                  >
                    {{ item.activo ? 'Desactivar' : 'Activar' }}
                  </button>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="4" class="py-6 text-center text-slate-600">Aún no hay registros.</td></tr>
            }
          </tbody>
        </table>
      </div>
    }

    <ui-dialog
      [id]="'dlg-' + tipo()"
      [abierto]="dialogoAbierto()"
      [titulo]="(esEdicion() ? 'Editar ' : 'Nuevo ') + cfg().titulo"
      (cerrar)="dialogoAbierto.set(false)"
    >
      <form id="form-{{ tipo() }}" class="space-y-5" (submit)="guardar($event)">
        <ui-notched-field
          [id]="tipo() + '-codigo'"
          etiqueta="Código"
          [valor]="codigo()"
          [deshabilitado]="esEdicion()"
          [obligatorio]="true"
          [placeholder]="cfg().ejemplo"
          [ayuda]="'Máximo ' + cfg().maxCodigo + ' caracteres, en mayúsculas. No se puede cambiar después.'"
          [error]="errores()['codigo']"
          (valorCambio)="codigo.set($event.toUpperCase())"
        />
        <ui-notched-field
          [id]="tipo() + '-nombre'"
          etiqueta="Nombre"
          [valor]="nombre()"
          [obligatorio]="true"
          [error]="errores()['nombre']"
          (valorCambio)="nombre.set($event)"
        />
        <ui-check
          [id]="tipo() + '-activo'"
          etiqueta="Activo"
          ayuda="Solo los registros activos aparecen en los formularios."
          [marcado]="activo()"
          (marcadoCambio)="activo.set($event)"
        />
      </form>
      <ng-container acciones>
        <ui-button variante="secundario" (click)="dialogoAbierto.set(false)">Cancelar</ui-button>
        <ui-button [cargando]="guardando()" (click)="guardar($event)">
          Guardar
        </ui-button>
      </ng-container>
    </ui-dialog>
  `,
})
export class CatalogoSimpleSectionComponent implements OnInit {
  readonly tipo = input.required<TipoCatalogo>();

  private readonly api = inject(ParametrizacionApi);
  private readonly toast = inject(NotificationService);

  protected readonly cfg = computed(() => CONFIG[this.tipo()]);
  protected readonly items = signal<readonly CatalogoItem[]>([]);
  protected readonly cargando = signal(true);
  protected readonly dialogoAbierto = signal(false);
  protected readonly esEdicion = signal(false);
  protected readonly guardando = signal(false);
  protected readonly codigo = signal('');
  protected readonly nombre = signal('');
  protected readonly activo = signal(true);
  protected readonly errores = signal<Record<string, string>>({});

  ngOnInit(): void {
    this.cargar();
  }

  private listar(): Observable<readonly CatalogoItem[]> {
    return this.tipo() === 'documento' ? this.api.listarTiposDocumento() : this.api.listarTiposContrato();
  }

  private enviar(item: CatalogoItem): Observable<void> {
    return this.tipo() === 'documento'
      ? this.api.guardarTipoDocumento(item)
      : this.api.guardarTipoContrato(item);
  }

  cargar(): void {
    this.cargando.set(true);
    this.listar().subscribe({
      next: (lista) => {
        this.items.set(lista);
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  nuevo(): void {
    this.esEdicion.set(false);
    this.codigo.set('');
    this.nombre.set('');
    this.activo.set(true);
    this.errores.set({});
    this.dialogoAbierto.set(true);
  }

  editar(item: CatalogoItem): void {
    this.esEdicion.set(true);
    this.codigo.set(item.codigo);
    this.nombre.set(item.nombre);
    this.activo.set(item.activo);
    this.errores.set({});
    this.dialogoAbierto.set(true);
  }

  guardar(evento: Event): void {
    evento.preventDefault();
    if (this.guardando()) return;
    const codigo = this.codigo().trim();
    const nombre = this.nombre().trim();
    const errores: Record<string, string> = {};
    if (!codigo) errores['codigo'] = 'El código es obligatorio.';
    else if (codigo.length > this.cfg().maxCodigo)
      errores['codigo'] = `Máximo ${this.cfg().maxCodigo} caracteres.`;
    if (!nombre) errores['nombre'] = 'El nombre es obligatorio.';
    else if (nombre.length > 60) errores['nombre'] = 'Máximo 60 caracteres.';
    this.errores.set(errores);
    if (Object.keys(errores).length) return;

    if (!this.esEdicion() && this.items().some((i) => i.codigo === codigo)) {
      this.errores.set({ codigo: 'Ya existe un registro con ese código.' });
      return;
    }

    this.guardando.set(true);
    this.enviar({ codigo, nombre, activo: this.activo() }).subscribe({
      next: () => {
        this.guardando.set(false);
        this.dialogoAbierto.set(false);
        this.toast.exito(`El ${this.cfg().titulo} se guardó correctamente.`);
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

  alternarActivo(item: CatalogoItem): void {
    this.enviar({ ...item, activo: !item.activo }).subscribe({
      next: () => {
        this.toast.exito(`"${item.nombre}" quedó ${item.activo ? 'inactivo' : 'activo'}.`);
        this.cargar();
      },
      error: (err: unknown) => this.toast.error(ApiClient.extraerMensajeError(err).general),
    });
  }
}
