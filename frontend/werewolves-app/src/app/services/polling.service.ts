import { Injectable, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subscription, interval, switchMap, filter, map, tap, shareReplay } from 'rxjs';
import { LobbyState } from '../models/game.models';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class PollingService implements OnDestroy {
  private readonly pollIntervalMs = 1000;
  private subscription?: Subscription;
  private apiUrl = environment.apiUrl;
  private lastVersion: number | null = null;

  constructor(private http: HttpClient) {}

  startPolling(gameId: string): Observable<LobbyState> {
    this.lastVersion = null;
    return interval(this.pollIntervalMs).pipe(
      switchMap(() => {
        const url = this.lastVersion !== null
          ? `${this.apiUrl}/game/${gameId}?version=${this.lastVersion}`
          : `${this.apiUrl}/game/${gameId}`;
        return this.http.get<LobbyState>(url, { observe: 'response' });
      }),
      filter(response => response.status === 200 && response.body !== null),
      map(response => response.body as LobbyState),
      tap(state => this.lastVersion = state.game.version),
      shareReplay(1)
    );
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }
}
