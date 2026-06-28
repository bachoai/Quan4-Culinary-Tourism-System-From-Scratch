import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AnimatePresence, motion } from 'framer-motion';
import {
  ArrowRight,
  CheckCircle2,
  Clock3,
  Compass,
  LoaderCircle,
  LogOut,
  MapPin,
  Navigation,
  QrCode,
  Route as RouteIcon,
  Search,
  ShieldCheck,
  Sparkles,
  Star,
  UserRound,
  Utensils,
  Volume2,
  XCircle,
} from 'lucide-react';
import { Component, useEffect, useRef, useState } from 'react';
import {
  Link,
  Navigate,
  Route,
  Routes,
  useLocation,
  useNavigate,
  useParams,
  useSearchParams,
} from 'react-router-dom';
import { authApi } from './api/authApi';
import { categoryApi } from './api/categoryApi';
import { ownerApi } from './api/ownerApi';
import { poiApi } from './api/poiApi';
import { qrApi } from './api/qrApi';
import { tourApi } from './api/tourApi';
import { audioApi } from './api/audioApi';
import { routeApi } from './api/routeApi';
import { PublicLayout } from './components/layout/PublicLayout';
import { PoiCard } from './components/common/PoiCard';
import { AudioPlayer } from './components/common/AudioPlayer';
import { PoiMap } from './components/map/PoiMap';
import { getCopy } from './i18n/copy';
import { useAppStore } from './store/appStore';
import type {
  CurrentUser,
  Lang,
  OwnerManagedPoi,
  OwnerSubmissionResponse,
  Poi,
  QrActivationResponse,
  TourResponse,
} from './types/responses';
import type {
  CreateOwnerSubmissionRequest,
  LoginRequest,
  RegisterRequest,
} from './types/requests';
import { distance, sendPresencePing, track } from './utils/analytics';
import { hasRole } from './utils/auth';
import { normalizeMediaUrl, poiImage } from './utils/media';

const heroImage =
  'https://images.unsplash.com/photo-1551218808-94e220e084d2?auto=format&fit=crop&w=1800&q=85';

function Spinner() {
  return (
    <div className="grid min-h-48 place-items-center">
      <LoaderCircle className="animate-spin text-coral" size={30} />
    </div>
  );
}

function ErrorBox({ text }: { text?: string }) {
  const lang = useAppStore((state) => state.lang);
  const ui = getCopy(lang);

  return (
    <div className="rounded-3xl border border-orange-200 bg-orange-50 p-8 text-center text-orange-800">
      {text || ui.common.loadError}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="grid gap-2 text-sm font-semibold text-slate-700 dark:text-slate-200">
      <span>{label}</span>
      {children}
    </label>
  );
}

function TextInput(props: React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      className={`rounded-2xl border border-slate-200 bg-white px-4 py-3 outline-none transition focus:border-coral dark:border-slate-700 dark:bg-slate-900 ${props.className || ''}`}
    />
  );
}

function TextArea(props: React.TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      {...props}
      className={`min-h-28 rounded-2xl border border-slate-200 bg-white px-4 py-3 outline-none transition focus:border-coral dark:border-slate-700 dark:bg-slate-900 ${props.className || ''}`}
    />
  );
}

function StatusPill({
  status,
}: {
  status: 'pending' | 'approved' | 'rejected' | string;
}) {
  const classes =
    status === 'approved'
      ? 'bg-emerald-100 text-emerald-700'
      : status === 'rejected'
        ? 'bg-rose-100 text-rose-700'
        : 'bg-amber-100 text-amber-700';

  return <span className={`rounded-full px-3 py-1 text-xs font-bold uppercase ${classes}`}>{status}</span>;
}

function SessionBootstrap() {
  const token = useAppStore((state) => state.token);
  const setCurrentUser = useAppStore((state) => state.setCurrentUser);
  const logout = useAppStore((state) => state.logout);
  const meQuery = useQuery({
    queryKey: ['auth-me', token],
    queryFn: authApi.me,
    enabled: Boolean(token),
    staleTime: 300000,
    retry: false,
  });

  useEffect(() => {
    if (meQuery.data) {
      setCurrentUser(meQuery.data);
    }
  }, [meQuery.data, setCurrentUser]);

  useEffect(() => {
    if (meQuery.error) {
      logout();
    }
  }, [logout, meQuery.error]);

  return null;
}

function PresenceHeartbeat() {
  const location = useLocation();
  const lang = useAppStore((state) => state.lang);
  const isAuthenticated = useAppStore((state) => state.isAuthenticated);
  const pageViewIdRef = useRef(crypto.randomUUID());
  const currentPath = `${location.pathname}${location.search}`;

  useEffect(() => {
    pageViewIdRef.current = crypto.randomUUID();
  }, [currentPath]);

  useEffect(() => {
    let intervalId: number | undefined;

    const send = () => {
      if (document.visibilityState !== 'visible') {
        return;
      }

      sendPresencePing(
        lang,
        {
          path: currentPath,
          title: document.title,
          isAuthenticated,
        },
        pageViewIdRef.current,
      );
    };

    const stop = () => {
      if (intervalId) {
        window.clearInterval(intervalId);
        intervalId = undefined;
      }
    };

    const start = () => {
      stop();
      send();
      intervalId = window.setInterval(send, 20000);
    };

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        start();
        return;
      }

      stop();
    };

    const handleFocus = () => send();

    handleVisibilityChange();
    document.addEventListener('visibilitychange', handleVisibilityChange);
    window.addEventListener('focus', handleFocus);

    return () => {
      stop();
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      window.removeEventListener('focus', handleFocus);
    };
  }, [currentPath, isAuthenticated, lang]);

  return null;
}

function RequireAuth({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAppStore((state) => state.isAuthenticated);
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to={`/login?next=${encodeURIComponent(`${location.pathname}${location.search}`)}`} replace />;
  }

  return <>{children}</>;
}

function RequireOwner({ children }: { children: React.ReactNode }) {
  const token = useAppStore((state) => state.token);
  const currentUser = useAppStore((state) => state.currentUser);
  const setCurrentUser = useAppStore((state) => state.setCurrentUser);
  const meQuery = useQuery({
    queryKey: ['owner-guard-me', token],
    queryFn: authApi.me,
    enabled: Boolean(token),
    staleTime: 0,
    refetchOnMount: 'always',
    retry: false,
  });

  useEffect(() => {
    if (meQuery.data) {
      setCurrentUser(meQuery.data);
    }
  }, [meQuery.data, setCurrentUser]);

  const resolvedCurrentUser = meQuery.data ?? currentUser;

  if (meQuery.isLoading && !resolvedCurrentUser) {
    return <Spinner />;
  }

  if (!hasRole(resolvedCurrentUser?.roles, 'Owner')) {
    return <Navigate to="/account" replace />;
  }

  return <>{children}</>;
}

function Categories({
  selected,
  onSelect,
}: {
  selected?: string;
  onSelect?: (id?: string) => void;
}) {
  const lang = useAppStore((state) => state.lang);
  const ui = getCopy(lang);
  const { data = [] } = useQuery({
    queryKey: ['categories'],
    queryFn: categoryApi.list,
  });

  return (
    <div className="flex gap-2 overflow-x-auto pb-2">
      <button
        onClick={() => onSelect?.()}
        className={`pill whitespace-nowrap ${!selected ? 'border-coral bg-orange-50 text-coral dark:bg-orange-500/15' : ''}`}
      >
        {ui.common.all}
      </button>

      {data.map((category) => (
        <button
          key={category.id}
          onClick={() => onSelect?.(category.id)}
          className={`pill whitespace-nowrap ${selected === category.id ? 'border-coral bg-orange-50 text-coral dark:bg-orange-500/15' : 'hover:border-teal'}`}
        >
          {category.name}
        </button>
      ))}
    </div>
  );
}

