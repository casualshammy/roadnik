export class Semaphore {
  private readonly p_waiters: Array<() => void> = [];
  private p_count: number;

  constructor(_count: number) {
    if (_count < 1)
      throw new Error("Semaphore count must be at least 1");

    this.p_count = _count;
  }

  async acquire(): Promise<void> {
    if (this.p_count > 0) {
      this.p_count--;
      return;
    }

    return new Promise<void>((resolve) => {
      this.p_waiters.push(resolve);
    });
  }

  release(): void {
    if (this.p_waiters.length > 0) {
      const nextResolve = this.p_waiters.shift();
      if (nextResolve)
        nextResolve();
    }
    else {
      this.p_count++;
    }
  }
}