import { HttpErrorResponse } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { Api } from '../../services/api';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './admin-dashboard.html',
  styleUrls: ['./admin-dashboard.css']
})
export class AdminDashboard {
  readonly constituencies = [
    'Mahabubnagar-Ranga Reddy-Hyderabad',
    'Warangal-Khammam-Nalgonda'
  ];
  selectedConstituency = this.constituencies[0];
  isDownloading = signal(false);
  downloadMessage = signal('');
  downloadError = signal('');

  constructor(
    private readonly api: Api,
    private readonly router: Router
  ) {}

  signOut(): void {
    this.router.navigate(['/admin/login']);
  }

  downloadCsv(): void {
    if (this.isDownloading()) {
      return;
    }

    this.isDownloading.set(true);
    this.downloadMessage.set('');
    this.downloadError.set('');
    const constituency = this.selectedConstituency;
    this.api.downloadVoterCsv(constituency).pipe(finalize(() => {
      this.isDownloading.set(false);
    })).subscribe({
      next: (response) => {
        const file = response.body;
        if (!file) {
          this.downloadError.set('The server returned an empty CSV file.');
          return;
        }

        const contentType = response.headers.get('Content-Type')?.toLowerCase() ?? file.type.toLowerCase();
        if (!contentType.includes('text/csv')) {
          this.downloadError.set('No voter CSV file is present yet.');
          return;
        }

        const filename = this.getFilename(
          response.headers.get('Content-Disposition')
        );
        const downloadUrl = URL.createObjectURL(file);
        const link = document.createElement('a');
        link.href = downloadUrl;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(downloadUrl), 1000);
        this.downloadMessage.set(`Downloaded ${filename} for ${constituency}.`);
      },
      error: (error: unknown) => {
        console.error('CSV download failed.', error);
        const httpError = error as HttpErrorResponse;
        this.downloadError.set(httpError.status === 404
          ? 'No voter CSV file is present yet.'
          : error instanceof Error && error.name === 'TimeoutError'
            ? 'The CSV download took too long. Please try again.'
            : 'The CSV could not be downloaded. Please try again.');
      }
    });
  }

  private getFilename(contentDisposition: string | null): string {
    if (!contentDisposition) {
      return 'voters.csv';
    }

    const encodedFilename = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1];
    if (encodedFilename) {
      try {
        return decodeURIComponent(encodedFilename.replace(/^"|"$/g, ''));
      } catch {
        // Use the regular filename parameter if filename* is malformed.
      }
    }

    const filename = contentDisposition.match(/filename="?([^";]+)"?/i)?.[1];
    return filename || 'voters.csv';
  }
}