function Home() {
  const navigate = useNavigate();
  const { lang } = useAppStore();
  const ui = getCopy(lang);
  const { data: pois = [], isLoading, isError } = useQuery({
    queryKey: ['pois', lang],
    queryFn: () => poiApi.list({ lang }),
  });
  const { data: categories = [] } = useQuery({
    queryKey: ['categories'],
    queryFn: categoryApi.list,
  });
  const [keyword, setKeyword] = useState('');

  return (
    <>
      <section className="shell pt-8 sm:pt-12">
        <div className="relative isolate overflow-hidden rounded-[2.5rem] bg-ink px-6 py-16 text-white sm:px-12 lg:py-24">
          <img
            src={heroImage}
            className="absolute inset-0 -z-10 h-full w-full object-cover opacity-35"
            alt="Món ăn Quận 4"
          />
          <div className="absolute inset-0 -z-10 bg-gradient-to-r from-slate-950 via-slate-950/75 to-transparent" />

          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="max-w-2xl">
            <p className="section-kicker !text-white/70">{ui.home.heroKicker}</p>
            <h1 className="mt-4 font-serif text-4xl font-bold leading-tight sm:text-6xl">
              {ui.home.heroTitleLead} <span className="text-coral">{ui.home.heroTitleAccent}</span>
            </h1>
            <p className="mt-5 max-w-xl text-base leading-7 text-slate-200 sm:text-lg">
              {ui.home.heroDescription}
            </p>

            <div className="mt-8 flex flex-wrap gap-3">
              <Link className="btn-primary" to="/explore">
                {ui.home.heroExplore} <ArrowRight size={18} />
              </Link>
              <Link className="btn-secondary !border-white/20 !bg-white/10 !text-white" to="/qr">
                <QrCode size={18} />
                {ui.home.heroQr}
              </Link>
              <Link className="btn-secondary !border-white/20 !bg-white/10 !text-white" to="/tours">
                <RouteIcon size={18} />
                {ui.home.heroTours}
              </Link>
            </div>
          </motion.div>

          <form
            onSubmit={(event) => {
              event.preventDefault();
              navigate(`/explore${keyword ? `?q=${encodeURIComponent(keyword)}` : ''}`);
            }}
            className="mt-10 flex max-w-xl items-center gap-2 rounded-2xl bg-white p-2 shadow-soft"
          >
            <Search className="ml-2 text-slate-400" />
            <input
              value={keyword}
              onChange={(event) => setKeyword(event.target.value)}
              placeholder={ui.home.heroSearchPlaceholder}
              className="min-w-0 flex-1 bg-transparent py-2 text-slate-900 outline-none"
            />
            <button className="btn-primary shrink-0">
              {ui.common.search}
            </button>
          </form>
        </div>
      </section>

      <section className="shell py-14">
        <p className="section-kicker">{ui.home.categoryKicker}</p>
        <h2 className="mt-2 text-3xl font-bold sm:text-4xl">{ui.home.categoryTitle}</h2>
        <div className="mt-6">
          <Categories onSelect={(id) => navigate(`/explore${id ? `?category=${id}` : ''}`)} />
        </div>
      </section>

      <section className="shell">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="section-kicker">{ui.home.featuredKicker}</p>
            <h2 className="mt-2 text-3xl font-bold sm:text-4xl">{ui.home.featuredTitle}</h2>
          </div>
          <div className="flex gap-3">
            <Link to="/explore" className="hidden font-bold text-coral sm:block">
              {ui.home.viewAll}
            </Link>
            <Link to="/tours" className="hidden font-bold text-teal sm:block">
              {ui.layout.publicTours}
            </Link>
          </div>
        </div>

        {isLoading ? (
          <Spinner />
        ) : isError ? (
          <ErrorBox />
        ) : (
          <div className="mt-7 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {pois.slice(0, 6).map((poi) => (
              <PoiCard
                key={poi.id}
                poi={poi}
                category={categories.find((category) => category.id === poi.categoryId)?.name}
              />
            ))}
          </div>
        )}
      </section>

      <section className="shell py-20">
        <div className="grid gap-5 md:grid-cols-4">
          {[
            [Compass, ui.home.featureMapTitle, ui.home.featureMapText],
            [Volume2, ui.home.featureAudioTitle, ui.home.featureAudioText],
            [Sparkles, ui.home.featureI18nTitle, ui.home.featureI18nText],
            [QrCode, ui.home.featureQrTitle, ui.home.featureQrText],
          ].map(([Icon, title, text]) => {
            const CurrentIcon = Icon as typeof Compass;

            return (
              <div
                key={title as string}
                className="rounded-3xl border border-slate-200 bg-white p-7 dark:border-slate-800 dark:bg-slate-900"
              >
                <span className="grid h-12 w-12 place-items-center rounded-2xl bg-teal/10 text-teal">
                  <CurrentIcon />
                </span>
                <h3 className="mt-5 text-xl font-bold">{title as string}</h3>
                <p className="mt-2 leading-6 text-slate-500">{text as string}</p>
              </div>
            );
          })}
        </div>

        <div className="mt-8 grid gap-6 rounded-[2rem] bg-teal p-8 text-ink md:grid-cols-[1fr_auto_auto_auto] md:items-center">
          <div>
            <p className="section-kicker !text-ink/70">{ui.home.contractKicker}</p>
            <h2 className="mt-2 text-3xl font-bold">{ui.home.contractTitle}</h2>
          </div>
          <Link className="btn-secondary !border-ink !bg-ink !text-white" to="/map">
            {ui.home.mapCta} <MapPin size={18} />
          </Link>
          <Link className="btn-secondary !border-ink !bg-white/20 !text-ink" to="/qr">
            {ui.home.qrCta} <QrCode size={18} />
          </Link>
          <Link className="btn-secondary !border-ink !bg-white/20 !text-ink" to="/account">
            {ui.home.accountCta} <UserRound size={18} />
          </Link>
        </div>
      </section>
    </>
  );
}

function Explore() {
  const { lang } = useAppStore();
  const navigate = useNavigate();
  const ui = getCopy(lang);
  const location = useLocation();
  const params = new URLSearchParams(location.search);
  const [keyword, setKeyword] = useState(params.get('q') || '');
  const [categoryId, setCategoryId] = useState(params.get('category') || '');
  const [priceRange, setPriceRange] = useState('');

  const query = useQuery({
    queryKey: ['explore', lang, keyword, categoryId, priceRange],
    queryFn: () => {
      const hasFilters = Boolean(keyword || categoryId || priceRange);
      const payload = {
        lang,
        keyword: keyword || undefined,
        categoryId: categoryId || undefined,
        priceRange: priceRange || undefined,
      };

      return hasFilters ? poiApi.search(payload) : poiApi.list({ lang });
    },
  });

  const runSearch = (event: React.FormEvent) => {
    event.preventDefault();
    track('search_executed', lang, undefined, { hasKeyword: Boolean(keyword) });
    const next = new URLSearchParams();
    if (keyword) {
      next.set('q', keyword);
    }
    if (categoryId) {
      next.set('category', categoryId);
    }
    navigate(`/explore${next.toString() ? `?${next.toString()}` : ''}`);
  };

  return (
    <section className="shell py-12">
      <p className="section-kicker">{ui.explore.kicker}</p>
      <h1 className="mt-2 text-4xl font-bold">{ui.explore.title}</h1>
      <p className="mt-3 max-w-2xl text-slate-500">
        {ui.explore.subtitle}
      </p>

      <form
        onSubmit={runSearch}
        className="mt-7 flex rounded-2xl border border-slate-200 bg-white p-2 dark:border-slate-800 dark:bg-slate-900"
      >
        <Search className="m-2 text-slate-400" />
        <input
          value={keyword}
          onChange={(event) => setKeyword(event.target.value)}
          className="min-w-0 flex-1 bg-transparent outline-none"
          placeholder={ui.explore.searchPlaceholder}
        />
        <button className="btn-primary shrink-0">
          {ui.common.search}
        </button>
      </form>

      <div className="mt-6">
        <Categories selected={categoryId} onSelect={(id) => setCategoryId(id || '')} />
      </div>

      <div className="mt-4 flex gap-2">
        <span className="self-center text-sm font-semibold uppercase tracking-[0.2em] text-slate-500">
          {ui.explore.priceLabel}
        </span>
        {['$', '$$', '$$$'].map((price) => (
          <button
            key={price}
            onClick={() => setPriceRange(priceRange === price ? '' : price)}
            className={`pill ${priceRange === price ? 'border-coral bg-orange-50 text-coral' : ''}`}
          >
            {price}
          </button>
        ))}
      </div>

      {query.isLoading ? (
        <Spinner />
      ) : query.isError ? (
        <ErrorBox text={(query.error as Error).message} />
      ) : query.data?.length ? (
        <div className="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {query.data.map((poi) => (
            <PoiCard key={poi.id} poi={poi} />
          ))}
        </div>
      ) : (
        <div className="mt-8 rounded-3xl bg-slate-100 p-12 text-center dark:bg-slate-900">
          <Utensils className="mx-auto text-teal" />
          <h2 className="mt-3 text-xl font-bold">{ui.explore.emptyTitle}</h2>
          <p className="mt-1 text-slate-500">{ui.explore.emptyDescription}</p>
        </div>
      )}
    </section>
  );
}

class RouteErrorBoundary extends Component<
  { fallbackText: string; children: React.ReactNode },
  { hasError: boolean }
> {
  state = { hasError: false };

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error: Error) {
    console.error(error);
  }

  render() {
    if (this.state.hasError) {
      return (
        <section className="shell py-12">
          <ErrorBox text={this.props.fallbackText} />
        </section>
      );
    }

    return this.props.children;
  }
}

