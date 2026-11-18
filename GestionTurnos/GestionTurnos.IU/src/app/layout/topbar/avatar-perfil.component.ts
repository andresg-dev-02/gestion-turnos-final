import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';

const TAMANOS = {
  sm: 'h-9 w-9 text-xs',
  xl: 'h-32 w-32 text-3xl',
} as const;

/** Foto de perfil circular; si no hay imagen (o falla al cargar) muestra las iniciales. */
@Component({
  selector: 'app-avatar-perfil',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="inline-flex shrink-0 items-center justify-center overflow-hidden rounded-full bg-[#113459] font-bold text-white ring-2 ring-white"
      [class]="clasesTamano()"
      aria-hidden="true"
    >
      @if (url() && urlFallida() !== url()) {
        <img
          [src]="url()"
          alt=""
          class="h-full w-full object-cover"
          (error)="urlFallida.set(url())"
        />
      } @else {
        {{ iniciales() }}
      }
    </span>
  `,
})
export class AvatarPerfilComponent {
  readonly url = input<string | null>(null);
  readonly nombre = input('');
  readonly tamano = input<keyof typeof TAMANOS>('sm');

  protected readonly urlFallida = signal<string | null>(null);
  protected readonly clasesTamano = computed(() => TAMANOS[this.tamano()]);
  protected readonly iniciales = computed(() => {
    const partes = this.nombre().trim().split(/\s+/).filter(Boolean);
    if (partes.length === 0) return '?';
    const primera = partes[0][0];
    const ultima = partes.length > 1 ? partes[partes.length - 1][0] : '';
    return (primera + ultima).toUpperCase();
  });
}
