import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'ui-auth-illustration-placeholder',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative flex w-full max-w-lg flex-col items-center justify-center p-4">
      <!-- Forma orgánica con la paleta de marca del sistema (petróleo / marino / menta) -->
      <div
        class="relative flex aspect-square w-full max-w-[440px] items-center justify-center rounded-[58%_42%_48%_52%/44%_56%_44%_56%] bg-gradient-to-tr from-[#113459] via-[#1D768A] to-[#31BBA1] p-4 shadow-2xl"
      >
        <!-- Ilustración oficial sin fondo -->
        <img
          src="/Ilustracion_Login_Register.png"
          alt="Gestión de Turnos y Calendario"
          width="420"
          height="420"
          class="relative z-10 h-full w-full object-contain drop-shadow-md select-none"
        />
      </div>

    </div>
  `,
})
export class AuthIllustrationPlaceholderComponent {}
