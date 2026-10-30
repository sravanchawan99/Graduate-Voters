import { Component, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize, TimeoutError } from 'rxjs';
import { Api } from '../../services/api';

@Component({
  selector: 'app-registration',
  standalone: true,
   imports: [FormsModule, RouterLink],
  templateUrl: './registration.html',
  styleUrl: './registration.css'
})
export class Registration {

  fullName = '';
  whatsapp = '';
  email = '';
  graduationYear: number | null = 2023;

  mlcConstituency = '';
  districtName = '';

  ack = false;

  constituencyError = '';
  districtError = '';
  nameError = '';
  whatsappError = '';
  yearError = '';
  emailError = '';
  ackError = '';
  submitError = signal('');
  isSubmitting = signal(false);

  graduationYears: number[] = Array.from(
    { length: 74 },
    (_, index) => 2023 - index
  );

  districtsByConstituency: Record<string, string[]> = {

    'Mahabubnagar-Ranga Reddy-Hyderabad': [
      '14-Rangareddy',
      '15-Vikarabad',
      '16-Medchal Malkajgiri',
      '17-Hyderabad',
      '18-Mahabubnagar',
      '19-Nagarkurnool',
      '20-Wanaparthy',
      '21-Jogulamba Gadwal',
      '33-Narayanpet'
    ],

    'Warangal-Khammam-Nalgonda': [
      '13-Siddipet',
      '22-Nalgonda',
      '23-Suryapet',
      '24-Yadadri Bhongiri',
      '25-Jangaon',
      '26-Mahabubabad',
      '27-Warangal',
      '28-Hanumakonda',
      '29-Jayashankar Bhupalpalle',
      '30-Bhadradri',
      '31-Khammam',
      '32-Mulugu'
    ]

  };

  constructor(
    private router: Router,
    private api: Api
  ) {}

  get districts(): string[] {
    return this.districtsByConstituency[this.mlcConstituency] ?? [];
  }

  onConstituencyChange(): void {
    this.districtName = '';
    this.districtError = '';
  }

  sanitizePhone(): void {
    this.whatsapp = this.whatsapp
      .replace(/\D/g, '')
      .slice(0, 10);
  }

  clearErrors(): void {
    this.constituencyError = '';
    this.districtError = '';
    this.nameError = '';
    this.whatsappError = '';
    this.yearError = '';
    this.emailError = '';
    this.ackError = '';
  }

  submitForm(): void {

    if (this.isSubmitting()) {
      return;
    }

    this.clearErrors();
    this.submitError.set('');

    let valid = true;

    const name = this.fullName.trim();
    const phone = this.whatsapp.trim();
    const email = this.email.trim();

    if (!this.mlcConstituency) {
      this.constituencyError =
        'Please select your MLC council constituency.';
      valid = false;
    }

    if (!this.districtName) {
      this.districtError =
        'Please select your district.';
      valid = false;
    }

    if (name.length < 2) {
      this.nameError =
        'Please enter your full name.';
      valid = false;
    }

    if (!/^[6-9]\d{9}$/.test(phone)) {
      this.whatsappError =
        'Enter a valid 10-digit Indian mobile number.';
      valid = false;
    }

    if (
      this.graduationYear === null ||
      this.graduationYear < 1950 ||
      this.graduationYear > 2023
    ) {
      this.yearError =
        'Graduation year must be 2023 or earlier.';
      valid = false;
    }

    if (
      email &&
      !/^\S+@\S+\.\S+$/.test(email)
    ) {
      this.emailError =
        'Enter a valid email address.';
      valid = false;
    }

    if (!this.ack) {
      this.ackError =
        'Please acknowledge the information above.';
      valid = false;
    }

    if (!valid) {
      return;
    }

    this.isSubmitting.set(true);
    this.api.addVoter({
      fullName: name,
      phoneNumber: `+91${phone}`,
      constituency: this.mlcConstituency,
      district: this.districtName,
      yearOfGraduation: String(this.graduationYear),
      email,
    }).pipe(finalize(() => {
      this.isSubmitting.set(false);
    })).subscribe({
      next: () => {
        this.router.navigate(['/confirmation']);
      },
      error: (error: unknown) => {
        console.error('Registration request failed.', error);
        this.submitError.set(error instanceof TimeoutError ||
          (error instanceof HttpErrorResponse && error.status === 0)
          ? 'Please click Continue again.'
          : 'Please click Continue again.');
      }
    });
  }
}
