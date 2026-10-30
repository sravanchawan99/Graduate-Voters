import { Routes } from '@angular/router';

import { Home } from './pages/home/home';
import { Registration } from './pages/registration/registration';
import { Confirmation } from './pages/confirmation/confirmation';
import { AdminLogin } from './pages/admin-login/admin-login';
import { AdminDashboard } from './pages/admin-dashboard/admin-dashboard';

export const routes: Routes = [

  {
    path: '',
    component: Home
  },

  {
    path: 'registration',
    component: Registration
  },

  {
    path: 'confirmation',
    component: Confirmation
  },

  {
    path: 'admin',
    redirectTo: 'admin/login',
    pathMatch: 'full'
  },

  {
    path: 'admin/login',
    component: AdminLogin
  },

  {
    path: 'admin/dashboard',
    component: AdminDashboard
  },

  {
    path: '**',
    redirectTo: ''
  }

];