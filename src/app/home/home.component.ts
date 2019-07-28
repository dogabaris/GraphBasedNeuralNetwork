import { Component, OnInit, ChangeDetectionStrategy, ElementRef, ViewChild, InjectionToken, Inject } from '@angular/core';
import { first } from 'rxjs/operators';

import { User } from '../_models';
import { UserService } from '../_services';
import { DOCUMENT } from '@angular/common';
@Component({
  templateUrl: 'home.component.html'
})
export class HomeComponent implements OnInit {
  users: User[] = [];
  exportCypherModel: string;
  @ViewChild('exportCypherEl', null) exportCypherEl: ElementRef;
  constructor(private userService: UserService, @Inject(DOCUMENT) private document: Document) { }

  ngOnInit() {
    this.userService.getAll().pipe(first()).subscribe(users => {
      this.users = users;
    });
  }

  exportCypher() {
    setTimeout(() => {
      if (this.exportCypherEl && this.exportCypherEl.nativeElement.classList.contains('hide')) {
        console.log("exportCypherElement is hided");
      } else {
        var mdl = document.getElementById("exportCypherTextId") as any;
        console.log("exportCypherModel: ", mdl.value);
        this.exportCypherModel = mdl.value;

        this.userService.exportCypher(this.exportCypherModel);
      }
    }, 300);
  }
}