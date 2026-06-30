import { create } from 'zustand';
import { normalizeUiLanguage, type UiLanguage } from '../i18n/copy';
import type { AuthResponse, CurrentUser, Lang } from '../types/responses';
import { track } from '../utils/analytics';

const THEME_KEY = 'q4-theme';
const LANG_KEY = 'q4-lang';
const AUDIO_LANG_KEY = 'q4-audio-lang';
const TOKEN_KEY = 'q4-token';
const USER_KEY = 'q4-current-user';

type State = {
  theme: 'light' | 'dark';
  lang: UiLanguage;
  audioLang: Lang;
  location?: { lat: number; lng: number };
  token?: string;
  currentUser?: CurrentUser;
  isAuthenticated: boolean;
  toggleTheme: () => void;
  setLang: (lang: UiLanguage) => void;
  setAudioLang: (lang: Lang) => void;
  setLocation: (location: { lat: number; lng: number }) => void;
  setAuth: (auth: AuthResponse) => void;
  setCurrentUser: (user?: CurrentUser) => void;
  logout: () => void;
};

function readStoredUser(): CurrentUser | undefined {
  const raw = localStorage.getItem(USER_KEY);
  if (!raw) {
    return undefined;
  }

  try {
    return JSON.parse(raw) as CurrentUser;
  } catch {
    localStorage.removeItem(USER_KEY);
    return undefined;
  }
}

function isSupportedAudioLanguage(value: string | null | undefined): value is Lang {
  return (
    value === 'vi' ||
    value === 'en' ||
    value === 'zh' ||
    value === 'ja' ||
    value === 'ko' ||
    value === 'fr' ||
    value === 'de' ||
    value === 'es' ||
    value === 'th' ||
    value === 'ru'
  );
}

const initialTheme = (localStorage.getItem(THEME_KEY) as 'light' | 'dark') || 'light';
const initialToken = localStorage.getItem(TOKEN_KEY) || undefined;
const initialUser = readStoredUser();
const storedUiLanguage = localStorage.getItem(LANG_KEY);
const storedAudioLanguage = localStorage.getItem(AUDIO_LANG_KEY);
const initialUiLanguage = normalizeUiLanguage(storedUiLanguage || undefined);
const initialAudioLanguage = isSupportedAudioLanguage(storedAudioLanguage)
  ? storedAudioLanguage
  : isSupportedAudioLanguage(storedUiLanguage)
    ? storedUiLanguage
    : initialUiLanguage;

document.documentElement.classList.toggle('dark', initialTheme === 'dark');

export const useAppStore = create<State>((set, get) => ({
  theme: initialTheme,
  lang: initialUiLanguage,
  audioLang: initialAudioLanguage,
  location: undefined,
  token: initialToken,
  currentUser: initialUser,
  isAuthenticated: Boolean(initialToken),
  toggleTheme: () => {
    const theme = get().theme === 'dark' ? 'light' : 'dark';
    localStorage.setItem(THEME_KEY, theme);
    document.documentElement.classList.toggle('dark', theme === 'dark');
    set({ theme });
  },
  setLang: (lang) => {
    const previousLanguage = get().lang;
    const previousAudioLanguage = get().audioLang;
    if (lang === previousLanguage) {
      return;
    }

    localStorage.setItem(LANG_KEY, lang);
    localStorage.setItem(AUDIO_LANG_KEY, lang);
    set({ lang, audioLang: lang });
    track('language_changed', lang, undefined, {
      previousLanguage,
      previousAudioLanguage,
      syncedAudioLanguage: lang,
    });
  },
  setAudioLang: (lang) => {
    const previousLanguage = get().audioLang;
    if (lang === previousLanguage) {
      return;
    }

    localStorage.setItem(AUDIO_LANG_KEY, lang);
    set({ audioLang: lang });
    track('audio_language_changed', get().lang, undefined, { previousLanguage, nextLanguage: lang });
  },
  setLocation: (location) => set({ location }),
  setAuth: ({ token, user }) => {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
    set({
      token,
      currentUser: user,
      isAuthenticated: true,
    });
  },
  setCurrentUser: (user) => {
    if (!user) {
      localStorage.removeItem(USER_KEY);
      set({ currentUser: undefined, isAuthenticated: Boolean(get().token) });
      return;
    }

    localStorage.setItem(USER_KEY, JSON.stringify(user));
    set({ currentUser: user, isAuthenticated: Boolean(get().token) });
  },
  logout: () => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    set({
      token: undefined,
      currentUser: undefined,
      isAuthenticated: false,
    });
  },
}));
