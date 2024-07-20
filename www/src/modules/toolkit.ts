import { sha256 } from "js-sha256";
import { CLASS_IS_DRAGGING, TRACK_COLORS } from "./consts";
import { Base64 } from 'js-base64';

const p_rgbPerColorName: Map<string, Uint8ClampedArray | null> = new Map<string, Uint8ClampedArray | null>();

export const groupBy = <T, K extends keyof any>(arr: T[], key: (i: T) => K) =>
  arr.reduce((groups, item) => {
    (groups[key(item)] ||= []).push(item);
    return groups;
  }, {} as Record<K, T[]>);

export function sleepAsync(_ms: number) {
  return new Promise<void>((_resolve, _reject) => {
    let timerId: NodeJS.Timeout | null = null;
    let completed = false;
    timerId = setTimeout(() => {
      if (completed)
        return;

      timerId = null;
      completed = true;
      _resolve();
    }, _ms);
  });
}

export function makeDraggableBottomLeft(element: HTMLElement, _callback: (_left: number, _bottom: number) => void) {
  element.addEventListener('mousedown', function (ev: MouseEvent) {
    var offsetX = ev.clientX - parseInt(window.getComputedStyle(element).left);
    var offsetY = window.innerHeight - parseInt(window.getComputedStyle(element).bottom) - ev.clientY;

    function mouseMoveHandler(e: MouseEvent) {
      const style = window.getComputedStyle(element);
      const left = e.clientX - offsetX;
      const bottom = window.innerHeight - e.clientY - offsetY;
      if (left < 5 || left > window.innerWidth - parseInt(style.width) - 5)
        return;
      if (bottom < 5 || bottom > window.innerHeight - parseInt(style.height) - 5)
        return;

      element.style.left = left + 'px';
      element.style.bottom = bottom + 'px';
      element.classList.add(CLASS_IS_DRAGGING);
      _callback(left, bottom);
    }

    function reset() {
      window.removeEventListener('mousemove', mouseMoveHandler);
      window.removeEventListener('mouseup', reset);
      element.classList.remove(CLASS_IS_DRAGGING);
    }

    window.addEventListener('mousemove', mouseMoveHandler);
    window.addEventListener('mouseup', reset);
  });

  element.addEventListener('touchstart', function (ev: TouchEvent) {
    var offsetX = ev.touches[0].clientX - parseInt(window.getComputedStyle(element).left);
    var offsetY = window.innerHeight - parseInt(window.getComputedStyle(element).bottom) - ev.touches[0].clientY;

    function mouseMoveHandler(e: TouchEvent) {
      const style = window.getComputedStyle(element);
      const left = e.touches[0].clientX - offsetX;
      const bottom = window.innerHeight - e.touches[0].clientY - offsetY;
      if (left < 0 || left > window.innerWidth - parseInt(style.width))
        return;
      if (bottom < 0 || bottom > window.innerHeight - parseInt(style.height))
        return;

      element.style.left = left + 'px';
      element.style.bottom = bottom + 'px';
      element.classList.add(CLASS_IS_DRAGGING);
      _callback(left, bottom);
    }

    function reset() {
      window.removeEventListener('touchmove', mouseMoveHandler);
      window.removeEventListener('touchend', reset);
      window.removeEventListener('touchcancel', reset);
      element.classList.remove(CLASS_IS_DRAGGING);
    }

    window.addEventListener('touchmove', mouseMoveHandler);
    window.addEventListener('touchend', reset);
    window.addEventListener('touchcancel', reset);
  });
}

export function colorNameToRgba(_colorName: string): Uint8ClampedArray | null {
  const cachedValue = p_rgbPerColorName.get(_colorName);
  if (cachedValue !== undefined)
    return cachedValue;

  const canvas = document.createElement('canvas');
  const context = canvas.getContext('2d');
  if (context === null) {
    p_rgbPerColorName.set(_colorName, null);
    return null;
  }


  context.fillStyle = _colorName;
  context.fillRect(0, 0, 1, 1);
  const result = context.getImageData(0, 0, 1, 1).data;
  p_rgbPerColorName.set(_colorName, result);

  return result;
}

export function byteArrayToHexString(_byteArray: number[]): string {
  const result = Array
    .from(_byteArray, function (_byte) {
      return ('0' + (_byte & 0xFF).toString(16)).slice(-2);
    })
    .join('');
  return result;
}

export function utf8TextToBase64(_text: string): string {
  const result = Base64.encode(_text);
  return result;
}

export function base64ToUtf8Text(_base64: string): string {
  const result = Base64.decode(_base64);
  return result;
}

export function getColorForString(_str: string): string {
  const sha = sha256(_str);
  const index = parseInt(sha.substring(0, 1), 16);

  return TRACK_COLORS[index];
}

export class Pool<T> {
  private readonly p_pool: T[] = [];
  private readonly p_factory: () => T;

  constructor(_factory: () => T) {
    this.p_factory = _factory;
  }

  resolve(): T {
    const v = this.p_pool.pop() ?? this.p_factory();
    return v;
  }

  free(_value: T): void {
    this.p_pool.push(_value);
  }

  getAvailableCount(): number {
    return this.p_pool.length;
  }

}