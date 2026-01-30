import { Routes } from '@angular/router';
import { CreateGameComponent } from './components/create-game/create-game';
import { JoinGameComponent } from './components/join-game/join-game';
import { LobbyComponent } from './components/lobby/lobby';

export const routes: Routes = [
  { path: '', component: CreateGameComponent },
  { path: 'game/:id', component: JoinGameComponent },
  { path: 'game/:id/lobby', component: LobbyComponent },
  { path: '**', redirectTo: '' }
];
