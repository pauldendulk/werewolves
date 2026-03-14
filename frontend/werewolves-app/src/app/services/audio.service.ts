import { Injectable } from '@angular/core';
import { AudioKey } from '../models/audio-keys';

const LANGUAGE = 'en-US';

@Injectable({ providedIn: 'root' })
export class AudioService {
  private unlocked = false;

  /** Call this on any user gesture (e.g. button click) to satisfy browser autoplay policy. */
  unlock(): void {
    if (this.unlocked) return;
    const audio = new Audio();
    audio.play().catch(() => {});
    this.unlocked = true;
  }

  play(key: AudioKey): Promise<void> {
    return new Promise(resolve => {
      const audio = new Audio(`assets/audio/${LANGUAGE}/${key}.mp3`);
      audio.onended = () => resolve();
      audio.onerror = () => resolve();
      audio.play().catch(() => resolve());
    });
  }
}
