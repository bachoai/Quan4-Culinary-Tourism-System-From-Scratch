import { analyticsApi } from '../api/analyticsApi';

const GUEST_KEY = 'q4-guest';
const SESSION_KEY = 'q4-session';

const id = (key: string) => {
  let value = localStorage.getItem(key);
  if (!value) {
    value = `${key}-${crypto.randomUUID()}`;
    localStorage.setItem(key, value);
  }
  return value;
};

function collect(eventName: string, lang: string, payload?: { poiId?: string; metadata?: object; pageViewId?: string }) {
  return analyticsApi
    .collect({
      eventName,
      anonymousId: id(GUEST_KEY),
      sessionId: id(SESSION_KEY),
      pageViewId: payload?.pageViewId ?? crypto.randomUUID(),
      lang,
      poiId: payload?.poiId,
      metadata: payload?.metadata ?? {},
    })
    .catch(() => console.warn('Analytics unavailable'));
}

export const track = (eventName: string, lang: string, poiId?: string, metadata?: object) =>
  collect(eventName, lang, { poiId, metadata });

export const sendPresencePing = (
  lang: string,
  metadata: {
    path: string;
    title?: string;
    isAuthenticated: boolean;
  },
  pageViewId: string,
) =>
  collect('presence_ping', lang, {
    pageViewId,
    metadata,
  });

export const distance = (m: number) => (m < 1000 ? `${Math.round(m)} m` : `${(m / 1000).toFixed(1)} km`);
