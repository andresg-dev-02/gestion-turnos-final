import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { ApiClient, AuthStore, NotificationService } from '../../core';
import { FotoPerfilStore } from '../../shared/data/foto-perfil.store';
import { UiButtonComponent } from '../../shared/ui/button/ui-button.component';
import { UiDialogComponent } from '../../shared/ui/dialog/ui-dialog.component';
import { AvatarPerfilComponent } from './avatar-perfil.component';

/**
 * Diálogo para subir, cambiar o quitar la foto de perfil del usuario en sesión.
 * Muestra una vista previa local antes de enviar la imagen.
 */
@Component({
  selector: 'app-foto-perfil-dialog',
  imports: [UiDialogComponent, UiButtonComponent, AvatarPerfilComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-dialog
      id="foto-perfil"
      titulo="Foto de perfil"
      subtitulo="JPG, PNG o WEBP de hasta 5 MB. Se recorta en un cuadrado centrado en tu rostro."
      [abierto]="abierto()"
      (cerrar)="cerrarDialogo()"
    >
      <div class="flex flex-col items-center gap-4">
        <app-avatar-perfil
          [url]="previsualizacion() ?? fotos.url()"
          [nombre]="nombre()"
          tamano="xl"
        />

        <label
          for="foto-perfil-archivo"
          class="inline-flex min-h-11 cursor-pointer items-center justify-center rounded-lg bg-white px-4 py-2 text-sm font-semibold text-[#113459] ring-1 ring-slate-300 ring-inset hover:bg-slate-50 focus-within:outline-2 focus-within:outline-[#1D768A]"
        >
          {{ fotos.url() || archivo() ? 'Elegir otra imagen' : 'Elegir imagen' }}
          <input
            id="foto-perfil-archivo"
            type="file"
            accept="image/jpeg,image/png,image/webp"
            class="sr-only"
            (change)="seleccionar($event)"
          />
        </label>

        @if (archivo(); as a) {
          <p class="text-center text-xs text-slate-600">{{ a.name }}</p>
        }
        @if (error()) {
          <p role="alert" class="text-center text-sm font-medium text-peligro">{{ error() }}</p>
        }
      </div>

      <div acciones class="flex w-full flex-wrap justify-between gap-2">
        <div>
          @if (fotos.url() && !archivo()) {
            <ui-button variante="peligro" [cargando]="eliminando()" [deshabilitado]="guardando()" (click)="quitar()">
              Quitar foto
            </ui-button>
          }
        </div>
        <div class="flex gap-2">
          <ui-button variante="secundario" [deshabilitado]="ocupado()" (click)="cerrarDialogo()">Cancelar</ui-button>
          <ui-button [deshabilitado]="!archivo() || eliminando()" [cargando]="guardando()" (click)="guardar()">
            Guardar
          </ui-button>
        </div>
      </div>
    </ui-dialog>
  `,
})
export class FotoPerfilDialogComponent implements OnDestroy {
  readonly abierto = input(false);
  readonly cerrar = output<void>();

  protected readonly fotos = inject(FotoPerfilStore);
  private readonly auth = inject(AuthStore);
  private readonly notificaciones = inject(NotificationService);

  protected readonly archivo = signal<File | null>(null);
  protected readonly previsualizacion = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly guardando = signal(false);
  protected readonly eliminando = signal(false);
  protected readonly ocupado = computed(() => this.guardando() || this.eliminando());
  protected readonly nombre = computed(() => this.auth.usuario()?.nombreCompleto ?? '');

  constructor() {
    // Al cerrar, descartar la selección pendiente.
    effect(() => {
      if (!this.abierto()) this.limpiarSeleccion();
    });
  }

  ngOnDestroy(): void {
    this.limpiarSeleccion();
  }

  protected seleccionar(evento: Event): void {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0] ?? null;
    entrada.value = '';
    if (!archivo) return;

    const problema = FotoPerfilStore.validar(archivo);
    if (problema) {
      this.limpiarSeleccion();
      this.error.set(problema);
      return;
    }

    this.limpiarSeleccion();
    this.archivo.set(archivo);
    this.previsualizacion.set(URL.createObjectURL(archivo));
  }

  protected guardar(): void {
    const archivo = this.archivo();
    if (!archivo || this.ocupado()) return;
    this.guardando.set(true);
    this.error.set(null);
    this.fotos.subir(archivo).subscribe({
      next: () => {
        this.guardando.set(false);
        this.notificaciones.exito('Tu foto de perfil se actualizó.');
        this.cerrarDialogo();
      },
      error: (err) => {
        this.guardando.set(false);
        this.error.set(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  protected quitar(): void {
    if (this.ocupado()) return;
    this.eliminando.set(true);
    this.error.set(null);
    this.fotos.eliminar().subscribe({
      next: () => {
        this.eliminando.set(false);
        this.notificaciones.exito('Se quitó tu foto de perfil.');
        this.cerrarDialogo();
      },
      error: (err) => {
        this.eliminando.set(false);
        this.error.set(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  protected cerrarDialogo(): void {
    if (this.ocupado()) return;
    this.cerrar.emit();
  }

  private limpiarSeleccion(): void {
    const url = this.previsualizacion();
    if (url) URL.revokeObjectURL(url);
    this.previsualizacion.set(null);
    this.archivo.set(null);
    this.error.set(null);
  }
}
