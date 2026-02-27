import { Injectable, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subscription, interval, switchMap, shareReplay, distinctUntilChanged } from 'rxjs';
import { LobbyState } from '../models/game.models';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class PollingService implements OnDestroy {
  private readonly pollIntervalMs = 1000;
  private subscription?: Subscription;
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  startPolling(gameId: string): Observable<LobbyState> {
    return interval(this.pollIntervalMs).pipe(
      switchMap(() => this.http.get<LobbyState>(`${this.apiUrl}/game/${gameId}`)),
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
      shareReplay(1)
    );
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }
}
