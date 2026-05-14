import { sha256 } from "js-sha256";
import * as Consts from "../data/Consts";
import { Base64 } from 'js-base64';

const p_rgbPerColorName: Map<string, Uint8ClampedArray | null> = new Map<string, Uint8ClampedArray | null>();

export const groupBy = <T, K extends keyof any>(arr: T[], key: (i: T) => K) =>
  arr.reduce((groups, item) => {
    (groups[key(item)] ||= []).push(item);
    return groups;
  }, {} as Record<K, T[]>);

export function sleepAsync(_ms: number) {
  return new Promise<void>(_resolve => {
    setTimeout(() => {
      _resolve();
    }, _ms);
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

  return Consts.TRACK_COLORS[index];
}

export function getHeartRateString(
  _hr: number | null | undefined
): string | undefined {
  if (!_hr)
    return undefined;

  const maxHr = 200;
  var hrPercent = _hr / maxHr;

  if (hrPercent < 0.6)
    return `💙${_hr} bpm`
  if (hrPercent < 0.7)
    return `💚${_hr} bpm`
  if (hrPercent < 0.8)
    return `💛${_hr} bpm`
  if (hrPercent < 0.9)
    return `🧡${_hr} bpm`;

  return `❤️${_hr} bpm`;
}