function Detail() {
  const { id = '' } = useParams();
  const location = useLocation();
  const { lang } = useAppStore();
  const ui = getCopy(lang);
  const queryParams = new URLSearchParams(location.search);
  const narrationLang: Lang = lang;
  const autoplay = queryParams.get('autoplay');
  const source = queryParams.get('source');
  const { data: poi, isLoading, isError } = useQuery({
    queryKey: ['detail', id, lang],
    queryFn: () => poiApi.detail(id, lang),
  });
  const audioQuery = useQuery({
    queryKey: ['audio', id, narrationLang],
    queryFn: () => audioApi.byPoi(id, narrationLang),
    enabled: Boolean(id),
  });

  useEffect(() => {
    if (id) {
      track('poi_viewed', lang, id, source ? { source } : undefined);
    }
  }, [id, lang, source]);

  if (isLoading) {
    return <Spinner />;
  }

  if (isError || !poi) {
    return (
      <section className="shell py-20">
        <ErrorBox />
      </section>
    );
  }

  const mapUrl = `https://www.google.com/maps/dir/?api=1&destination=${poi.latitude},${poi.longitude}`;
  const narrationText = poi.ttsScript || poi.description || '';

  return (
    <section className="shell py-8">
      <Link to="/explore" className="text-sm font-bold text-coral">
        {ui.detail.back}
      </Link>

      {source === 'qr' ? (
        <div className="mt-5 rounded-2xl border border-teal/20 bg-teal/10 px-4 py-3 text-sm text-teal-900 dark:text-teal-100">
          {ui.detail.qrOpened}
        </div>
      ) : null}

      <div className="mt-5 grid gap-8 lg:grid-cols-[1.15fr_.85fr]">
        <motion.img
          initial={{ opacity: 0, scale: 0.97 }}
          animate={{ opacity: 1, scale: 1 }}
          className="h-80 w-full rounded-[2rem] object-cover sm:h-[30rem]"
          src={poiImage(poi)}
          alt={poi.name}
        />

        <div className="py-3">
          <p className="section-kicker">{ui.detail.kicker}</p>
          <h1 className="mt-2 text-4xl font-bold">{poi.name}</h1>

          <div className="mt-4 flex flex-wrap gap-2">
            <span className="pill border-coral text-coral">{poi.priceRange}</span>
            {poi.rating > 0 ? (
              <span className="pill flex items-center gap-1">
                <Star size={15} className="fill-amber-400 text-amber-400" />
                {poi.rating.toFixed(1)} ({poi.reviewCount})
              </span>
            ) : null}
          </div>

          <p className="mt-6 leading-7 text-slate-600 dark:text-slate-300">{poi.description}</p>
          <p className="mt-5 flex gap-2 text-sm text-slate-500">
            <MapPin size={18} className="shrink-0 text-coral" />
            {poi.address}, {poi.district}
          </p>

          <div className="mt-6">
            <AudioPlayer
              audioUrl={audioQuery.data?.audioUrl}
              text={narrationText}
              lang={narrationLang}
              autoplay={Boolean(autoplay)}
              loading={audioQuery.isLoading}
              errorText={audioQuery.isError ? ui.detail.audioError : undefined}
              onPlay={(mode) =>
                track(
                  mode === 'audio' ? 'audio_played' : 'tts_played',
                  narrationLang,
                  poi.id,
                  autoplay ? { autoplay, mode } : { mode },
                )
              }
            />
          </div>

          <div className="mt-5 flex gap-3">
            <Link className="btn-primary" to={`/map?poi=${poi.id}`}>
              <Navigation size={17} />
              {ui.detail.direction}
            </Link>
            <a className="btn-secondary" target="_blank" rel="noreferrer" href={mapUrl}>
              Google Maps
            </a>
          </div>
        </div>
      </div>

      <div className="mt-9 grid gap-6 md:grid-cols-2">
        <div className="rounded-3xl bg-white p-6 shadow-soft dark:bg-slate-900">
          <h2 className="text-xl font-bold">{ui.detail.hoursTitle}</h2>
          <div className="mt-3 space-y-2 text-sm">
            {poi.openingHours?.length ? (
              poi.openingHours.map((item) => (
                <p key={item.dayOfWeek} className="flex justify-between gap-6">
                  <span>{item.dayOfWeek}</span>
                  <b>{item.isClosed ? ui.detail.closed : `${item.openTime} - ${item.closeTime}`}</b>
                </p>
              ))
            ) : (
              <p className="text-slate-500">{ui.detail.notUpdated}</p>
            )}
          </div>
        </div>

        <div className="rounded-3xl bg-white p-6 shadow-soft dark:bg-slate-900">
          <h2 className="text-xl font-bold">{ui.detail.infoTagsTitle}</h2>
          <div className="mt-3 flex flex-wrap gap-2">
            {poi.tags?.map((tag) => (
              <span key={tag} className="pill bg-slate-50 dark:bg-slate-800">
                #{tag}
              </span>
            ))}
          </div>
          {poi.contactInfo?.phone ? (
            <p className="mt-5 text-sm text-slate-500">{poi.contactInfo.phone}</p>
          ) : null}
        </div>
      </div>
    </section>
  );
}

function Nearby() {
  const { lang, setLocation } = useAppStore();
  const [radius, setRadius] = useState(3000);
  const [coords, setCoords] = useState<{ lat: number; lng: number }>();
  const [locationError, setLocationError] = useState('');
  const hasValidCoords = Boolean(coords && Number.isFinite(coords.lat) && Number.isFinite(coords.lng));

  const query = useQuery({
    queryKey: ['nearby', coords, radius, lang],
    queryFn: async () => {
      if (!coords || !Number.isFinite(coords.lat) || !Number.isFinite(coords.lng)) {
        return [];
      }

      const data = await poiApi.nearby({ lat: coords.lat, lng: coords.lng, radius, limit: 20, lang });
      return Array.isArray(data) ? data : [];
    },
    enabled: hasValidCoords,
    retry: 1,
  });

  const nearbyPois = Array.isArray(query.data) ? query.data : [];

  const safeTrackNearby = () => {
    try {
      track('nearby_requested', lang, undefined, { radius });
    } catch (error) {
      console.warn('Nearby analytics unavailable', error);
    }
  };

  const formatNearbyDistance = (meters?: number) => {
    if (typeof meters !== 'number' || !Number.isFinite(meters)) {
      return 'Đang cập nhật';
    }

    return distance(meters);
  };

  const locate = (fallback = false) => {
    const setCurrentLocation = (lat: number, lng: number) => {
      setCoords({ lat, lng });
      setLocation({ lat, lng });
      setLocationError('');
      safeTrackNearby();
    };

    if (fallback) {
      setCurrentLocation(10.7578, 106.706);
      return;
    }

    if (!navigator.geolocation) {
      setLocationError('Trình duyệt này không hỗ trợ định vị. Hãy dùng vị trí mặc định Quận 4.');
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => setCurrentLocation(position.coords.latitude, position.coords.longitude),
      () => setLocationError('Không lấy được GPS. Bạn có thể thử lại hoặc dùng vị trí mặc định Quận 4.'),
      { enableHighAccuracy: true, timeout: 10000 },
    );
  };

  return (
    <section className="shell py-12">
      <p className="section-kicker">GOI Y QUANH BAN</p>
      <h1 className="mt-2 text-4xl font-bold">Tìm quán gần tôi</h1>
      <p className="mt-3 max-w-xl text-slate-500">
        Cho phép trình duyệt sử dụng vị trí để tìm những hương vị đáng thử gần bạn nhất.
      </p>

      <div className="mt-7 rounded-3xl bg-ink p-7 text-white">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="font-bold">Bán kính tìm kiếm</p>
            <div className="mt-3 flex gap-2">
              {[1000, 3000, 5000, 10000].map((item) => (
                <button
                  key={item}
                  onClick={() => setRadius(item)}
                  className={`rounded-full px-3 py-1.5 text-sm ${radius === item ? 'bg-coral' : 'bg-white/10'}`}
                >
                  {item / 1000} km
                </button>
              ))}
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            <button onClick={() => locate()} className="btn-primary">
              <Navigation size={17} />
              Lấy vị trí hiện tại
            </button>
            <button
              onClick={() => locate(true)}
              className="btn-secondary !border-white/20 !bg-transparent !text-white"
            >
              Dùng vị trí Quận 4
            </button>
          </div>
        </div>
        {locationError ? <p className="mt-4 text-sm text-amber-200">{locationError}</p> : null}
      </div>

      {query.isLoading ? (
        <Spinner />
      ) : query.isError ? (
        <ErrorBox text="Không tìm được địa điểm gần đây. Thử lại sau." />
      ) : nearbyPois.length ? (
        <div className="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {nearbyPois.map((poi) => (
            <div key={poi.id} className="relative">
              <PoiCard poi={poi} />
              <span className="absolute right-5 top-4 rounded-full bg-ink px-2 py-1 text-xs font-bold text-white">
                {formatNearbyDistance(poi.distanceMeters)}
              </span>
            </div>
          ))}
        </div>
      ) : hasValidCoords ? (
        <div className="mt-8 rounded-3xl bg-slate-100 p-12 text-center dark:bg-slate-900">
          <Navigation className="mx-auto text-teal" />
          <h2 className="mt-3 text-xl font-bold">Chưa có địa điểm phù hợp</h2>
          <p className="mt-1 text-slate-500">Hãy tăng bán kính tìm kiếm hoặc thử lại với vị trí khác.</p>
        </div>
      ) : null}
    </section>
  );
}

