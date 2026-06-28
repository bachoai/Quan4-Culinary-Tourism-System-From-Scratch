import { create } from 'zustand';
import type { AuthResponse, CurrentUser, Lang } from '../types/responses';
import { track } from '../utils/analytics';

const THEME_KEY = 'q4-theme';
const LANG_KEY = 'q4-lang';
const TOKEN_KEY = 'q4-token';
const USER_KEY = 'q4-current-user';

type State = {
  theme: 'light' | 'dark';
  lang: Lang;
  location?: { lat: number; lng: number };
  token?: string;
  currentUser?: CurrentUser;
  isAuthenticated: boolean;
  toggleTheme: () => void;
  setLang: (lang: Lang) => void;
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

const initialTheme = (localStorage.getItem(THEME_KEY) as 'light' | 'dark') || 'light';
const initialToken = localStorage.getItem(TOKEN_KEY) || undefined;
const initialUser = readStoredUser();

document.documentElement.classList.toggle('dark', initialTheme === 'dark');

export const useAppStore = create<State>((set, get) => ({
  theme: initialTheme,
  lang: (localStorage.getItem(LANG_KEY) as Lang) || 'vi',
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
    if (lang === previousLanguage) {
      return;
    }

    localStorage.setItem(LANG_KEY, lang);
    set({ lang });
    track('language_changed', lang, undefined, { previousLanguage });
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
