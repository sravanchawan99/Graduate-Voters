import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Api } from '../../services/api';

@Component({
  selector: 'app-admin-login',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './admin-login.html',
  styleUrl: './admin-login.css'
})
export class AdminLogin {
  username = '';
  password = '';
  errorMessage = signal('');
  isSubmitting = signal(false);

  constructor(
    private router: Router,
    private api: Api
  ) {}

  signIn(): void {
    this.errorMessage.set('');

    if (!this.username.trim()) {
      this.errorMessage.set('Please enter your username.');
      return;
    }

    if (!this.password.trim()) {
      this.errorMessage.set('Please enter the admin password.');
      return;
    }

    this.isSubmitting.set(true);
    this.api.login({
      username: this.username.trim(),
      password: this.password
    }).pipe(finalize(() => {
      this.isSubmitting.set(false);
    })).subscribe({
      next: (response) => {
        if (response.success ?? response.Success) {
          this.router.navigate(['/admin/dashboard']);
          return;
        }

        this.errorMessage.set(response.message || response.Message || 'Login failed. Please check your credentials.');
      },
      error: (error: { error?: { message?: string; Message?: string } }) => {
        this.errorMessage.set((error as { name?: string }).name === 'TimeoutError'
          ? 'The sign-in request timed out after 15 seconds. Please try again.'
          : error.error?.message || error.error?.Message || 'Unable to sign in. Please try again.');
      }
    });
  }
}