function MapPage() {
  const { lang, location, setLocation } = useAppStore();
  const [searchParams, setSearchParams] = useSearchParams();
  const [geoError, setGeoError] = useState('');
  const [isLocating, setIsLocating] = useState(false);
  const selectedPoiId = searchParams.get('poi') || '';
  const { data: pois = [], isLoading, isError } = useQuery({
    queryKey: ['map-pois', lang],
    queryFn: () => poiApi.list({ lang }),
  });
  const selectedPoi = pois.find((poi) => poi.id === selectedPoiId);
  const routeQuery = useQuery({
    queryKey: ['map-route', selectedPoiId, location?.lat, location?.lng],
    queryFn: () =>
      routeApi.between(
        { lat: location!.lat, lng: location!.lng },
        { lat: selectedPoi!.latitude, lng: selectedPoi!.longitude },
      ),
    enabled: Boolean(selectedPoi && location),
    staleTime: 30000,
    retry: 1,
  });

  useEffect(() => {
    if (!navigator.geolocation) {
      setGeoError('Trinh duyet nay khong ho tro GPS.');
      return;
    }

    const syncLocation = (position: GeolocationPosition) => {
      setLocation({
        lat: position.coords.latitude,
        lng: position.coords.longitude,
      });
      setGeoError('');
      setIsLocating(false);
    };

    const showError = () => {
      setGeoError('Không lấy được vị trí hiện tại. Hãy bật GPS và thử lại.');
      setIsLocating(false);
    };

    setIsLocating(true);
    navigator.geolocation.getCurrentPosition(syncLocation, showError, {
      enableHighAccuracy: true,
      timeout: 15000,
      maximumAge: 15000,
    });

    const watchId = navigator.geolocation.watchPosition(syncLocation, () => undefined, {
      enableHighAccuracy: true,
      timeout: 20000,
      maximumAge: 15000,
    });

    return () => navigator.geolocation.clearWatch(watchId);
  }, [setLocation]);

  useEffect(() => {
    if (!selectedPoiId || selectedPoi || !pois.length) {
      return;
    }

    const next = new URLSearchParams(searchParams);
    next.delete('poi');
    setSearchParams(next, { replace: true });
  }, [pois, searchParams, selectedPoi, selectedPoiId, setSearchParams]);

  const focusPoi = (poiId: string) => {
    const next = new URLSearchParams(searchParams);
    next.set('poi', poiId);
    setSearchParams(next, { replace: true });
  };

  const clearFocusPoi = () => {
    const next = new URLSearchParams(searchParams);
    next.delete('poi');
    setSearchParams(next, { replace: true });
  };

  const refreshLocation = () => {
    if (!navigator.geolocation) {
      setGeoError('Trinh duyet nay khong ho tro GPS.');
      return;
    }

    setIsLocating(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        setLocation({
          lat: position.coords.latitude,
          lng: position.coords.longitude,
        });
        setGeoError('');
        setIsLocating(false);
      },
      () => {
        setGeoError('Không lấy được vị trí hiện tại. Hãy bật GPS và thử lại.');
        setIsLocating(false);
      },
      {
        enableHighAccuracy: true,
        timeout: 15000,
        maximumAge: 0,
      },
    );
  };

  const durationText = routeQuery.data
    ? routeQuery.data.durationSeconds >= 3600
      ? `${Math.floor(routeQuery.data.durationSeconds / 3600)}h ${Math.round((routeQuery.data.durationSeconds % 3600) / 60)}m`
      : `${Math.max(1, Math.round(routeQuery.data.durationSeconds / 60))} phut`
    : null;

  return (
    <section className="shell py-12">
      <p className="section-kicker">DINH VI HUONG VI</p>
      <h1 className="mt-2 text-4xl font-bold">Bản đồ ẩm thực</h1>
      <p className="mt-2 text-slate-500">Lấy vị trí thật của bạn, chọn một POI và xem đường đi ngay trên bản đồ.</p>

      <div className="mt-6 grid gap-4 rounded-[2rem] bg-white p-5 shadow-soft dark:bg-slate-900 md:grid-cols-[1.2fr_.8fr_auto] md:items-center">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">GPS thời gian thực</p>
          <p className="mt-2 text-sm text-slate-500">
            {location
              ? `Vị trí hiện tại: ${location.lat.toFixed(5)}, ${location.lng.toFixed(5)}`
              : isLocating
                ? 'Đang lấy vị trí hiện tại của bạn...'
                : 'Chưa lấy được vị trí. Bấm nút bên phải để bật định vị.'}
          </p>
          {geoError ? <p className="mt-2 text-sm text-rose-600">{geoError}</p> : null}
        </div>
        <div className="rounded-2xl bg-slate-50 p-4 dark:bg-slate-800">
          {selectedPoi ? (
            <>
              <p className="text-xs font-bold uppercase tracking-[0.2em] text-teal">POI đang chọn</p>
              <p className="mt-2 text-lg font-bold">{selectedPoi.name}</p>
              <p className="mt-1 text-sm text-slate-500">{selectedPoi.address}</p>
              {routeQuery.data ? (
                <div className="mt-3 flex flex-wrap gap-2 text-sm">
                  <span className="pill border-teal text-teal">{distance(routeQuery.data.distanceMeters)}</span>
                  <span className="pill">{durationText}</span>
                </div>
              ) : routeQuery.isLoading ? (
                <p className="mt-2 text-sm text-slate-500">Đang tìm đường đi trên mạng lưới đường phố...</p>
              ) : routeQuery.isError ? (
                <p className="mt-2 text-sm text-rose-600">{(routeQuery.error as Error).message}</p>
              ) : location ? (
                <p className="mt-2 text-sm text-slate-500">Đang sẵn sàng chỉ đường ngay khi có route.</p>
              ) : (
                <p className="mt-2 text-sm text-slate-500">Cần vị trí thật của bạn để vẽ đường đi.</p>
              )}
            </>
          ) : (
            <>
              <p className="text-xs font-bold uppercase tracking-[0.2em] text-teal">Chưa chọn điểm đến</p>
              <p className="mt-2 text-sm text-slate-500">Hãy bấm vào marker hoặc một dòng trong danh sách để xem route và số km.</p>
            </>
          )}
        </div>
        <div className="flex flex-wrap gap-2 md:justify-end">
          <button type="button" className="btn-primary" onClick={refreshLocation} disabled={isLocating}>
            <Navigation size={17} />
            {isLocating ? 'Đang lấy GPS' : 'Lấy vị trí của tôi'}
          </button>
          {selectedPoi ? (
            <button type="button" className="btn-secondary" onClick={clearFocusPoi}>
              Bỏ chọn điểm
            </button>
          ) : null}
        </div>
      </div>

      <div className="mt-7 grid gap-5 lg:grid-cols-[1.3fr_.7fr]">
        <PoiMap
          pois={pois}
          userLocation={location}
          selectedPoiId={selectedPoiId || undefined}
          routeGeometry={routeQuery.data?.geometry}
          onSelectPoi={focusPoi}
        />
        <div className="max-h-[620px] space-y-3 overflow-y-auto pr-1 md:max-h-[700px] xl:max-h-[760px]">
          {isLoading ? (
            <Spinner />
          ) : isError ? (
            <ErrorBox />
          ) : (
            pois.map((poi) => (
              <div
                key={poi.id}
                className={`rounded-2xl bg-white p-4 shadow-sm transition dark:bg-slate-900 ${selectedPoiId === poi.id ? 'ring-2 ring-teal' : 'hover:shadow-soft'}`}
              >
                <button type="button" className="block w-full text-left" onClick={() => focusPoi(poi.id)}>
                  <p className="font-bold">{poi.name}</p>
                  <p className="mt-1 flex items-center gap-1 text-sm text-slate-500">
                    <MapPin size={14} />
                    {poi.address}
                  </p>
                </button>
                <div className="mt-3 flex flex-wrap gap-2">
                  <button type="button" className="pill border-teal text-teal" onClick={() => focusPoi(poi.id)}>
                    {selectedPoiId === poi.id ? 'Đang chỉ đường' : 'Chỉ đường trên map'}
                  </button>
                  <Link to={`/poi/${poi.id}`} className="pill">
                    Xem chi tiết
                  </Link>
                </div>
              </div>
            ))
          )}
        </div>
      </div>

      <Link className="btn-secondary mt-5" to="/nearby">
        <Navigation size={17} />
        Tìm quanh tôi
      </Link>
    </section>
  );
}

function ToursPage() {
  const { lang } = useAppStore();
  const toursQuery = useQuery({
    queryKey: ['public-tours', lang],
    queryFn: () => tourApi.list(lang),
  });
  const poisQuery = useQuery({
    queryKey: ['tour-pois', lang],
    queryFn: () => poiApi.list({ lang }),
  });

  const poiLookup = Object.fromEntries((poisQuery.data || []).map((poi) => [poi.id, poi])) as Record<string, Poi>;

  return (
    <section className="shell py-12">
      <p className="section-kicker">PUBLIC TOURS</p>
      <h1 className="mt-2 text-4xl font-bold">Lich trinh am thuc cong khai</h1>
      <p className="mt-2 max-w-2xl text-slate-500">
        Backend da co endpoint tours cong khai, nen website-user nay hien da mo luon luong xem tour va cac diem dung.
      </p>

      {toursQuery.isLoading || poisQuery.isLoading ? (
        <Spinner />
      ) : toursQuery.isError ? (
        <ErrorBox text={(toursQuery.error as Error).message} />
      ) : toursQuery.data?.length ? (
        <div className="mt-8 grid gap-6 lg:grid-cols-2">
          {toursQuery.data.map((tour) => (
            <TourCard key={tour.id} tour={tour} poiLookup={poiLookup} />
          ))}
        </div>
      ) : (
        <div className="mt-8 rounded-3xl bg-slate-100 p-12 text-center dark:bg-slate-900">
          <RouteIcon className="mx-auto text-teal" />
          <h2 className="mt-3 text-xl font-bold">Chưa có tour công khai</h2>
          <p className="mt-1 text-slate-500">Admin có thể tạo tour mới trong trang quản trị.</p>
        </div>
      )}
    </section>
  );
}

function TourCard({
  tour,
  poiLookup,
}: {
  tour: TourResponse;
  poiLookup: Record<string, Poi>;
}) {
  const cover = tour.coverImageUrl
    ? normalizeMediaUrl(tour.coverImageUrl)
    : poiLookup[tour.stops[0]?.poiId]
      ? poiImage(poiLookup[tour.stops[0].poiId])
      : heroImage;

  return (
    <article className="overflow-hidden rounded-[2rem] bg-white shadow-soft dark:bg-slate-900">
      <img src={cover} alt={tour.title} className="h-56 w-full object-cover" />
      <div className="p-6">
        <div className="flex flex-wrap items-center gap-2">
          <span className="pill border-coral text-coral">{tour.lang.toUpperCase()}</span>
          <span className="pill">{tour.estimatedDurationMinutes} phut</span>
          <span className="pill">{tour.stops.length} diem dung</span>
        </div>

        <h2 className="mt-4 text-2xl font-bold">{tour.title}</h2>
        <p className="mt-2 leading-7 text-slate-600 dark:text-slate-300">{tour.description}</p>

        <div className="mt-5 space-y-3">
          {tour.stops
            .slice()
            .sort((left, right) => left.order - right.order)
            .map((stop) => {
              const poi = poiLookup[stop.poiId];
              return (
                <div
                  key={`${tour.id}-${stop.poiId}-${stop.order}`}
                  className="rounded-2xl border border-slate-200 p-4 dark:border-slate-800"
                >
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">Stop {stop.order + 1}</p>
                      <h3 className="mt-1 font-bold">{stop.title || poi?.name || stop.poiId}</h3>
                      <p className="mt-1 text-sm text-slate-500">{poi?.address || 'POI se duoc resolve khi click'}</p>
                    </div>
                    <span className="pill">
                      <Clock3 size={14} className="mr-1 inline" />
                      {stop.estimatedStayMinutes} phut
                    </span>
                  </div>

                  <div className="mt-3">
                    <Link className="text-sm font-bold text-coral" to={`/poi/${stop.poiId}`}>
                      Mo chi tiet POI
                    </Link>
                  </div>
                </div>
              );
            })}
        </div>
      </div>
    </article>
  );
}

