import { Component, OnInit } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { first } from 'rxjs/operators';
import { User } from '../_models/user';
import { Result } from '../_models/result';

import { AuthenticationService } from '../_services';

@Component({ templateUrl: 'register.component.html' })
export class RegisterComponent implements OnInit {
  registerForm: FormGroup;
  loading = false;
  submitted = false;
  returnUrl: string;
  error = '';
  info = '';

  constructor(
    private formBuilder: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private authenticationService: AuthenticationService) { }

  ngOnInit() {
    this.registerForm = this.formBuilder.group({
      Username: ['', Validators.required],
      Password: ['', Validators.required],
      FirstName: ['', Validators.required],
      LastName: ['', Validators.required]
    });

    // login durumunu sıfırlar
    this.authenticationService.logout();

    // nereye dönülmek isteniyorsa urli alıp oraya döndürülür.
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/';
  }

  get f() { return this.registerForm.controls; }

  onSubmit() {
    this.submitted = true;

    // Boş bırakılan yer hata ver
    if (this.registerForm.invalid) {
      return;
    }

    this.loading = true;
    this.authenticationService.register(this.f.Username.value, this.f.Password.value, this.f.FirstName.value, this.f.LastName.value)
      .pipe(first())
      .subscribe(
      result => {
          if (result.data == null) {
            this.error = result.message;
          } else {
            this.info = result.message;
            this.error = '';

            setTimeout(() => {
              this.router.navigate(['/login']);
            }, 500);
          }
        },
        error => {
          this.error = error;
          this.loading = false;
        });
  }
}