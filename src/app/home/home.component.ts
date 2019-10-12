import { Component, OnInit, ChangeDetectionStrategy, ElementRef, ViewChild, InjectionToken, Inject, Renderer, ChangeDetectorRef } from '@angular/core';
import { first } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { User } from '../_models';
import { UserService } from '../_services';
import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Workspace } from '../_models/workspace';

declare var NeoVis: any;

@Component({
  templateUrl: 'home.component.html'
})
export class HomeComponent implements OnInit {
  users: User[] = [];
  workspaces: any = [];
  exportCypherModel: string;
  @ViewChild('exportCypherEl', null) exportCypherEl: ElementRef;

  emptyObj1: any;
  emptyObj: any;
  info: any;
  neo4jframe: any;
  viz: any;
  interval: any;
  showTrainTools = false;
  selectedModel: any;

  constructor(private userService: UserService, @Inject(DOCUMENT) private document: Document, private toastr: ToastrService
    , private cdRef: ChangeDetectorRef) { }

  ngOnInit() {
    //this.userService.getAll().pipe(first()).subscribe(users => {
    //  this.users = users;
    //});

    this.userService.getAllWorkspaces().pipe(first()).subscribe(workspaces => {
      this.workspaces = workspaces;
    });
  }

  newModel() {
    document.getElementById("viz").classList.add("hide");
    document.getElementById("tools").classList.remove("hide");
    document.getElementById("canvas").classList.remove("hide");
    this.showTrainTools = false;
    this.cancelRefreshOfViewModel();
  }

  importH5Model() {
    this.userService.importH5Model().pipe(first()).subscribe(result => {
      console.log(result);
      this.showSuccess("Model başarıyla aktarıldı!");
    },
      err => {
        console.log("Error occured!");
        this.showError("Model aktarılırken sorun oluştu!");
      });
  }

  trainBinaryPerceptron() {
    this.userService.trainBinaryPerceptron(this.selectedModel).pipe(first()).subscribe(result => {
      console.log(result);
      this.showSuccess("Model başarıyla eğitildi! <br>" + JSON.stringify(result));
    },
      err => {
        console.log("Error occured!");
        this.showError("Model eğitiminde sorun oluştu!");
      });
  }

  getModel(modelName: string) {
    document.getElementById("tools").classList.add("hide");
    document.getElementById("canvas").classList.add("hide");
    document.getElementById("viz").classList.remove("hide");
    this.showTrainTools = true;
    this.selectedModel = modelName;

    const url = 'bolt://localhost:7687';
    const username = 'neo4j';
    const password = 'password';
    const encrypted = true;

    var config = {
      container_id: "viz",
      server_url: "bolt://localhost:7687",
      server_user: "neo4j",
      server_password: "password",
      labels: {
        "input": {
          "caption": "input",
          "size": "data"
        },
        "hidden": {
          "caption": "hidden",
          "size": "data"
        },
        "output": {
          "caption": "data",
          "size": "data"
        }
      },
      relationships: {
        "related": {
          "caption": "weight",
          "thickness": "weight"
        }
      },

      initial_cypher: "start n=node(*), r=relationship(*) match(n) where(n.workspace = '" + modelName + "') return n,r",
      arrows: true,
      hierarchical: true,
      //hierarchical_layout: true,
      hierarchical_sort_method: "directed",
    };
    this.viz = new NeoVis.default(config);

    this.viz.render();
    console.log(this.viz);

    this.interval = setInterval(() => {
      this.refreshViewModel();
    }, 2000);
  }

  refreshViewModel() {
    this.viz.stabilize();
    this.viz.reload();
    //this.viz.render();
  }

  cancelRefreshOfViewModel() {
    if (this.interval) {
      clearInterval(this.interval);
    }
  }

  showSuccess(msg: any) {
    this.toastr.success(msg, null, { disableTimeOut: true, closeButton: true, enableHtml: true });
  }

  showError(msg: any) {
    this.toastr.error(msg, null, { disableTimeOut: true, closeButton: true, enableHtml: true  });
  }

  createModel() {
    setTimeout(() => {
      if (this.exportCypherEl && this.exportCypherEl.nativeElement.classList.contains('hide')) {
        console.log("exportCypherElement is hided");
      } else {
        var mdl = document.getElementById("exportCypherTextId") as any;
        console.log("exportCypherModel: ", mdl.value);
        this.exportCypherModel = mdl.value;

        this.userService.createModel(this.exportCypherModel).subscribe(
          res => {
            console.log(res);
            this.showSuccess("Model başarıyla oluşturuldu!");
            this.exportCypherEl.nativeElement.classList.add("hide");
          },
          err => {
            console.log("Error occured");
            this.showError("Model oluşturulamadı!");
          }
        );
      }
    }, 300);
  }
  openNewModelPage() {
    localStorage.removeItem('graph-diagram-markup');
    location.reload();
    //this.cdRef.detectChanges();
    //localStorage.clear();
  }
}