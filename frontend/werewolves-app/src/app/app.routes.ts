import { Routes } from '@angular/router';
import { CreateGameComponent } from './components/create-game/create-game';
import { JoinGameComponent } from './components/join-game/join-game';
import { LobbyComponent } from './components/lobby/lobby';
import { SessionComponent } from './components/session/session';

export const routes: Routes = [
  { path: '', component: CreateGameComponent },
  { path: 'game/:id', component: JoinGameComponent },
  { path: 'game/:id/lobby', component: LobbyComponent },
  { path: 'game/:id/session', component: SessionComponent },
  { path: '**', redirectTo: '' }
];
