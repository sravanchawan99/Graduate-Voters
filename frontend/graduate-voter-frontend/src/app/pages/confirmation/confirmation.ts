import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-confirmation',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './confirmation.html',
  styleUrl: './confirmation.css'
})
export class Confirmation {
}