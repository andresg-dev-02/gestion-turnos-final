import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { PwaInstallPromptComponent, SkipLinkComponent } from './layout';
import { UiToastContainerComponent } from './shared/ui';

/**
 * Componente raíz recopilatorio de la aplicación.
 * Ensambla únicamente el enlace de salto accesible (WCAG 2.4.1),
 * la región aria-live de notificaciones y el enrutador principal.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, SkipLinkComponent, UiToastContainerComponent, PwaInstallPromptComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
