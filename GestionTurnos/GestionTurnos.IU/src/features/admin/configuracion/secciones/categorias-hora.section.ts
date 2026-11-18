import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ApiClient, NotificationService } from '../../../../app/core';
import { CategoriaHora } from '../../../../app/shared/data/modelos';
import { ParametrizacionApi } from '../../../../app/shared/data/apis';
import {
  UiBadgeComponent,
  UiButtonComponent,
  UiCheckComponent,
  UiDialogComponent,
  UiNotchedFieldComponent,
  UiSkeletonComponent,
} from '../../../../app/shared/ui';

/** CRUD de parametrizacion.categoria_hora (ordinaria, recargos, extras) y su factor de pago. */
@Component({
  selector: 'app-categorias-hora-section',
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
        El factor de pago multiplica el valor de la hora ordinaria en la liquidación.
      </p>
      <ui-button (click)="abrir(null)">Nueva categoría</ui-button>
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
              <th scope="col">Factor de pago</th>
              <th scope="col">Estado</th>
              <th scope="col" class="text-right">Acciones</th>
            </tr>
          </thead>
          <tbody>
            @for (c of items(); track c.codCategoriaHora) {
              <tr>
                <td class="font-mono font-semibold">{{ c.codCategoriaHora }}</td>
                <td>{{ c.nombre }}</td>
                <td class="font-mono">× {{ c.factorPago.toFixed(2) }}</td>
                <td><ui-badge [estado]="c.activo ? 'ACTIVO' : 'INACTIVO'" /></td>
                <td class="text-right whitespace-nowrap">
                  <button type="button" class="accion-secundaria" (click)="abrir(c)">Editar</button>
                  <button
                    type="button"
                    [class]="c.activo ? 'accion-peligro ml-2' : 'accion-exito ml-2'"
                    (click)="enviar({ ...c, activo: !c.activo }, false)"
                  >
                    {{ c.activo ? 'Desactivar' : 'Activar' }}
                  </button>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="5" class="py-6 text-center text-slate-600">Aún no hay registros.</td></tr>
            }
          </tbody>
        </table>
      </div>
    }

    <ui-dialog
      id="dlg-categoria-hora"
      [abierto]="abierto()"
      [titulo]="edicion() ? 'Editar categoría de hora' : 'Nueva categoría de hora'"
      (cerrar)="abierto.set(false)"
    >
      <form class="space-y-5" (submit)="guardar($event)">
        <ui-notched-field
          id="ch-codigo"
          etiqueta="Código"
          placeholder="EXTRA_DOMINICAL"
          [valor]="codigo()"
          [deshabilitado]="edicion()"
          [obligatorio]="true"
          [error]="errores()['codCategoriaHora']"
          (valorCambio)="codigo.set($event.toUpperCase())"
        />
        <ui-notched-field
          id="ch-nombre"
          etiqueta="Nombre"
          [valor]="nombre()"
          [obligatorio]="true"
          [error]="errores()['nombre']"
          (valorCambio)="nombre.set($event)"
        />
        <ui-notched-field
          id="ch-factor"
          etiqueta="Factor de pago"
          tipo="number"
          min="0"
          paso="0.01"
          [valor]="factor()"
          [obligatorio]="true"
          ayuda="Ejemplo: 1.35 para un recargo del 35 %."
          [error]="errores()['factorPago']"
          (valorCambio)="factor.set($event)"
        />
        <ui-check id="ch-activo" etiqueta="Activo" [marcado]="activo()" (marcadoCambio)="activo.set($event)" />
      </form>
      <ng-container acciones>
        <ui-button variante="secundario" (click)="abierto.set(false)">Cancelar</ui-button>
        <ui-button [cargando]="guardando()" (click)="guardar($event)">Guardar</ui-button>
      </ng-container>
    </ui-dialog>
  `,
})
export class CategoriasHoraSectionComponent implements OnInit {
  private readonly api = inject(ParametrizacionApi);
  private readonly toast = inject(NotificationService);

  protected readonly items = signal<readonly CategoriaHora[]>([]);
  protected readonly cargando = signal(true);
  protected readonly abierto = signal(false);
  protected readonly edicion = signal(false);
  protected readonly guardando = signal(false);
  protected readonly codigo = signal('');
  protected readonly nombre = signal('');
  protected readonly factor = signal('1.00');
  protected readonly activo = signal(true);
  protected readonly errores = signal<Record<string, string>>({});

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.api.listarCategoriasHora().subscribe({
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

  abrir(c: CategoriaHora | null): void {
    this.edicion.set(c !== null);
    this.codigo.set(c?.codCategoriaHora ?? '');
    this.nombre.set(c?.nombre ?? '');
    this.factor.set((c?.factorPago ?? 1).toFixed(2));
    this.activo.set(c?.activo ?? true);
    this.errores.set({});
    this.abierto.set(true);
  }

  guardar(evento: Event): void {
    evento.preventDefault();
    const codigo = this.codigo().trim();
    const nombre = this.nombre().trim();
    const factor = Number(this.factor());
    const errores: Record<string, string> = {};
    if (!codigo) errores['codCategoriaHora'] = 'El código es obligatorio.';
    else if (codigo.length > 30) errores['codCategoriaHora'] = 'Máximo 30 caracteres.';
    else if (!this.edicion() && this.items().some((i) => i.codCategoriaHora === codigo))
      errores['codCategoriaHora'] = 'Ya existe una categoría con ese código.';
    if (!nombre) errores['nombre'] = 'El nombre es obligatorio.';
    if (!Number.isFinite(factor) || factor < 0) errores['factorPago'] = 'Ingrese un número mayor o igual a 0.';
    this.errores.set(errores);
    if (Object.keys(errores).length) return;
    this.enviar({ codCategoriaHora: codigo, nombre, factorPago: factor, activo: this.activo() }, true);
  }

  enviar(c: CategoriaHora, desdeDialogo: boolean): void {
    this.guardando.set(true);
    this.api.guardarCategoriaHora(c).subscribe({
      next: () => {
        this.guardando.set(false);
        if (desdeDialogo) this.abierto.set(false);
        this.toast.exito(`Categoría "${c.nombre}" guardada.`);
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
