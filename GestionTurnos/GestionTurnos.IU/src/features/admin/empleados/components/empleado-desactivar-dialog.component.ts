import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Empleado } from '../../../../app/shared/data';
import { UiButtonComponent, UiDialogComponent, UiNotchedFieldComponent } from '../../../../app/shared/ui';

@Component({
  selector: 'app-empleado-desactivar-dialog',
  imports: [FormsModule, UiDialogComponent, UiNotchedFieldComponent, UiButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-dialog
      id="dlg-desactivar-empleado"
      [abierto]="abierto()"
      titulo="Desactivar Empleado"
      [subtitulo]="(empleado()?.nombres ?? '') + ' ' + (empleado()?.apellidos ?? '')"
      (cerrar)="cerrar.emit()"
    >
      <div class="space-y-4">
        <p class="text-sm text-slate-700">
          Al desactivar este empleado, su estado pasará a <strong>INACTIVO</strong> y se suspenderá
          el acceso de su cuenta de usuario vinculada. Los registros históricos y turnos pasados se mantendrán intactos.
        </p>

        <ui-notched-field
          id="emp-fecha-retiro"
          tipo="date"
          etiqueta="Fecha de retiro"
          [valor]="fechaRetiro()"
          [obligatorio]="true"
          (valorCambio)="fechaRetiro.set($event)"
        />

        <div class="mt-6 flex justify-end gap-3 pt-2">
          <button type="button" class="accion-secundaria" (click)="cerrar.emit()">
            Cancelar
          </button>
          <ui-button
            variante="peligro"
            [deshabilitado]="procesando() || !fechaRetiro()"
            (click)="confirmar.emit(fechaRetiro())"
          >
            {{ procesando() ? 'Desactivando...' : 'Confirmar desactivación' }}
          </ui-button>
        </div>
      </div>
    </ui-dialog>
  `,
})
export class EmpleadoDesactivarDialogComponent {
  readonly abierto = input.required<boolean>();
  readonly empleado = input<Empleado | null>(null);
  readonly procesando = input<boolean>(false);

  readonly confirmar = output<string>();
  readonly cerrar = output<void>();

  protected readonly fechaRetiro = signal(new Date().toISOString().substring(0, 10));
}
