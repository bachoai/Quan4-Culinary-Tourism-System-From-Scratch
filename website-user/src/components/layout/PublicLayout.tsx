import { useQuery } from '@tanstack/react-query';
import {
  HeartPulse,
  Map,
  Menu,
  Moon,
  QrCode,
  Route,
  Sun,
  UserRound,
  UtensilsCrossed,
  X,
} from 'lucide-react';
import { useState } from 'react';
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom';
import { audioApi } from '../../api/audioApi';
import { healthApi } from '../../api/healthApi';
import { ChatWidget } from '../ChatWidget';
import { LANGUAGE_OPTIONS } from '../../constants/languages';
import { getCopy } from '../../i18n/copy';
import { useAppStore } from '../../store/appStore';
import type { Lang } from '../../types/responses';
import { hasRole } from '../../utils/auth';

export function PublicLayout() {
  const {
    theme,
    toggleTheme,
    lang,
    setLang,
    currentUser,
    isAuthenticated,
    logout,
  } = useAppStore();
  const ui = getCopy(lang);
  const [open, setOpen] = useState(false);
  const location = useLocation();
  const audioLanguagesQuery = useQuery({
    queryKey: ['audio-languages'],
    queryFn: audioApi.getLanguages,
    retry: false,
    staleTime: 300000,
  });
  const healthQuery = useQuery({
    queryKey: ['api-health'],
    queryFn: healthApi.check,
    staleTime: 30000,
    retry: false,
  });

  const baseNav = [
    ['/', ui.nav.home],
    ['/explore', ui.nav.explore],
    ['/tours', ui.nav.tours],
    ['/qr', ui.nav.qr],
    ['/nearby', ui.nav.nearby],
    ['/map', ui.nav.map],
    ['/about', ui.nav.about],
  ] as const;

  const nav = hasRole(currentUser?.roles, 'Owner')
    ? [...baseNav, ['/owner', ui.nav.owner] as const]
    : baseNav;
  const supportedLanguageCodes = new Set(
    audioLanguagesQuery.data?.map((item) => item.code) ?? LANGUAGE_OPTIONS.map((item) => item.value),
  );
  const languageOptions = LANGUAGE_OPTIONS.filter((option) => supportedLanguageCodes.has(option.value));

  const healthOk = healthQuery.data?.mongoConnected && healthQuery.data.status === 'Healthy';
  const showChatWidget =
    isAuthenticated &&
    (location.pathname === '/' ||
      location.pathname === '/explore' ||
      location.pathname === '/nearby' ||
      location.pathname.startsWith('/poi/'));

  return (
    <div className="min-h-screen">
      <header className="sticky top-0 z-30 border-b border-slate-200/60 bg-slate-50/80 backdrop-blur-xl dark:border-slate-800 dark:bg-slate-950/80">
        <div className="shell flex h-18 items-center justify-between py-3">
          <Link to="/" className="flex items-center gap-2 text-lg font-extrabold">
            <span className="grid h-10 w-10 place-items-center rounded-2xl bg-coral text-white">
              <UtensilsCrossed size={20} />
            </span>
            <span>
              Quan4 <i className="font-serif text-coral">Food</i>
            </span>
          </Link>

          <nav className="hidden items-center gap-1 md:flex">
            {nav.map(([to, label]) => (
              <NavLink
                key={to}
                to={to}
                className={({ isActive }) =>
                  `rounded-full px-3 py-2 text-sm font-semibold ${
                    isActive
                      ? 'bg-orange-100 text-coral dark:bg-orange-500/15'
                      : 'text-slate-500 hover:text-slate-950 dark:text-slate-400 dark:hover:text-white'
                  }`
                }
              >
                {label}
              </NavLink>
            ))}
          </nav>

          <div className="flex items-center gap-2">
            <select
              value={lang}
              onChange={(event) => setLang(event.target.value as Lang)}
              aria-label={ui.common.languageLabel}
              className="rounded-full bg-transparent px-2 py-2 text-sm font-semibold"
            >
              {languageOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>

            <button
              onClick={toggleTheme}
              className="rounded-full p-2.5 hover:bg-slate-200 dark:hover:bg-slate-800"
              aria-label={ui.common.themeLabel}
            >
              {theme === 'dark' ? <Sun size={19} /> : <Moon size={19} />}
            </button>

            <div className="hidden items-center gap-2 md:flex">
              {isAuthenticated ? (
                <>
                  <Link to="/account" className="btn-secondary !px-4 !py-2">
                    <UserRound size={16} />
                    {currentUser?.fullName?.split(' ')[0] || ui.common.account}
                  </Link>
                  <button onClick={logout} className="pill hover:border-coral hover:text-coral">
                    {ui.common.logout}
                  </button>
                </>
              ) : (
                <>
                  <Link to="/login" className="pill hover:border-coral hover:text-coral">
                    {ui.common.login}
                  </Link>
                  <Link to="/register" className="btn-primary !px-4 !py-2">
                    {ui.common.createAccount}
                  </Link>
                </>
              )}
            </div>

            <button onClick={() => setOpen((value) => !value)} className="p-2 md:hidden">
              {open ? <X /> : <Menu />}
            </button>
          </div>
        </div>

        {open ? (
          <nav className="shell grid gap-2 border-t border-slate-200 py-3 md:hidden dark:border-slate-800">
            {nav.map(([to, label]) => (
              <NavLink
                key={to}
                to={to}
                onClick={() => setOpen(false)}
                className="rounded-xl px-3 py-2 font-semibold"
              >
                {label}
              </NavLink>
            ))}

            <div className="mt-2 grid gap-2">
              {isAuthenticated ? (
                <>
                  <Link to="/account" onClick={() => setOpen(false)} className="btn-secondary">
                    <UserRound size={16} />
                    {ui.common.account}
                  </Link>
                  <button
                    onClick={() => {
                      logout();
                      setOpen(false);
                    }}
                    className="pill py-3 text-left font-semibold"
                  >
                    {ui.common.logout}
                  </button>
                </>
              ) : (
                <>
                  <Link to="/login" onClick={() => setOpen(false)} className="btn-secondary">
                    {ui.common.login}
                  </Link>
                  <Link to="/register" onClick={() => setOpen(false)} className="btn-primary">
                    {ui.common.createAccount}
                  </Link>
                </>
              )}
            </div>
          </nav>
        ) : null}
      </header>

      <main>
        <Outlet />
      </main>

      {showChatWidget ? <ChatWidget /> : null}

      <footer className="mt-20 bg-ink py-12 text-slate-300">
        <div className="shell flex flex-col justify-between gap-8 md:flex-row">
          <div className="space-y-3">
            <p className="text-xl font-bold text-white">Quan4 Food Stories</p>
            <p className="max-w-sm text-sm leading-6">
              {ui.layout.footerDescription}
            </p>
            <div className="flex flex-wrap items-center gap-3 text-sm">
              <span className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1.5">
                <HeartPulse size={15} className={healthOk ? 'text-emerald-400' : 'text-amber-300'} />
                {healthOk ? ui.layout.apiHealthy : ui.layout.apiChecking}
              </span>
              <Link to="/qr" className="inline-flex items-center gap-2 text-coral">
                <QrCode size={15} />
                {ui.layout.qrFlow}
              </Link>
              <Link to="/tours" className="inline-flex items-center gap-2 text-teal">
                <Route size={15} />
                {ui.layout.publicTours}
              </Link>
            </div>
          </div>

          <div className="space-y-2 text-sm">
            <div className="flex items-center gap-2">
              <Map size={16} className="text-teal" />
              {ui.layout.location}
            </div>
            {isAuthenticated ? (
              <p>
                {ui.layout.helloUser} {currentUser?.fullName || ui.common.account.toLowerCase()}.
                {' '}
                {hasRole(currentUser?.roles, 'Owner') ? ui.layout.ownerMode : ui.layout.userMode}
              </p>
            ) : (
              <p>{ui.layout.loginPrompt}</p>
            )}
          </div>
        </div>
      </footer>
    </div>
  );
}
