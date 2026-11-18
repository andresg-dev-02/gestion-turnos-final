import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-skip-link',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a href="#contenido-principal" class="skip-link">
      Saltar al contenido principal
    </a>
  `,
})
export class SkipLinkComponent {}