function QrPage() {
  const { lang } = useAppStore();
  const navigate = useNavigate();
  const ui = getCopy(lang);
  const [searchParams] = useSearchParams();
  const [code, setCode] = useState('');
  const [statusText, setStatusText] = useState<string>(ui.qr.startingCamera);
  const [lastResolved, setLastResolved] = useState<QrActivationResponse | null>(null);
  const [errorText, setErrorText] = useState('');
  const scanLockRef = useRef(false);
  const scannerRef = useRef<{
    stop: () => Promise<void>;
    clear: () => void;
  } | null>(null);
  const resolveRef = useRef<(value: string) => Promise<void>>(async () => undefined);
  const codeFromQuery = searchParams.get('code')?.trim() ?? '';

  const resolveValue = async (rawValue: string) => {
    const value = rawValue.trim();
    if (!value || scanLockRef.current) {
      return;
    }

    scanLockRef.current = true;
    setErrorText('');

    try {
      const qrCodeFromUrl = (() => {
        try {
          const url = new URL(value);
          if (!/^https?:$/i.test(url.protocol)) {
            return null;
          }

          if (/\/qr\/?$/i.test(url.pathname)) {
            return url.searchParams.get('code');
          }

          const hash = url.hash.startsWith('#') ? url.hash.slice(1) : url.hash;
          if (hash) {
            const [hashPath, hashQuery = ''] = hash.split('?');
            if (/^\/qr\/?$/i.test(hashPath)) {
              return new URLSearchParams(hashQuery).get('code');
            }
          }

          return null;
        } catch {
          return null;
        }
      })();

      const poiIdFromCustomScheme = value.startsWith('quan4tourism://poi/')
        ? value.slice('quan4tourism://poi/'.length)
        : null;

      if (poiIdFromCustomScheme) {
        const poiId = poiIdFromCustomScheme;
        await track('qr_scanned', lang, poiId, { fallback: 'poi_id' });
        navigate(`/poi/${poiId}?autoplay=prefer_audio&source=qr`);
        return;
      }

      const normalizedCode = qrCodeFromUrl
        ?? (value.startsWith('quan4tourism://qr/')
          ? value.slice('quan4tourism://qr/'.length)
          : value);

      if (/^[a-f0-9]{24}$/i.test(normalizedCode)) {
        await track('qr_scanned', lang, normalizedCode, { fallback: 'poi_id' });
        navigate(`/poi/${normalizedCode}?autoplay=prefer_audio&source=qr`);
        return;
      }

      const activation = await qrApi.resolve(normalizedCode);
      setLastResolved(activation);
      await track('qr_scanned', lang, activation.poiId, {
        qrCode: activation.code,
        scanMode: activation.scanMode,
        activationTitle: activation.title,
      });

      navigate(`/poi/${activation.poiId}?autoplay=${encodeURIComponent(activation.scanMode)}&source=qr`);
    } catch (error) {
      setErrorText((error as Error).message);
      setStatusText(ui.qr.resolveFailed);
    } finally {
      scanLockRef.current = false;
    }
  };

  resolveRef.current = resolveValue;

  useEffect(() => {
    if (!codeFromQuery) {
      return;
    }

    setCode(codeFromQuery);
    setStatusText(ui.qr.openingFromLink);
    void resolveValue(codeFromQuery);
  }, [codeFromQuery]);

  useEffect(() => {
    let disposed = false;

    async function startScanner() {
      if (codeFromQuery) {
        return;
      }

      if (!navigator.mediaDevices?.getUserMedia) {
        setStatusText(ui.qr.cameraUnsupported);
        return;
      }

      try {
        const { Html5Qrcode } = await import('html5-qrcode');
        if (disposed) {
          return;
        }

        const scanner = new Html5Qrcode('qr-reader');
        scannerRef.current = scanner;
        setStatusText(ui.qr.cameraReady);

        await scanner.start(
          { facingMode: 'environment' },
          { fps: 10, qrbox: { width: 220, height: 220 } },
          (decodedText) => {
            void resolveRef.current(decodedText);
          },
          () => undefined,
        );
      } catch {
        setStatusText(ui.qr.cameraFailed);
      }
    }

    void startScanner();

    return () => {
      disposed = true;
      const scanner = scannerRef.current;
      scannerRef.current = null;
      if (scanner) {
        void scanner
          .stop()
          .catch(() => undefined)
          .finally(() => {
            scanner.clear();
          });
      }
    };
  }, [codeFromQuery, ui.qr.cameraFailed, ui.qr.cameraReady, ui.qr.cameraUnsupported]);

  return (
    <section className="shell py-12">
      <p className="section-kicker">{ui.qr.kicker}</p>
      <h1 className="mt-2 text-4xl font-bold">{ui.qr.title}</h1>
      <p className="mt-3 max-w-2xl text-slate-500">
        {ui.qr.subtitle}
      </p>

      <div className="mt-8 grid gap-6 lg:grid-cols-[1.1fr_.9fr]">
        <div className="rounded-[2rem] bg-white p-6 shadow-soft dark:bg-slate-900">
          <div className="flex items-center gap-3">
            <div className="grid h-12 w-12 place-items-center rounded-2xl bg-coral/10 text-coral">
              <QrCode />
            </div>
            <div>
              <p className="text-sm font-bold uppercase tracking-[0.2em] text-slate-500">{ui.qr.cameraTitle}</p>
              <p className="text-sm text-slate-500">{statusText}</p>
            </div>
          </div>
          <div id="qr-reader" className="mt-5 overflow-hidden rounded-3xl border border-slate-200 dark:border-slate-700" />
        </div>

        <div className="rounded-[2rem] bg-white p-6 shadow-soft dark:bg-slate-900">
          <h2 className="text-2xl font-bold">{ui.qr.manualTitle}</h2>
          <p className="mt-2 text-sm text-slate-500">
            {ui.qr.manualSupport}
          </p>

          <form
            className="mt-5 grid gap-4"
            onSubmit={(event) => {
              event.preventDefault();
              void resolveValue(code);
            }}
          >
            <TextInput
              value={code}
              onChange={(event) => setCode(event.target.value)}
              placeholder={ui.qr.manualPlaceholder}
            />
            <button className="btn-primary justify-center">
              <QrCode size={16} />
              {ui.qr.openFromQr}
            </button>
          </form>

          {errorText ? <div className="mt-4 rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700">{errorText}</div> : null}

          {lastResolved ? (
            <div className="mt-5 rounded-3xl border border-slate-200 p-4 dark:border-slate-700">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">{lastResolved.stopZone}</p>
                  <h3 className="mt-1 text-lg font-bold">{lastResolved.poiName}</h3>
                </div>
                <StatusPill status={lastResolved.isActive ? 'approved' : 'pending'} />
              </div>
              <p className="mt-2 text-sm text-slate-500">{lastResolved.title}</p>
              <p className="mt-1 text-sm text-slate-500">{lastResolved.poiAddress}</p>
            </div>
          ) : null}
        </div>
      </div>
    </section>
  );
}

function LoginPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const setAuth = useAppStore((state) => state.setAuth);
  const [form, setForm] = useState<LoginRequest>({ email: '', password: '' });
  const mutation = useMutation({
    mutationFn: authApi.login,
    onSuccess: (response) => {
      setAuth(response);
      navigate(searchParams.get('next') || '/account', { replace: true });
    },
  });

  return (
    <section className="shell py-12">
      <div className="mx-auto max-w-xl rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
        <p className="section-kicker">AUTH</p>
        <h1 className="mt-2 text-4xl font-bold">Đăng nhập</h1>
        <p className="mt-2 text-slate-500">Su dung API auth/login va auth/me tu backend.</p>

        <form
          className="mt-8 grid gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            mutation.mutate(form);
          }}
        >
          <Field label="Email">
            <TextInput
              type="email"
              value={form.email}
              onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
              required
            />
          </Field>
          <Field label="Mat khau">
            <TextInput
              type="password"
              value={form.password}
              onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
              required
            />
          </Field>

          {mutation.error ? (
            <div className="rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700">
              {(mutation.error as Error).message}
            </div>
          ) : null}

          <button className="btn-primary justify-center" disabled={mutation.isPending}>
            {mutation.isPending ? <LoaderCircle className="animate-spin" size={18} /> : <UserRound size={18} />}
            Đăng nhập
          </button>
        </form>

        <p className="mt-5 text-sm text-slate-500">
          Chưa có tài khoản?{' '}
          <Link to="/register" className="font-bold text-coral">
            Đăng ký ngay
          </Link>
        </p>
      </div>
    </section>
  );
}

function RegisterPage() {
  const navigate = useNavigate();
  const setAuth = useAppStore((state) => state.setAuth);
  const [form, setForm] = useState<RegisterRequest>({
    fullName: '',
    email: '',
    password: '',
    phoneNumber: '',
  });
  const mutation = useMutation({
    mutationFn: authApi.register,
    onSuccess: (response) => {
      setAuth(response);
      navigate('/account', { replace: true });
    },
  });

  return (
    <section className="shell py-12">
      <div className="mx-auto max-w-2xl rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
        <p className="section-kicker">AUTH</p>
        <h1 className="mt-2 text-4xl font-bold">Tạo tài khoản user</h1>
        <p className="mt-2 text-slate-500">Mở FE cho API auth/register và luôn đăng nhập sau khi tạo tài khoản thành công.</p>

        <form
          className="mt-8 grid gap-4 md:grid-cols-2"
          onSubmit={(event) => {
            event.preventDefault();
            mutation.mutate(form);
          }}
        >
          <Field label="Ho va ten">
            <TextInput
              value={form.fullName}
              onChange={(event) => setForm((current) => ({ ...current, fullName: event.target.value }))}
              required
            />
          </Field>
          <Field label="So dien thoai">
            <TextInput
              value={form.phoneNumber}
              onChange={(event) => setForm((current) => ({ ...current, phoneNumber: event.target.value }))}
            />
          </Field>
          <Field label="Email">
            <TextInput
              type="email"
              value={form.email}
              onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
              required
            />
          </Field>
          <Field label="Mat khau">
            <TextInput
              type="password"
              value={form.password}
              onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
              required
              minLength={6}
            />
          </Field>

          {mutation.error ? (
            <div className="rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700 md:col-span-2">
              {(mutation.error as Error).message}
            </div>
          ) : null}

          <button className="btn-primary justify-center md:col-span-2" disabled={mutation.isPending}>
            {mutation.isPending ? <LoaderCircle className="animate-spin" size={18} /> : <ShieldCheck size={18} />}
            Tạo tài khoản và đăng nhập
          </button>
        </form>
      </div>
    </section>
  );
}

function AccountPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const token = useAppStore((state) => state.token);
  const currentUser = useAppStore((state) => state.currentUser);
  const setCurrentUser = useAppStore((state) => state.setCurrentUser);
  const logout = useAppStore((state) => state.logout);
  const [ownerForm, setOwnerForm] = useState({
    businessName: '',
    businessAddress: '',
    phoneNumber: currentUser?.phoneNumber || '',
    description: '',
  });
  const registerOwnerMutation = useMutation({
    mutationFn: authApi.registerOwner,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['auth-me'] });
      await queryClient.invalidateQueries({ queryKey: ['owner-dashboard'] });
      alert('Đã gửi đăng ký owner thành công.');
    },
  });
  const currentUserQuery = useQuery({
    queryKey: ['account-me', token],
    queryFn: authApi.me,
    enabled: Boolean(token),
    staleTime: 0,
    refetchOnMount: 'always',
  });

  useEffect(() => {
    if (currentUserQuery.data) {
      setCurrentUser(currentUserQuery.data);
    }
  }, [currentUserQuery.data, setCurrentUser]);

  const resolvedCurrentUser = currentUserQuery.data ?? currentUser;
  const canOpenOwnerWorkspace = hasRole(resolvedCurrentUser?.roles, 'Owner');
  const ownerApprovedWithoutRole =
    resolvedCurrentUser?.ownerStatus === 'approved' && !canOpenOwnerWorkspace;

  if (!resolvedCurrentUser) {
    return (
      <section className="shell py-12">
        <div className="rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
          <Spinner />
        </div>
      </section>
    );
  }

  return (
    <section className="shell py-12">
      <div className="grid gap-6 lg:grid-cols-[.95fr_1.05fr]">
        <div className="rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
          <p className="section-kicker">TAI KHOAN</p>
          <h1 className="mt-2 text-4xl font-bold">{resolvedCurrentUser.fullName}</h1>
          <p className="mt-2 text-slate-500">{resolvedCurrentUser.email}</p>

          <div className="mt-6 grid gap-3">
            <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800">
              <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">Roles</p>
              <p className="mt-1 font-semibold">{resolvedCurrentUser.roles.join(', ')}</p>
            </div>
            <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800">
              <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">Owner status</p>
              <div className="mt-2">
                <StatusPill status={resolvedCurrentUser.ownerStatus} />
              </div>
            </div>
          </div>

          <div className="mt-6 flex flex-wrap gap-3">
            {canOpenOwnerWorkspace ? (
              <Link className="btn-primary" to="/owner">
                Workspace owner <ArrowRight size={18} />
              </Link>
            ) : null}
            <button
              className="btn-secondary"
              onClick={() => {
                logout();
                navigate('/', { replace: true });
              }}
            >
              <LogOut size={18} />
              Đăng xuất
            </button>
          </div>
        </div>

        <div className="rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
          {canOpenOwnerWorkspace ? (
            <>
              <p className="section-kicker">OWNER DA DUOC DUYET</p>
              <h2 className="mt-2 text-3xl font-bold">Tài khoản này đã là owner</h2>
              <p className="mt-2 text-slate-500">
                Admin đã xác nhận quyền owner cho tài khoản này. Bạn không cần gửi lại form đăng ký.
              </p>
              <div className="mt-6 rounded-2xl bg-emerald-50 p-5 text-emerald-800">
                <p className="font-bold">Trang quản lý owner đã sẵn sàng.</p>
                <p className="mt-1 text-sm">Bạn có thể vào workspace owner để xem địa điểm, lượt xem, lượt nghe audio và lượt quét QR.</p>
              </div>
              <div className="mt-6">
                <Link className="btn-primary" to="/owner">
                  Mo workspace owner <ArrowRight size={18} />
                </Link>
              </div>
            </>
          ) : ownerApprovedWithoutRole ? (
            <>
              <p className="section-kicker">OWNER CHUA SAN SANG</p>
              <h2 className="mt-2 text-3xl font-bold">Tài khoản này chưa có quyền owner để vào workspace</h2>
              <p className="mt-2 text-slate-500">
                Hệ thống đang ghi nhận trạng thái owner đã được duyệt, nhưng tài khoản hiện tại chưa có role `OWNER` để mở trang quản lý owner.
              </p>
              <div className="mt-6 rounded-2xl bg-amber-50 p-5 text-amber-900">
                <p className="font-bold">Nếu đây là tài khoản owner vừa được duyệt:</p>
                <p className="mt-1 text-sm">Hãy đăng xuất đăng nhập lại. Nếu vẫn không vào được, admin cần kiểm tra role `OWNER` trên tài khoản này.</p>
              </div>
            </>
          ) : (
            <>
              <p className="section-kicker">REGISTER OWNER</p>
              <h2 className="mt-2 text-3xl font-bold">Gui yeu cau owner</h2>
              <p className="mt-2 text-slate-500">
                FE này đã nối thẳng vào `POST /api/v1/auth/register-owner`. Khi được approve, tài khoản sẽ có role `OWNER`.
              </p>

              <form
                className="mt-8 grid gap-4"
                onSubmit={(event) => {
                  event.preventDefault();
                  registerOwnerMutation.mutate(ownerForm);
                }}
              >
                <Field label="Ten co so">
                  <TextInput
                    value={ownerForm.businessName}
                    onChange={(event) => setOwnerForm((current) => ({ ...current, businessName: event.target.value }))}
                    required
                  />
                </Field>
                <Field label="Địa chỉ kinh doanh">
                  <TextInput
                    value={ownerForm.businessAddress}
                    onChange={(event) => setOwnerForm((current) => ({ ...current, businessAddress: event.target.value }))}
                    required
                  />
                </Field>
                <Field label="So dien thoai">
                  <TextInput
                    value={ownerForm.phoneNumber}
                    onChange={(event) => setOwnerForm((current) => ({ ...current, phoneNumber: event.target.value }))}
                    required
                  />
                </Field>
                <Field label="Mo ta">
                  <TextArea
                    value={ownerForm.description}
                    onChange={(event) => setOwnerForm((current) => ({ ...current, description: event.target.value }))}
                    placeholder="Mo ta ngan ve co so cua ban"
                  />
                </Field>

                {registerOwnerMutation.error ? (
                  <div className="rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700">
                    {(registerOwnerMutation.error as Error).message}
                  </div>
                ) : null}

                <button className="btn-primary justify-center" disabled={registerOwnerMutation.isPending}>
                  {registerOwnerMutation.isPending ? (
                    <LoaderCircle className="animate-spin" size={18} />
                  ) : (
                    <ShieldCheck size={18} />
                  )}
                  Gui yeu cau owner
                </button>
              </form>
            </>
          )}
        </div>
      </div>
    </section>
  );
}

