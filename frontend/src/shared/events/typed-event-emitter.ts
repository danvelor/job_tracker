export type EventMap = Record<string, unknown>;

export interface TypedEventEmitter<E extends EventMap> {
  on<K extends keyof E>(event: K, handler: (payload: E[K]) => void): void;
  off<K extends keyof E>(event: K, handler: (payload: E[K]) => void): void;
  emit<K extends keyof E>(event: K, payload: E[K]): void;
}

type HandlerStore<E extends EventMap> = {
  [K in keyof E]?: Set<(payload: E[K]) => void>;
};

export function createTypedEventEmitter<E extends EventMap>(): TypedEventEmitter<E> {
  const handlers: HandlerStore<E> = {};

  return {
    on(event, handler) {
      const existing = handlers[event];
      if (existing === undefined) {
        handlers[event] = new Set([handler]);
        return;
      }
      existing.add(handler);
    },

    off(event, handler) {
      handlers[event]?.delete(handler);
    },

    emit(event, payload) {
      handlers[event]?.forEach((handler) => handler(payload));
    },
  };
}
