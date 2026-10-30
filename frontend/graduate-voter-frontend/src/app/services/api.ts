import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, timeout } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AddVoterPayload {
  fullName: string;
  phoneNumber: string;
  constituency: string;
  district: string;
  yearOfGraduation: string;
  email: string;
}

export interface LoginPayload {
  username: string;
  password: string;
}

export interface LoginResponse {
  message?: string;
  success?: boolean;
  // Accept PascalCase too in case the API's serializer settings change.
  Message?: string;
  Success?: boolean;
}

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly apiBaseUrl = environment.apiBaseUrl.replace(/\/$/, '');

  constructor(private readonly http: HttpClient) {}

  addVoter(payload: AddVoterPayload): Observable<unknown> {
    return this.http.post<unknown>(`${this.apiBaseUrl}/AddVoter`, payload).pipe(timeout(3_000));
  }

  login(payload: LoginPayload): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiBaseUrl}/Login`, payload).pipe(timeout(15_000));
  }

  downloadVoterCsv(constituency: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.apiBaseUrl}/DownloadVoterCsv`, {
      params: { constituency },
      observe: 'response',
      responseType: 'blob'
    }).pipe(timeout(30_000));
  }
}
