import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map } from 'rxjs/operators';
import { User } from '../_models/user';

@Injectable({ providedIn: 'root' })
export class AuthenticationService {
  constructor(private http: HttpClient) { }

  login(username: string, password: string) {
    return this.http.post<any>(`${config.apiUrl}/users/authenticate`, { username, password })
      .pipe(map(user => {
        // jwt token varsa giriş başarılıdır.
        if (user && user.token) {
          // browsera user bilgileri kaydedilir daha sonra kullanılabilir olur
          localStorage.setItem('currentUser', JSON.stringify(user));
        }

        return user;
      }));
  }

  register(username: string, password: string, firstname: string, lastname: string) {
    return this.http.post<any>(`${config.apiUrl}/users/register`, { username, password, firstname, lastname })
      .pipe(map(result => {
        // login successful if there's a jwt token in the response
        //if (user && user.token) {
        //  // store user details and jwt token in local storage to keep user logged in between page refreshes
        //  localStorage.setItem('currentUser', JSON.stringify(user));
        //}

        return result;
      }));
  }

  logout() {
    // remove user from local storage to log user out
    localStorage.removeItem('currentUser');
  }
}