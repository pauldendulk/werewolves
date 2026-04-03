import { Injectable } from '@angular/core';
import { AudioKey } from '../models/audio-keys';

const LANGUAGE = 'en-US';

@Injectable({ providedIn: 'root' })
export class AudioService {
  private unlocked = false;
  private preloaded = new Map<string, HTMLAudioElement>();

  constructor() {
    for (const key of Object.values(AudioKey)) {
      const audio = new Audio(`assets/audio/${LANGUAGE}/${key}.mp3`);
      audio.preload = 'auto';
      audio.load();
      this.preloaded.set(key, audio);
    }
  }

  /** Call this on any user gesture (e.g. button click) to satisfy browser autoplay policy. */
  unlock(): void {
    if (this.unlocked) return;
    const audio = new Audio();
    audio.play().catch(() => {});
    this.unlocked = true;
  }

  play(key: AudioKey): Promise<void> {
    return new Promise(resolve => {
      const audio = this.preloaded.get(key);
      if (!audio) { resolve(); return; }
      audio.currentTime = 0;
      audio.onended = () => resolve();
      audio.onerror = () => resolve();
      audio.play().catch(() => resolve());
    });
  }

  /**
   * Schedule audio to play at a specific local Date.
   * If the date is in the past (e.g. late poll), plays immediately.
   */
  schedulePlay(key: AudioKey, playAt: Date): void {
    const delayMs = playAt.getTime() - Date.now();
    if (delayMs <= 0) {
      this.play(key);
    } else {
      setTimeout(() => this.play(key), delayMs);
    }
  }

}
