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