function OwnerPage() {
  const queryClient = useQueryClient();
  const { lang } = useAppStore();
  const submissionFormRef = useRef<HTMLDivElement>(null);
  const [editingSubmissionId, setEditingSubmissionId] = useState<string | null>(null);
  const createEmptySubmissionForm = (): CreateOwnerSubmissionRequest => ({
    submissionType: 'create',
    poiId: '',
    poiName: '',
    description: '',
    categoryId: '',
    location: {
      latitude: 10.7578,
      longitude: 106.706,
    },
    address: '',
    ward: '',
    district: 'Quan 4',
    city: 'TP.HCM',
    priceRange: '$$',
    priority: 0,
    mapUrl: '',
    ttsScript: '',
    geofenceRadiusMeters: 100,
    autoNarrationEnabled: true,
    images: [],
    openingHours: [],
    contactInfo: null,
    tags: [],
  });
  const [submissionForm, setSubmissionForm] = useState<CreateOwnerSubmissionRequest>(createEmptySubmissionForm);
  const dashboardQuery = useQuery({
    queryKey: ['owner-dashboard'],
    queryFn: ownerApi.dashboard,
  });
  const ownerPoisQuery = useQuery({
    queryKey: ['owner-pois', lang],
    queryFn: () => ownerApi.pois(lang),
  });
  const submissionsQuery = useQuery({
    queryKey: ['owner-submissions'],
    queryFn: ownerApi.submissions,
  });
  const categoriesQuery = useQuery({
    queryKey: ['categories'],
    queryFn: categoryApi.list,
  });
  const saveSubmissionMutation = useMutation({
    mutationFn: (payload: CreateOwnerSubmissionRequest) =>
      editingSubmissionId
        ? ownerApi.updateSubmission(editingSubmissionId, payload)
        : ownerApi.createSubmission(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['owner-dashboard'] });
      await queryClient.invalidateQueries({ queryKey: ['owner-pois'] });
      await queryClient.invalidateQueries({ queryKey: ['owner-submissions'] });
      setEditingSubmissionId(null);
      setSubmissionForm(createEmptySubmissionForm());
    },
  });

  const ownerPois = ownerPoisQuery.data || [];
  const submissions = submissionsQuery.data || [];

  const beginUpdateSubmissionFromPoi = (poi: OwnerManagedPoi) => {
    setEditingSubmissionId(null);
    setSubmissionForm({
      submissionType: 'update',
      poiId: poi.id,
      poiName: poi.name,
      description: poi.description,
      categoryId: poi.categoryId,
      location: {
        latitude: poi.latitude,
        longitude: poi.longitude,
      },
      address: poi.address,
      ward: poi.ward,
      district: poi.district,
      city: poi.city,
      priceRange:
        poi.priceRange === '$' || poi.priceRange === '$$' || poi.priceRange === '$$$'
          ? poi.priceRange
          : '$$',
      priority: poi.priority,
      mapUrl: poi.mapUrl || '',
      ttsScript: poi.ttsScript || '',
      geofenceRadiusMeters: poi.geofenceRadiusMeters,
      autoNarrationEnabled: poi.autoNarrationEnabled,
      images: poi.images || [],
      openingHours: poi.openingHours || [],
      contactInfo: poi.contactInfo || null,
      tags: poi.tags || [],
    });
    submissionFormRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  return (
    <section className="shell py-12">
      <p className="section-kicker">OWNER WORKSPACE</p>
      <h1 className="mt-2 text-4xl font-bold">Quản lý địa điểm và submissions</h1>
      <p className="mt-2 max-w-3xl text-slate-500">
        Trang owner này đã có dashboard tổng quan, danh sách địa điểm của chính bạn, lượt vào trang, số người đã nghe audio, lượt quét QR và khu gửi yêu cầu cập nhật nội dung.
      </p>

      <div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {dashboardQuery.isLoading ? (
          <Spinner />
        ) : dashboardQuery.isError ? (
          <ErrorBox text={(dashboardQuery.error as Error).message} />
        ) : (
          [
            ['Tong POI', dashboardQuery.data?.totalPois || 0],
            ['Tong submissions', dashboardQuery.data?.totalSubmissions || 0],
            ['Lượt vào trang', dashboardQuery.data?.totalViews || 0],
            ['Người vào trang', dashboardQuery.data?.uniqueVisitors || 0],
            ['Lượt nghe audio', dashboardQuery.data?.totalAudioPlays || 0],
            ['Người đã nghe', dashboardQuery.data?.uniqueAudioListeners || 0],
            ['Lượt quét QR', dashboardQuery.data?.totalQrScans || 0],
          ].map(([label, value]) => (
            <div
              key={label as string}
              className="rounded-3xl bg-white p-6 shadow-soft dark:bg-slate-900"
            >
              <p className="text-sm text-slate-500">{label as string}</p>
              <p className="mt-2 text-3xl font-bold">{value as number}</p>
            </div>
          ))
        )}
      </div>

      <div className="mt-8 rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h2 className="text-2xl font-bold">Địa điểm của tôi</h2>
            <p className="mt-2 text-sm text-slate-500">
              Dữ liệu lấy từ `GET /api/v1/owner/pois`, gồm thông tin POI, lượt vào trang, người nghe audio và số lần quét QR theo từng địa điểm.
            </p>
          </div>
          <button
            className="btn-secondary"
            onClick={() => {
              setEditingSubmissionId(null);
              setSubmissionForm(createEmptySubmissionForm());
              submissionFormRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }}
          >
            Tạo submission mới
          </button>
        </div>

        {ownerPoisQuery.isLoading ? (
          <Spinner />
        ) : ownerPoisQuery.isError ? (
          <div className="mt-6">
            <ErrorBox text={(ownerPoisQuery.error as Error).message} />
          </div>
        ) : ownerPois.length ? (
          <div className="mt-6 grid gap-5 lg:grid-cols-2">
            {ownerPois.map((poi) => (
              <OwnerPoiCard key={poi.id} poi={poi} onCreateUpdate={() => beginUpdateSubmissionFromPoi(poi)} />
            ))}
          </div>
        ) : (
          <div className="mt-6 rounded-3xl bg-slate-100 p-8 text-center dark:bg-slate-800">
            <p className="font-bold">Chưa có địa điểm nào được gán cho owner này</p>
            <p className="mt-1 text-sm text-slate-500">
              Bạn vẫn có thể tạo submission mới ở phần bên dưới để đề xuất địa điểm mới hoặc yêu cầu cập nhật.
            </p>
          </div>
        )}
      </div>

      <div className="mt-8 grid gap-6 lg:grid-cols-[1.05fr_.95fr]">
        <div ref={submissionFormRef} className="rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
          <h2 className="text-2xl font-bold">Tạo submission mới</h2>
          <p className="mt-2 text-sm text-slate-500">
            Bạn có thể tạo submission mới, sửa submission pending, hoặc từ một địa điểm đang quản lý tạo nhanh submission `update` đã prefill sẵn dữ liệu hiện tại.
          </p>

          {editingSubmissionId ? (
            <div className="mt-4 flex flex-wrap items-center gap-3 rounded-2xl bg-amber-50 px-4 py-3 text-sm text-amber-800">
              <span>Đang sửa submission {editingSubmissionId}</span>
              <button
                type="button"
                className="font-bold text-coral"
                onClick={() => {
                  setEditingSubmissionId(null);
                  setSubmissionForm(createEmptySubmissionForm());
                }}
              >
                Hủy chế độ sửa
              </button>
            </div>
          ) : null}

          <form
            className="mt-6 grid gap-4 md:grid-cols-2"
            onSubmit={(event) => {
              event.preventDefault();
              saveSubmissionMutation.mutate({
                ...submissionForm,
                poiId: submissionForm.poiId || undefined,
              });
            }}
          >
            <Field label="Loại submission">
              <select
                value={submissionForm.submissionType}
                onChange={(event) =>
                  setSubmissionForm((current) => ({ ...current, submissionType: event.target.value }))
                }
                className="rounded-2xl border border-slate-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900"
              >
                <option value="create">create</option>
                <option value="update">update</option>
              </select>
            </Field>
            <Field label="POI id (nếu update)">
              <TextInput
                value={submissionForm.poiId}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, poiId: event.target.value }))}
              />
            </Field>
            <Field label="Tên địa điểm">
              <TextInput
                value={submissionForm.poiName}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, poiName: event.target.value }))}
                required
              />
            </Field>
            <Field label="Category">
              <select
                value={submissionForm.categoryId}
                onChange={(event) =>
                  setSubmissionForm((current) => ({ ...current, categoryId: event.target.value }))
                }
                required
                className="rounded-2xl border border-slate-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900"
              >
                <option value="">Chọn category</option>
                {(categoriesQuery.data || []).map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Latitude">
              <TextInput
                type="number"
                step="0.000001"
                value={submissionForm.location.latitude}
                onChange={(event) =>
                  setSubmissionForm((current) => ({
                    ...current,
                    location: {
                      ...current.location,
                      latitude: Number(event.target.value),
                    },
                  }))
                }
                required
              />
            </Field>
            <Field label="Longitude">
              <TextInput
                type="number"
                step="0.000001"
                value={submissionForm.location.longitude}
                onChange={(event) =>
                  setSubmissionForm((current) => ({
                    ...current,
                    location: {
                      ...current.location,
                      longitude: Number(event.target.value),
                    },
                  }))
                }
                required
              />
            </Field>
            <Field label="Địa chỉ">
              <TextInput
                value={submissionForm.address}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, address: event.target.value }))}
                required
              />
            </Field>
            <Field label="Ward">
              <TextInput
                value={submissionForm.ward}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, ward: event.target.value }))}
                required
              />
            </Field>
            <Field label="District">
              <TextInput
                value={submissionForm.district}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, district: event.target.value }))}
                required
              />
            </Field>
            <Field label="City">
              <TextInput
                value={submissionForm.city}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, city: event.target.value }))}
                required
              />
            </Field>
            <Field label="Price range">
              <select
                value={submissionForm.priceRange}
                onChange={(event) =>
                  setSubmissionForm((current) => ({
                    ...current,
                    priceRange: event.target.value as '$' | '$$' | '$$$',
                  }))
                }
                className="rounded-2xl border border-slate-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900"
              >
                <option value="$">$</option>
                <option value="$$">$$</option>
                <option value="$$$">$$$</option>
              </select>
            </Field>
            <Field label="Priority">
              <TextInput
                type="number"
                min={0}
                value={submissionForm.priority}
                onChange={(event) =>
                  setSubmissionForm((current) => ({ ...current, priority: Number(event.target.value) }))
                }
              />
            </Field>
            <Field label="Geofence radius">
              <TextInput
                type="number"
                min={1}
                value={submissionForm.geofenceRadiusMeters}
                onChange={(event) =>
                  setSubmissionForm((current) => ({
                    ...current,
                    geofenceRadiusMeters: Number(event.target.value),
                  }))
                }
              />
            </Field>
            <Field label="Map URL">
              <TextInput
                value={submissionForm.mapUrl}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, mapUrl: event.target.value }))}
              />
            </Field>
            <Field label="Tags (CSV)">
              <TextInput
                value={submissionForm.tags.join(', ')}
                onChange={(event) =>
                  setSubmissionForm((current) => ({
                    ...current,
                    tags: event.target.value
                      .split(',')
                      .map((item) => item.trim())
                      .filter(Boolean),
                  }))
                }
              />
            </Field>
            <Field label="Mo ta">
              <TextArea
                value={submissionForm.description}
                onChange={(event) =>
                  setSubmissionForm((current) => ({ ...current, description: event.target.value }))
                }
                required
                className="md:col-span-2"
              />
            </Field>
            <Field label="Nội dung thuyết minh (để trống sẽ đọc mô tả)">
              <TextArea
                value={submissionForm.ttsScript}
                onChange={(event) =>
                  setSubmissionForm((current) => ({ ...current, ttsScript: event.target.value }))
                }
                placeholder="Nhập kịch bản riêng nếu muốn đọc khác phần mô tả"
                className="md:col-span-2"
              />
            </Field>
            <label className="flex items-center gap-3 rounded-2xl border border-slate-200 px-4 py-3 md:col-span-2 dark:border-slate-700">
              <input
                type="checkbox"
                checked={submissionForm.autoNarrationEnabled}
                onChange={(event) =>
                  setSubmissionForm((current) => ({
                    ...current,
                    autoNarrationEnabled: event.target.checked,
                  }))
                }
              />
              <span>Bật auto narration</span>
            </label>

            {saveSubmissionMutation.error ? (
              <div className="rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700 md:col-span-2">
                {(saveSubmissionMutation.error as Error).message}
              </div>
            ) : null}

            <button className="btn-primary justify-center md:col-span-2" disabled={saveSubmissionMutation.isPending}>
              {saveSubmissionMutation.isPending ? (
                <LoaderCircle className="animate-spin" size={18} />
              ) : (
                <CheckCircle2 size={18} />
              )}
              {editingSubmissionId ? 'Cập nhật submission owner' : 'Tạo submission owner'}
            </button>
          </form>
        </div>

        <div className="rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
          <h2 className="text-2xl font-bold">Danh sách submissions</h2>
          <p className="mt-2 text-sm text-slate-500">Dữ liệu lấy từ `GET /api/v1/owner/submissions`.</p>

          {submissionsQuery.isLoading ? (
            <Spinner />
          ) : submissionsQuery.isError ? (
            <ErrorBox text={(submissionsQuery.error as Error).message} />
          ) : submissions.length ? (
            <div className="mt-6 space-y-4">
              {submissions.map((submission) => (
                <SubmissionCard
                  key={submission.id}
                  submission={submission}
                  onEdit={() => {
                    setEditingSubmissionId(submission.id);
                    setSubmissionForm((current) => ({
                      ...current,
                      submissionType: submission.submissionType,
                      poiId: submission.poiId || '',
                      poiName: submission.poiName,
                      priority: submission.priority,
                      mapUrl: submission.mapUrl || '',
                      ttsScript: submission.ttsScript || '',
                      geofenceRadiusMeters: submission.geofenceRadiusMeters,
                      autoNarrationEnabled: submission.autoNarrationEnabled,
                    }));
                    submissionFormRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                  }}
                />
              ))}
            </div>
          ) : (
            <div className="mt-6 rounded-3xl bg-slate-100 p-8 text-center dark:bg-slate-800">
              <p className="font-bold">Chưa có submission nào</p>
              <p className="mt-1 text-sm text-slate-500">Gửi submission đầu tiên của bạn bằng form bên trái.</p>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}

function OwnerPoiCard({
  poi,
  onCreateUpdate,
}: {
  poi: OwnerManagedPoi;
  onCreateUpdate: () => void;
}) {
  const mapUrl = `https://www.google.com/maps/dir/?api=1&destination=${poi.latitude},${poi.longitude}`;

  return (
    <article className="overflow-hidden rounded-[2rem] border border-slate-200 bg-white shadow-soft dark:border-slate-800 dark:bg-slate-950">
      <img src={poiImage(poi)} alt={poi.name} className="h-52 w-full object-cover" />

      <div className="p-6">
        <div className="flex flex-wrap items-center gap-2">
          <span className={`pill ${poi.isActive ? 'border-emerald-400 text-emerald-600' : 'border-slate-300 text-slate-500'}`}>
            {poi.isActive ? 'Đang hiển thị' : 'Tạm ẩn'}
          </span>
          <span className="pill">Audio: {poi.audioStatus}</span>
          {poi.activationRequested ? <span className="pill border-amber-300 text-amber-700">Chờ duyệt kích hoạt</span> : null}
        </div>

        <h3 className="mt-4 text-2xl font-bold">{poi.name}</h3>
        <p className="mt-2 line-clamp-2 text-sm leading-6 text-slate-500">{poi.description}</p>

        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800">
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">Lượt vào trang</p>
            <p className="mt-1 text-2xl font-bold">{poi.viewCount}</p>
          </div>
          <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800">
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-amber-600">Người vào trang</p>
            <p className="mt-1 text-2xl font-bold">{poi.uniqueVisitorCount}</p>
          </div>
          <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800">
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-teal">Lượt nghe</p>
            <p className="mt-1 text-2xl font-bold">{poi.audioPlayCount}</p>
          </div>
          <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800">
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-sky-600">Người đã nghe</p>
            <p className="mt-1 text-2xl font-bold">{poi.uniqueAudioListenerCount}</p>
          </div>
          <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800 md:col-span-2">
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-fuchsia-600">Lượt quét QR</p>
            <p className="mt-1 text-2xl font-bold">{poi.qrScanCount}</p>
          </div>
        </div>

        <div className="mt-4 space-y-2 text-sm text-slate-500">
          <p className="flex items-start gap-2">
            <MapPin size={16} className="mt-0.5 shrink-0 text-coral" />
            <span>{poi.address}, {poi.ward}, {poi.district}</span>
          </p>
          <p>Gia: {poi.priceRange} Â· Priority: {poi.priority}</p>
          <p>Cập nhật: {new Date(poi.updatedAt).toLocaleString()}</p>
        </div>

        <div className="mt-5 flex flex-wrap gap-3">
          <Link className="btn-secondary !px-4 !py-2" to={`/poi/${poi.id}`}>
            Xem chi tiết
          </Link>
          <a className="btn-secondary !px-4 !py-2" href={mapUrl} target="_blank" rel="noreferrer">
            <Navigation size={16} />
            Chi duong
          </a>
          <button className="btn-primary !px-4 !py-2" onClick={onCreateUpdate}>
            Gửi yêu cầu cập nhật <ArrowRight size={16} />
          </button>
        </div>
      </div>
    </article>
  );
}

function SubmissionCard({
  submission,
  onEdit,
}: {
  submission: OwnerSubmissionResponse;
  onEdit: () => void;
}) {
  return (
    <article className="rounded-3xl border border-slate-200 p-5 dark:border-slate-800">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">{submission.submissionType}</p>
          <h3 className="mt-1 text-lg font-bold">{submission.poiName}</h3>
        </div>
        <StatusPill status={submission.status} />
      </div>

      <div className="mt-4 grid gap-2 text-sm text-slate-500">
        <p>Priority: {submission.priority}</p>
        <p>Geofence: {submission.geofenceRadiusMeters} m</p>
        <p>Auto narration: {submission.autoNarrationEnabled ? 'bật' : 'tắt'}</p>
        <p>Tạo lúc: {new Date(submission.createdAt).toLocaleString()}</p>
        {submission.adminNote ? <p>Admin note: {submission.adminNote}</p> : null}
      </div>

      {submission.status === 'pending' ? (
        <button onClick={onEdit} className="mt-4 text-sm font-bold text-coral">
          Sửa submission pending
        </button>
      ) : null}
    </article>
  );
}

function About() {
  return (
    <section className="shell py-12">
      <p className="section-kicker">CÂU CHUYỆN DỰ ÁN</p>
      <h1 className="mt-2 max-w-3xl text-4xl font-bold leading-tight">
        Một người bạn đồng hành cho hành trình ăn ngon ở Quận 4.
      </h1>

      <div className="mt-10 grid gap-8 lg:grid-cols-2">
        <div className="rounded-[2rem] bg-orange-100 p-8 dark:bg-orange-500/10">
          <h2 className="text-2xl font-bold">Mục tiêu</h2>
          <p className="mt-4 leading-7 text-slate-600 dark:text-slate-300">
            Hệ thống giúp du khách tìm, nghe và cảm nhận những điểm ẩm thực địa phương một cách trực quan qua web, mobile và bản đồ thông minh.
          </p>
        </div>

        <div className="rounded-[2rem] bg-teal/10 p-8">
          <h2 className="text-2xl font-bold">Có gì trong trải nghiệm?</h2>
          <ul className="mt-4 grid gap-3 text-slate-600 dark:text-slate-300">
            {[
              'POI ẩm thực được tuyển chọn',
              'GPS và gợi ý gần bạn',
              'Audio thuyết minh đa ngôn ngữ',
              'QR user flow va deep-link resolve',
              'Tours công khai và owner workspace',
            ].map((item) => (
              <li key={item} className="flex gap-2">
                <span className="text-teal">âœ¦</span>
                {item}
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

function NotFound() {
  return (
    <section className="shell grid min-h-[55vh] place-items-center text-center">
      <div>
        <p className="text-7xl font-bold text-coral">404</p>
        <h1 className="mt-3 text-2xl font-bold">Không tìm thấy trang này</h1>
        <Link className="btn-primary mt-6" to="/">
          Ve trang chu
        </Link>
      </div>
    </section>
  );
}

export default function App() {
  return (
    <>
      <SessionBootstrap />
      <PresenceHeartbeat />
      <AnimatePresence mode="wait">
        <Routes>
          <Route element={<PublicLayout />}>
            <Route path="/" element={<Home />} />
            <Route path="/explore" element={<Explore />} />
            <Route path="/poi/:id" element={<Detail />} />
            <Route
              path="/nearby"
              element={
                <RouteErrorBoundary fallbackText="Trang Gần tôi vừa gặp lỗi hiển thị. Hãy tải lại trang hoặc thử lấy vị trí lại.">
                  <Nearby />
                </RouteErrorBoundary>
              }
            />
            <Route path="/map" element={<MapPage />} />
            <Route path="/tours" element={<ToursPage />} />
            <Route path="/qr" element={<QrPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route
              path="/account"
              element={
                <RequireAuth>
                  <AccountPage />
                </RequireAuth>
              }
            />
            <Route
              path="/owner"
              element={
                <RequireAuth>
                  <RequireOwner>
                    <OwnerPage />
                  </RequireOwner>
                </RequireAuth>
              }
            />
            <Route path="/about" element={<About />} />
            <Route path="*" element={<NotFound />} />
          </Route>
        </Routes>
      </AnimatePresence>
    </>
  );
}






