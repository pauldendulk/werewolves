import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, interval, switchMap, filter, map, tap, shareReplay, catchError, EMPTY } from 'rxjs';
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
        return this.http.get<LobbyState>(url, { observe: 'response' }).pipe(
          catchError((err: HttpErrorResponse) => {
            // 404 means the game is gone — propagate so callers can navigate away.
            // All other errors (offline, 500, timeout) are transient: skip this
            // tick and let the next interval tick retry automatically.
            if (err.status === 404) throw err;
            return EMPTY;
          })
        );
      }),
      filter(response => response.status === 200 && response.body !== null),
      map(response => response.body as LobbyState),
      tap(state => lastVersion = state.game.version),
      shareReplay(1)
    );
  }
}

