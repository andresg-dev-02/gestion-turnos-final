import {
  ChangeDetectionStrategy,
  Component,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RolSistema } from '../../../../app/shared/data';
import {
  UiButtonComponent,
  UiDialogComponent,
  UiNotchedFieldComponent,
  UiNotchedTextareaComponent,
} from '../../../../app/shared/ui';

export interface GuardarRolEvento {
  readonly idRol?: number | null;
  readonly nombre: string;
  readonly descripcion: string | null;
  readonly activo: boolean;
}

@Component({
  selector: 'app-rol-form-dialog',
  imports: [
    FormsModule,
    UiDialogComponent,
    UiNotchedFieldComponent,
    UiNotchedTextareaComponent,
    UiButtonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-dialog
      id="dlg-rol-form"
      [abierto]="abierto()"
      [titulo]="rol() ? 'Editar Rol' : 'Nuevo Rol Institucional'"
      [subtitulo]="rol() ? ('Configuración básica del rol: ' + rol()?.nombre) : 'Define el nombre y descripción para este nuevo rol en el sistema.'"
      (cerrar)="cerrar.emit()"
    >
      <form (submit)="enviar($event)" class="space-y-4">
        <ui-notched-field
          id="rol-nombre"
          etiqueta="Nombre del rol"
          [valor]="nombre()"
          [obligatorio]="true"
          (valorCambio)="nombre.set($event)"
        />

        <ui-notched-textarea
          id="rol-descripcion"
          etiqueta="Descripción del rol"
          [valor]="descripcion()"
          (valorCambio)="descripcion.set($event)"
        />

        <div class="flex items-center gap-2 pt-1">
          <input
            id="rol-activo"
            type="checkbox"
            class="h-4 w-4 rounded border-slate-300 text-marca-600 focus:ring-marca-500"
            [ngModel]="activo()"
            (ngModelChange)="activo.set($event)"
            name="rol-activo"
          />
          <label for="rol-activo" class="text-xs font-semibold text-slate-700">
            Rol activo para asignación de usuarios
          </label>
        </div>

        <div class="mt-6 flex justify-end gap-3 pt-3">
          <button
            type="button"
            class="accion-secundaria"
            (click)="cerrar.emit()"
          >
            Cancelar
          </button>
          <ui-button
            tipo="submit"
            [deshabilitado]="guardando() || !formularioValido()"
          >
            {{ guardando() ? 'Guardando...' : (rol() ? 'Guardar cambios' : 'Crear rol') }}
          </ui-button>
        </div>
      </form>
    </ui-dialog>
  `,
})
export class RolFormDialogComponent {
  readonly abierto = input.required<boolean>();
  readonly rol = input<RolSistema | null>(null);
  readonly guardando = input<boolean>(false);

  readonly guardar = output<GuardarRolEvento>();
  readonly cerrar = output<void>();

  protected readonly nombre = signal('');
  protected readonly descripcion = signal('');
  protected readonly activo = signal(true);

  constructor() {
    effect(() => {
      const r = this.rol();
      if (r) {
        this.nombre.set(r.nombre);
        this.descripcion.set(r.descripcion || '');
        this.activo.set(r.activo);
      } else {
        this.nombre.set('');
        this.descripcion.set('');
        this.activo.set(true);
      }
    });
  }

  protected formularioValido(): boolean {
    return Boolean(this.nombre().trim().length >= 3);
  }

  protected enviar(event: Event): void {
    event.preventDefault();
    if (!this.formularioValido()) return;

    this.guardar.emit({
      idRol: this.rol()?.idRol ?? null,
      nombre: this.nombre().trim(),
      descripcion: this.descripcion().trim() || null,
      activo: this.activo(),
    });
  }
}
