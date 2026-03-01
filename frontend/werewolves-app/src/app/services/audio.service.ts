import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AudioService {
  private voicesReady: Promise<void>;

  constructor() {
    this.voicesReady = new Promise(resolve => {
      if (typeof window === 'undefined' || !('speechSynthesis' in window)) {
        resolve();
        return;
      }
      const voices = window.speechSynthesis.getVoices();
      if (voices.length > 0) {
        resolve();
      } else {
        window.speechSynthesis.onvoiceschanged = () => resolve();
      }
    });
  }

  speak(text: string): Promise<void> {
    return this.voicesReady.then(() => new Promise(resolve => {
      if (!('speechSynthesis' in window)) { resolve(); return; }
      window.speechSynthesis.cancel();
      const utterance = new SpeechSynthesisUtterance(text);
      utterance.rate = 0.9;
      utterance.onend = () => resolve();
      utterance.onerror = () => resolve();
      window.speechSynthesis.speak(utterance);
    }));
  }
}
