import { Component, ViewEncapsulation } from '@angular/core';

@Component({
  selector: 'app',
  styleUrls: ['./app.component.css'],
  templateUrl: 'app.component.html',
  encapsulation: ViewEncapsulation.None
})

export class AppComponent {
  opened: boolean;
  shouldRun = [/(^|\.)plnkr\.co$/, /(^|\.)stackblitz\.io$/].some(h => h.test(window.location.host));
}