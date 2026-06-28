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
import { Link, NavLink, Outlet } from 'react-router-dom';
import { healthApi } from '../../api/healthApi';
import { useAppStore } from '../../store/appStore';
import type { Lang } from '../../types/responses';
import { hasRole } from '../../utils/auth';

const baseNav = [
  ['/', 'Trang chu'],
  ['/explore', 'Kham pha'],
  ['/tours', 'Tours'],
  ['/qr', 'Quet QR'],
  ['/nearby', 'Gan toi'],
  ['/map', 'Ban do'],
  ['/about', 'Ve du an'],
] as const;

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
  const [open, setOpen] = useState(false);
  const healthQuery = useQuery({
    queryKey: ['api-health'],
    queryFn: healthApi.check,
    staleTime: 30000,
    retry: false,
  });

  const nav = hasRole(currentUser?.roles, 'Owner')
    ? [...baseNav, ['/owner', 'Owner']]
    : baseNav;

  const healthOk = healthQuery.data?.mongoConnected && healthQuery.data.status === 'Healthy';

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
              aria-label="Ngon ngu"
              className="rounded-full bg-transparent px-2 py-2 text-sm font-semibold"
            >
              <option value="vi">VI</option>
              <option value="en">EN</option>
              <option value="zh">ZH</option>
              <option value="ja">JA</option>
              <option value="ko">KO</option>
            </select>

            <button
              onClick={toggleTheme}
              className="rounded-full p-2.5 hover:bg-slate-200 dark:hover:bg-slate-800"
              aria-label="Doi giao dien"
            >
              {theme === 'dark' ? <Sun size={19} /> : <Moon size={19} />}
            </button>

            <div className="hidden items-center gap-2 md:flex">
              {isAuthenticated ? (
                <>
                  <Link to="/account" className="btn-secondary !px-4 !py-2">
                    <UserRound size={16} />
                    {currentUser?.fullName?.split(' ')[0] || 'Tai khoan'}
                  </Link>
                  <button onClick={logout} className="pill hover:border-coral hover:text-coral">
                    Dang xuat
                  </button>
                </>
              ) : (
                <>
                  <Link to="/login" className="pill hover:border-coral hover:text-coral">
                    Dang nhap
                  </Link>
                  <Link to="/register" className="btn-primary !px-4 !py-2">
                    Tao tai khoan
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
                    Tai khoan
                  </Link>
                  <button
                    onClick={() => {
                      logout();
                      setOpen(false);
                    }}
                    className="pill py-3 text-left font-semibold"
                  >
                    Dang xuat
                  </button>
                </>
              ) : (
                <>
                  <Link to="/login" onClick={() => setOpen(false)} className="btn-secondary">
                    Dang nhap
                  </Link>
                  <Link to="/register" onClick={() => setOpen(false)} className="btn-primary">
                    Tao tai khoan
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

      <footer className="mt-20 bg-ink py-12 text-slate-300">
        <div className="shell flex flex-col justify-between gap-8 md:flex-row">
          <div className="space-y-3">
            <p className="text-xl font-bold text-white">Quan4 Food Stories</p>
            <p className="max-w-sm text-sm leading-6">
              Kham pha nhung cau chuyen am thuc song dong o Quan 4, Thanh pho Ho Chi Minh.
            </p>
            <div className="flex flex-wrap items-center gap-3 text-sm">
              <span className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1.5">
                <HeartPulse size={15} className={healthOk ? 'text-emerald-400' : 'text-amber-300'} />
                {healthOk ? 'API healthy' : 'API dang kiem tra'}
              </span>
              <Link to="/qr" className="inline-flex items-center gap-2 text-coral">
                <QrCode size={15} />
                QR user flow
              </Link>
              <Link to="/tours" className="inline-flex items-center gap-2 text-teal">
                <Route size={15} />
                Public tours
              </Link>
            </div>
          </div>

          <div className="space-y-2 text-sm">
            <div className="flex items-center gap-2">
              <Map size={16} className="text-teal" />
              Quan 4, TP. Ho Chi Minh
            </div>
            {isAuthenticated ? (
              <p>
                Xin chao {currentUser?.fullName || 'ban'}.
                {hasRole(currentUser?.roles, 'Owner') ? ' Ban dang co quyen owner.' : ' Ban dang dung che do user.'}
              </p>
            ) : (
              <p>Dang nhap de gui dang ky owner va quan ly submission cua ban.</p>
            )}
          </div>
        </div>
      </footer>
    </div>
  );
}
