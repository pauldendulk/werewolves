import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, interval, switchMap, filter, map, tap, shareReplay } from 'rxjs';
import { LobbyState } from '../models/game.models';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class PollingService {
  private readonly pollIntervalMs = 1000;
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  startPolling(gameId: string): Observable<LobbyState> {
    // lastVersion is closed over per call so concurrent lobby+session pollers
    // don't clobber each other's version state.
    let lastVersion: number | null = null;
    return interval(this.pollIntervalMs).pipe(
      switchMap(() => {
        const url = lastVersion !== null
          ? `${this.apiUrl}/game/${gameId}?version=${lastVersion}`
          : `${this.apiUrl}/game/${gameId}`;
        return this.http.get<LobbyState>(url, { observe: 'response' });
      }),
      filter(response => response.status === 200 && response.body !== null),
      map(response => response.body as LobbyState),
      tap(state => lastVersion = state.game.version),
      shareReplay(1)
    );
  }
}

