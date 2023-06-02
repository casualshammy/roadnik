export interface StringDictionary<T> {
  [key: string]: T;
}

export interface NumberDictionary<T> {
  [key: number]: T;
}

export const groupBy = <T, K extends keyof any>(arr: T[], key: (i: T) => K) =>
  arr.reduce((groups, item) => {
    (groups[key(item)] ||= []).push(item);
    return groups;
  }, {} as Record<K, T[]>);

export class Pool<T> {
  private readonly p_pool: T[] = [];
  private readonly p_factory: () => T;

  constructor(_factory: () => T) {
    this.p_factory = _factory;
  }

  resolve() : T {
    const v = this.p_pool.pop() ?? this.p_factory();
    return v;
  }

  free(_value: T) : void {
    this.p_pool.push(_value);
  }

  getAvailableCount() : number {
    return this.p_pool.length;
  }

}