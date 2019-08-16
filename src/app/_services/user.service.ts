﻿import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';

import { User } from '../_models';
import { catchError, tap } from 'rxjs/operators';
import { throwError } from 'rxjs/internal/observable/throwError';
import { Observable } from 'rxjs/internal/Observable';
import { ToastrService } from 'ngx-toastr';
import { Workspace } from '../_models/workspace';

@Injectable({ providedIn: 'root' })
export class UserService {
  constructor(private http: HttpClient, private toastr: ToastrService) { }

  getAll() {
    return this.http.get<User[]>(`${config.apiUrl}/users`);
  }

  getAllWorkspaces() {
    var user = JSON.parse(localStorage.getItem('currentUser'));
    let params = new HttpParams().set("userId", user.id );
    return this.http.get<Workspace[]>(`${config.apiUrl}/users/getallworkspaces`, { params: params });
  }
  
  createModel(cypherQuery: any) {
    console.log("exportCypher fired");
    var user = JSON.parse(localStorage.getItem('currentUser'));
    if (user && user.token) {
      return this.http.post(`${config.apiUrl}/users/createmodel`, { "user": user, "cypherQuery": cypherQuery });
      
    }
    else {
      this.toastr.error("Giriş yapın!");
    }
  }

  trainBinaryPerceptron(workspace: string) {
    var user = JSON.parse(localStorage.getItem('currentUser'));
    let params = new HttpParams().set("model", workspace);
    return this.http.get<Workspace[]>(`${config.apiUrl}/users/trainbinaryperceptron`, { params: params });

  }
  
  handleError(error: HttpErrorResponse) {
    if (error.error instanceof ErrorEvent) {
      // A client-side or network error occurred. Handle it accordingly.
      console.error('An error occurred:', error.error.message);
    } else {
      // The backend returned an unsuccessful response code.
      // The response body may contain clues as to what went wrong,
      console.error(
        `Backend returned code ${error.status}, ` +
        `body was: ${error.error}`);
    }
    // return an observable with a user-facing error message
    return throwError(
      'Something bad happened; please try again later.');
  };
}