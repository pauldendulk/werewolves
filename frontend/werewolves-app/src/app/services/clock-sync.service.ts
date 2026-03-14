import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

const PING_COUNT = 5;
const SYNC_INTERVAL_MS = 5 * 60 * 1000; // re-sync every 5 minutes

@Injectable({ providedIn: 'root' })
export class ClockSyncService {
  /** Estimated offset: localTime + offset = serverTime */
  private offsetMs = 0;
  private syncTimer: ReturnType<typeof setInterval> | null = null;

  constructor(private http: HttpClient) {}

  /** Start syncing. Call once when the player joins a game. */
  start(): void {
    this.sync();
    this.syncTimer = setInterval(() => this.sync(), SYNC_INTERVAL_MS);
  }

  stop(): void {
    if (this.syncTimer !== null) {
      clearInterval(this.syncTimer);
      this.syncTimer = null;
    }
  }

  /** Convert a server UTC-millisecond timestamp to a local Date. */
  serverMsToLocal(serverMs: number): Date {
    return new Date(serverMs - this.offsetMs);
  }

  /** Convert an ISO server timestamp string to a local Date. */
  serverIsoToLocal(iso: string): Date {
    return this.serverMsToLocal(new Date(iso).getTime());
  }

  private sync(): void {
    const samples: number[] = [];
    let remaining = PING_COUNT;

    const ping = () => {
      const t1 = Date.now();
      this.http.get<{ serverTimeMs: number }>(`${environment.apiUrl}/game/time`)
        .subscribe({
          next: ({ serverTimeMs }) => {
            const t2 = Date.now();
            const rtt = t2 - t1;
            // offset = serverTime - localTimeAtMidpoint
            samples.push(serverTimeMs - (t1 + rtt / 2));
            remaining--;
            if (remaining > 0) {
              setTimeout(ping, 100);
            } else {
              this.applyOffset(samples);
            }
          },
          error: () => {
            remaining--;
            if (remaining > 0) setTimeout(ping, 100);
          }
        });
    };

    ping();
  }

  private applyOffset(samples: number[]): void {
    if (samples.length === 0) return;
    const sorted = [...samples].sort((a, b) => a - b);
    // Trim outliers: drop top and bottom if we have enough samples
    const trimmed = sorted.length >= 4 ? sorted.slice(1, -1) : sorted;
    this.offsetMs = Math.round(trimmed.reduce((s, v) => s + v, 0) / trimmed.length);
  }
}
