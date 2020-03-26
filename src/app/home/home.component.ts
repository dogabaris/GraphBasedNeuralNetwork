import { Component, OnInit, ChangeDetectionStrategy, ElementRef, ViewChild, InjectionToken, Inject, Renderer, ChangeDetectorRef } from '@angular/core';
import { first } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { User } from '../_models';
import { UserService } from '../_services';
import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Workspace } from '../_models/workspace';

declare var NeoVis: any;
declare var neo4j: any;
declare var Viva: any;

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
  isPause = false;
  openTestModel = false;
  showBigGraphEditor = false;
  toggleLabels: any;
  clearBigGraph: any;
  graph: any;
  renderer: any;
  graphics: any;
  layout: any;
  viva: any;
  events: any;

  testNode1 = 0;
  testNode2 = 0;
  testNode3 = 0;

  clearBigGraphFnc() {
    this.clearBigGraph();
  }

  toggleLabelsFnc() {
    this.toggleLabels();
  }

  showGroupedGraph() {
    this.bigGraph(this.selectedModel, true);
    this.showBigGraphEditor = true;
  }

  bigGraph(this: any, workspace: any, showGrouped: any) {
    (document.querySelector('#graph') as HTMLElement).style.display = 'block';
    var neo = neo4j.v1;
    var driver = neo.driver("bolt://localhost:7687", neo.auth.basic("neo4j", "password"));
    var session = driver.session();
    var container = document.body;
    var domLabels: any;
    var displayLabels = false;
    var _this = this;

    if (this.renderer != null) {
      this.clearBigGraph();
    }

    this.toggleLabels = function toggleLabels() {
      console.log(domLabels);
      displayLabels = !displayLabels;
    }
    
    _this.graph = Viva.Graph.graph();
    _this.layout = Viva.Graph.Layout.forceDirected(_this.graph, {
      springLength: 100,
      springCoeff: 0.0008,
      dragCoeff: 0.009,
      gravity: -1,
      theta: 0.8,
    });
    var colors = { Member: 0xff0000, Topic: 0x000000, Group: 0x0000ff };
    _this.graphics = Viva.Graph.View.webglGraphics();

    _this.events = Viva.Graph.webglInputEvents(_this.graphics, _this.graph);
    _this.events.mouseEnter(function (node: any) {
      console.log('Label: ' + node.id.labels[0]);
      console.log('Data: ' + node.id.properties.data);
      console.log('Workspace: ' + node.id.properties.workspace);
      console.log('Node Bulk Data: ' + JSON.stringify(node));
    }).click(function (node: any) {
      console.log('Single click on node: ' + node.id);
    });

    _this.graphics.node(function (node: any) {
      console.log("node", node)
      var color = colors[node.data] || 0x0fffff;
      var degree = node.links.length;
      var size = Math.log(degree + 1) * 10;
      console.log("color", color, "data", node.data, "size", size, "degree", degree)
      var node = new Viva.Graph.View.webglSquare(size, color);
      return node;
    });

    _this.graphics.link(function (link: any) {
      return Viva.Graph.View.webglLine(0xF0Fff0);
    });

    function query(pattern: any) {
      var statement = pattern;
      console.log("Running", statement);
      session.run(statement).subscribe(_this.viva);
    }

    _this.renderer = Viva.Graph.View.renderer(_this.graph,
      {
        layout: _this.layout,
        graphics: _this.graphics,
        renderLinks: true,
        prerender: true,
        container: document.getElementById('graph')
      });

    this.clearBigGraph = function clearBigGraph() {
      console.log("clearbiggraph ", _this.renderer);
      _this.renderer.dispose();
      _this.renderer = null;
      _this.showBigGraphEditor = false;
      const removeElements = document.getElementsByClassName("node-label");
      while (removeElements.length > 0) removeElements[0].remove();
    }

    function generateDOMLabels(graph: any) {
      var labels = Object.create(null);
      graph.forEachNode(function (node: any) {
        var label = document.createElement('span');
        label.classList.add('node-label');
        label.innerText = node.id.labels[0];
        labels[node.id] = label;
        container.appendChild(label);
      });
      return labels;
    }

    var count = 0;
    _this.viva = {
      onNext: function (record: any) {
        count++;
        console.log("record", record);
        record._fields[0].data = record.workspace;
        var n1 = record._fields[0];
        if (record.length == 2) {
          _this.graph.addNode(n1);
        }
        if (record.length == 2) {
          var n2 = record._fields[1];
          _this.graph.addLink(n1, n2);
        }
        if (record.length == 4) {
          var n2 = record._fields[2];
          _this.graph.addNode(n1, record._fields[1])
          _this.graph.addNode(n2, record._fields[3])
          _this.graph.addLink(n1, n2);
        }
        if (count % 5000 == 0) console.log("Currently", count, "links");
      },
      onCompleted: function () {
        console.log("Query finished, currently ", count, "links");
        domLabels = generateDOMLabels(_this.graph);

        _this.graphics.placeNode(function (ui: any, pos: any) {
          var domPos = {
            x: pos.x,
            y: pos.y
          };
          _this.graphics.transformGraphToClientCoordinates(domPos);
          var nodeId = ui.node.id;
          var labelStyle = domLabels[nodeId].style;
          labelStyle.left = domPos.x + 'px';
          labelStyle.top = domPos.y + 'px';
          if (displayLabels)
            labelStyle.display = ''
          else
            labelStyle.display = 'none'
        });

        // tüm veriler yüklendikten sonra çizim için
        _this.renderer.run();
        //setTimeout(function() { console.log("Pausing renderer"); renderer.pause(); },10000);
      }
    };

    if (showGrouped)
      query("CALL apoc.nodes.group(['*'],['workspace']) YIELD nodes,relationships UNWIND nodes as node UNWIND relationships as rel WITH node, rel MATCH p=(node)-[rel]->() WHERE apoc.any.properties(node).workspace = '" + workspace + "' RETURN node as n, node.type as nt, nodes(p)[1] as m, nodes(p)[1].type as mt");
    else
      query("CYPHER runtime=compiled MATCH (to)-->(from) where from.workspace='" + workspace + "' RETURN from as n, from.type as nt, to as m, to.type as mt");
    //renderer.run(); // her seferinde çizim için
  }

  constructor(private userService: UserService, @Inject(DOCUMENT) private document: Document, private toastr: ToastrService
    , private cdRef: ChangeDetectorRef) { }

  ngOnInit() {
    this.userService.getAllWorkspaces().pipe(first()).subscribe(workspaces => {
      this.workspaces = workspaces;
    });
  }

  testModelPopup(content: any) {
    this.openTestModel = !this.openTestModel;
  }

  deleteModel() {
    this.userService.deleteModel(this.selectedModel).pipe(first()).subscribe(
      res => {
        console.log(res);
        this.showSuccess("Model başarıyla silindi!");
      },
      err => {
        console.log("Error occured");
        this.showError("Model silinirken sorun oluştu!");
      }
    );
  }

  testModel() {
    var dataNodes = new Array<number>();
    dataNodes.push(this.testNode1);
    dataNodes.push(this.testNode2);
    dataNodes.push(this.testNode3);

    this.userService.testModel(this.selectedModel, dataNodes).pipe(first()).subscribe(
      res => {
        console.log(res);
        this.showSuccess("Model test edildi!");
      },
      err => {
        console.log("Error occured");
        this.showError("Model test edilirken sorun oluştu!");
      }
    );
  }

  newModel() {
    document.getElementById("viz").classList.add("hide");
    document.getElementById("tools").classList.remove("hide");
    document.getElementById("canvas").classList.remove("hide");
    this.showTrainTools = false;
    this.cancelRefreshOfViewModel();
    this.clearBigGraph();
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

  importCnnH5Model() {
    this.userService.importCnnH5Model().pipe(first()).subscribe(result => {
      console.log(result);
      this.showSuccess("Model başarıyla aktarıldı!");
    },
      err => {
        console.log(err);
        console.log("Error occured!");
        this.showError("Model aktarılırken sorun oluştu!");
      });
  }

  importMnistH5Model() {
    this.userService.importMnistH5Model().pipe(first()).subscribe(result => {
      console.log(result);
      this.showSuccess("Model başarıyla aktarıldı!");
    },
      err => {
        console.log(err);
        console.log("Error occured!");
        this.showError("Model aktarılırken sorun oluştu!");
      });
  }

  exportH5Model() {
    this.userService.exportH5Model(this.selectedModel).pipe(first()).subscribe(result => {
      console.log(result);
      this.showSuccess("Model başarıyla dışa aktarıldı!");
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
    var _this = this;
    _this.showTrainTools = true;
    _this.selectedModel = modelName;
    this.cancelRefreshOfViewModel();

    var neo = neo4j.v1;
    var driver = neo.driver("bolt://localhost:7687", neo.auth.basic("neo4j", "password"));
    var sessionlocal = driver.session();

    var isBig = false;

    var result = {
      onNext: function (record: any) {
        console.log("Node sayisi: ", record._fields[0].low);
        if (record._fields[0].low > 30)
          isBig = true;
      },
      onCompleted: function (data: any) {
        console.log("Model verileri: ", JSON.stringify(data));

        if (isBig) {
          console.log("Big Graph");
          _this.bigGraph(_this.selectedModel, false);
          _this.showBigGraphEditor = true;
        } else {
          console.log("Little Graph");
          _this.littleGraph(_this.selectedModel);
        }
      }
    };

    sessionlocal.run("MATCH (n) WHERE n.workspace = '" + modelName + "' RETURN COUNT(*)").subscribe(result);
  }

  littleGraph(modelName: any) {
    (document.querySelector('#graph') as HTMLElement).style.display = 'none';

    if (this.renderer != null) {
      this.clearBigGraph();
    }

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

      initial_cypher: "match (n)-[r:related]->(e)  where(n.workspace = '" + modelName + "') return n,r,e",
      //"start n=node(*), r=relationship(*) match(n) where(n.workspace = '" + modelName + "') return n,r",
      arrows: true,
      hierarchical: true,
      //hierarchical_layout: true,
      hierarchical_sort_method: "directed",
    };
    this.viz = new NeoVis.default(config);

    this.viz.render();
    console.log(this.viz);

    this.startRefreshInterval();
  }

  startRefreshInterval() {
    this.isPause = false;
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
    this.isPause = true;
    if (this.interval) {
      clearInterval(this.interval);
    }
  }

  showSuccess(msg: any) {
    this.toastr.success(msg, null, { disableTimeOut: true, positionClass: 'toast-top-center', closeButton: true, enableHtml: true });
  }

  showError(msg: any) {
    this.toastr.error(msg, null, { disableTimeOut: true, positionClass: 'toast-top-center', closeButton: true, enableHtml: true });
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