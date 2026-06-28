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
import { useEffect, useRef, useState } from 'react';
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
import { PublicLayout } from './components/layout/PublicLayout';
import { PoiCard } from './components/common/PoiCard';
import { AudioPlayer } from './components/common/AudioPlayer';
import { PoiMap } from './components/map/PoiMap';
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
import { distance, track } from './utils/analytics';
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
  return (
    <div className="rounded-3xl border border-orange-200 bg-orange-50 p-8 text-center text-orange-800">
      {text || 'Chua the tai du lieu. Hay kiem tra ket noi API.'}
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
        Tat ca
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
            alt="Mon an Quan 4"
          />
          <div className="absolute inset-0 -z-10 bg-gradient-to-r from-slate-950 via-slate-950/75 to-transparent" />

          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="max-w-2xl">
            <span className="section-kicker text-orange-300">QUAN 4 · SAIGON FOOD STORIES</span>
            <h1 className="mt-4 font-serif text-4xl font-bold leading-tight sm:text-6xl">
              Kham pha am thuc <span className="text-orange-400">Quan 4</span>
            </h1>
            <p className="mt-5 max-w-xl text-base leading-7 text-slate-200 sm:text-lg">
              Ban do am thuc thong minh voi audio thuyet minh, da ngon ngu va goi y dia diem gan ban.
            </p>

            <div className="mt-8 flex flex-wrap gap-3">
              <Link className="btn-primary" to="/explore">
                Kham pha ngay <ArrowRight size={18} />
              </Link>
              <Link className="btn-secondary !border-white/20 !bg-white/10 !text-white" to="/qr">
                <QrCode size={18} />
                Quet QR user
              </Link>
              <Link className="btn-secondary !border-white/20 !bg-white/10 !text-white" to="/tours">
                <RouteIcon size={18} />
                Xem tours
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
              placeholder="Ban dang them mon gi?"
              className="min-w-0 flex-1 bg-transparent py-2 text-slate-900 outline-none"
            />
            <button className="rounded-xl bg-ink px-4 py-2 font-semibold">Tim</button>
          </form>
        </div>
      </section>

      <section className="shell py-14">
        <p className="section-kicker">CHON THEO GU CUA BAN</p>
        <h2 className="mt-2 text-3xl font-bold">Hom nay an gi?</h2>
        <div className="mt-6">
          <Categories onSelect={(id) => navigate(`/explore${id ? `?category=${id}` : ''}`)} />
        </div>
      </section>

      <section className="shell">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="section-kicker">GOI Y CHO BAN</p>
            <h2 className="mt-2 text-3xl font-bold">Diem den noi bat</h2>
          </div>
          <div className="flex gap-3">
            <Link to="/explore" className="hidden font-bold text-coral sm:block">
              Xem tat ca
            </Link>
            <Link to="/tours" className="hidden font-bold text-teal sm:block">
              Tours cong khai
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
            [Compass, 'Ban do thong minh', 'Tim diem an dung gu ngay tren ban do.'],
            [Volume2, 'Audio thuyet minh', 'Moi dia diem deu co mot cau chuyen de nghe.'],
            [Sparkles, 'Da ngon ngu', 'Chao don du khach bang nhieu ngon ngu.'],
            [QrCode, 'QR activation', 'Mo nhanh POI va audio ngay tu ma QR.'],
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
            <p className="text-sm font-bold uppercase tracking-widest">MO DAY DU CONTRACT PUBLIC TU BACKEND</p>
            <h2 className="mt-1 text-3xl font-bold">Tours, QR, auth, owner va POI tren web user</h2>
          </div>
          <Link className="btn-secondary !border-ink !bg-ink !text-white" to="/map">
            Xem ban do <MapPin size={18} />
          </Link>
          <Link className="btn-secondary !border-ink !bg-white/20 !text-ink" to="/qr">
            Quet QR <QrCode size={18} />
          </Link>
          <Link className="btn-secondary !border-ink !bg-white/20 !text-ink" to="/account">
            Tai khoan <UserRound size={18} />
          </Link>
        </div>
      </section>
    </>
  );
}

function Explore() {
  const { lang } = useAppStore();
  const navigate = useNavigate();
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
      <p className="section-kicker">KHAM PHA HUONG VI</p>
      <h1 className="mt-2 text-4xl font-bold">An gi o Quan 4?</h1>
      <p className="mt-2 text-slate-500">Loc theo cam hung, muc gia va khu vuc ban muon ghe.</p>

      <form
        onSubmit={runSearch}
        className="mt-7 flex rounded-2xl border border-slate-200 bg-white p-2 dark:border-slate-800 dark:bg-slate-900"
      >
        <Search className="m-2 text-slate-400" />
        <input
          value={keyword}
          onChange={(event) => setKeyword(event.target.value)}
          className="min-w-0 flex-1 bg-transparent outline-none"
          placeholder="Tim mon, ten quan, con duong..."
        />
        <button className="btn-primary !py-2">Tim</button>
      </form>

      <div className="mt-6">
        <Categories selected={categoryId} onSelect={(id) => setCategoryId(id || '')} />
      </div>

      <div className="mt-4 flex gap-2">
        <span className="py-1.5 text-sm text-slate-500">Muc gia:</span>
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
          <h2 className="mt-3 text-xl font-bold">Chua tim thay dia diem phu hop</h2>
          <p className="mt-1 text-slate-500">Thu doi tu khoa hoac bo loc cua ban.</p>
        </div>
      )}
    </section>
  );
}

function Detail() {
  const { id = '' } = useParams();
  const location = useLocation();
  const { lang } = useAppStore();
  const narrationLang: Lang = 'vi';
  const queryParams = new URLSearchParams(location.search);
  const autoplay = queryParams.get('autoplay');
  const source = queryParams.get('source');
  const { data: poi, isLoading, isError } = useQuery({
    queryKey: ['detail', id, lang],
    queryFn: () => poiApi.detail(id, lang),
  });
  const { data: narrationPoi } = useQuery({
    queryKey: ['detail-narration', id, narrationLang],
    queryFn: () => poiApi.detail(id, narrationLang),
    enabled: Boolean(id) && lang !== narrationLang,
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
  const narrationText =
    narrationLang === lang
      ? (poi.ttsScript || poi.description)
      : (narrationPoi?.ttsScript || narrationPoi?.description || '');

  return (
    <section className="shell py-8">
      <Link to="/explore" className="text-sm font-bold text-coral">
        ← Quay lai kham pha
      </Link>

      {source === 'qr' ? (
        <div className="mt-5 rounded-2xl border border-teal/20 bg-teal/10 px-4 py-3 text-sm text-teal-900 dark:text-teal-100">
          POI nay duoc mo tu luong QR user.
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
          <p className="section-kicker">AM THUC QUAN 4</p>
          <h1 className="mt-2 text-4xl font-bold leading-tight">{poi.name}</h1>

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
              errorText={audioQuery.isError ? 'Khong tai duoc audio thuyet minh tu backend.' : undefined}
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
            <a className="btn-primary" target="_blank" rel="noreferrer" href={mapUrl}>
              <Navigation size={17} />
              Chi duong
            </a>
            <Link className="btn-secondary" to={`/map?poi=${poi.id}`}>
              Xem tren ban do
            </Link>
          </div>
        </div>
      </div>

      <div className="mt-9 grid gap-6 md:grid-cols-2">
        <div className="rounded-3xl bg-white p-6 shadow-soft dark:bg-slate-900">
          <h2 className="font-bold">Gio hoat dong</h2>
          <div className="mt-3 space-y-2 text-sm">
            {poi.openingHours?.length ? (
              poi.openingHours.map((item) => (
                <p key={item.dayOfWeek} className="flex justify-between gap-6">
                  <span>{item.dayOfWeek}</span>
                  <b>{item.isClosed ? 'Dong cua' : `${item.openTime} - ${item.closeTime}`}</b>
                </p>
              ))
            ) : (
              <p className="text-slate-500">Chua cap nhat.</p>
            )}
          </div>
        </div>

        <div className="rounded-3xl bg-white p-6 shadow-soft dark:bg-slate-900">
          <h2 className="font-bold">Thong tin va the</h2>
          <div className="mt-3 flex flex-wrap gap-2">
            {poi.tags?.map((tag) => (
              <span key={tag} className="pill bg-slate-50 dark:bg-slate-800">
                #{tag}
              </span>
            ))}
          </div>
          {poi.contactInfo?.phone ? (
            <p className="mt-4 text-sm text-slate-500">Lien he: {poi.contactInfo.phone}</p>
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

  const query = useQuery({
    queryKey: ['nearby', coords, radius, lang],
    queryFn: () => poiApi.nearby({ lat: coords!.lat, lng: coords!.lng, radius, limit: 20, lang }),
    enabled: Boolean(coords),
  });

  const locate = (fallback = false) => {
    const setCurrentLocation = (lat: number, lng: number) => {
      setCoords({ lat, lng });
      setLocation({ lat, lng });
      track('nearby_requested', lang, undefined, { radius });
    };

    if (fallback) {
      setCurrentLocation(10.7578, 106.706);
      return;
    }

    navigator.geolocation?.getCurrentPosition(
      (position) => setCurrentLocation(position.coords.latitude, position.coords.longitude),
      () => alert('Ban da tu choi GPS. Hay dung vi tri mac dinh Quan 4.'),
      { enableHighAccuracy: true, timeout: 10000 },
    );
  };

  return (
    <section className="shell py-12">
      <p className="section-kicker">GOI Y QUANH BAN</p>
      <h1 className="mt-2 text-4xl font-bold">Tim quan gan toi</h1>
      <p className="mt-3 max-w-xl text-slate-500">
        Cho phep trinh duyet su dung vi tri de tim nhung huong vi dang thu gan ban nhat.
      </p>

      <div className="mt-7 rounded-3xl bg-ink p-7 text-white">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="font-bold">Ban kinh tim kiem</p>
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
              Lay vi tri hien tai
            </button>
            <button
              onClick={() => locate(true)}
              className="btn-secondary !border-white/20 !bg-transparent !text-white"
            >
              Dung vi tri Quan 4
            </button>
          </div>
        </div>
      </div>

      {query.isLoading ? (
        <Spinner />
      ) : query.isError ? (
        <ErrorBox text="Khong tim duoc dia diem gan day. Thu lai sau." />
      ) : query.data ? (
        <div className="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {query.data.map((poi) => (
            <div key={poi.id} className="relative">
              <PoiCard poi={poi} />
              <span className="absolute right-5 top-4 rounded-full bg-ink px-2 py-1 text-xs font-bold text-white">
                {distance(poi.distanceMeters)}
              </span>
            </div>
          ))}
        </div>
      ) : null}
    </section>
  );
}

function MapPage() {
  const { lang } = useAppStore();
  const { data: pois = [], isLoading } = useQuery({
    queryKey: ['map-pois', lang],
    queryFn: () => poiApi.list({ lang }),
  });

  return (
    <section className="shell py-12">
      <p className="section-kicker">DINH VI HUONG VI</p>
      <h1 className="mt-2 text-4xl font-bold">Ban do am thuc</h1>
      <p className="mt-2 text-slate-500">Chon marker hoac dia diem de xem chi tiet va chi duong.</p>

      <div className="mt-7 grid gap-5 lg:grid-cols-[1.3fr_.7fr]">
        <PoiMap pois={pois} />
        <div className="max-h-[430px] space-y-3 overflow-y-auto pr-1">
          {isLoading ? (
            <Spinner />
          ) : (
            pois.map((poi) => (
              <Link
                key={poi.id}
                to={`/poi/${poi.id}`}
                className="block rounded-2xl bg-white p-4 shadow-sm transition hover:shadow-soft dark:bg-slate-900"
              >
                <p className="font-bold">{poi.name}</p>
                <p className="mt-1 flex items-center gap-1 text-sm text-slate-500">
                  <MapPin size={14} />
                  {poi.address}
                </p>
              </Link>
            ))
          )}
        </div>
      </div>

      <Link className="btn-secondary mt-5" to="/nearby">
        <Navigation size={17} />
        Tim quanh toi
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
          <h2 className="mt-3 text-xl font-bold">Chua co tour cong khai</h2>
          <p className="mt-1 text-slate-500">Admin co the tao tour moi trong trang quan tri.</p>
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
  const [code, setCode] = useState('');
  const [statusText, setStatusText] = useState('Dang khoi dong camera QR...');
  const [lastResolved, setLastResolved] = useState<QrActivationResponse | null>(null);
  const [errorText, setErrorText] = useState('');
  const scanLockRef = useRef(false);
  const scannerRef = useRef<{
    stop: () => Promise<void>;
    clear: () => void;
  } | null>(null);
  const resolveRef = useRef<(value: string) => Promise<void>>(async () => undefined);

  const resolveValue = async (rawValue: string) => {
    const value = rawValue.trim();
    if (!value || scanLockRef.current) {
      return;
    }

    scanLockRef.current = true;
    setErrorText('');

    try {
      if (value.startsWith('quan4tourism://poi/')) {
        const poiId = value.slice('quan4tourism://poi/'.length);
        await track('qr_scanned', lang, poiId, { fallback: 'poi_id' });
        navigate(`/poi/${poiId}?autoplay=prefer_audio&source=qr`);
        return;
      }

      const normalizedCode = value.startsWith('quan4tourism://qr/')
        ? value.slice('quan4tourism://qr/'.length)
        : value;

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
      setStatusText('Khong resolve duoc QR nay. Ban co the thu nhap ma thu cong.');
    } finally {
      scanLockRef.current = false;
    }
  };

  resolveRef.current = resolveValue;

  useEffect(() => {
    let disposed = false;

    async function startScanner() {
      if (!navigator.mediaDevices?.getUserMedia) {
        setStatusText('Trinh duyet nay khong ho tro camera. Ban van co the nhap ma QR thu cong.');
        return;
      }

      try {
        const { Html5Qrcode } = await import('html5-qrcode');
        if (disposed) {
          return;
        }

        const scanner = new Html5Qrcode('qr-reader');
        scannerRef.current = scanner;
        setStatusText('Huong camera vao ma QR de mo POI ngay lap tuc.');

        await scanner.start(
          { facingMode: 'environment' },
          { fps: 10, qrbox: { width: 220, height: 220 } },
          (decodedText) => {
            void resolveRef.current(decodedText);
          },
          () => undefined,
        );
      } catch {
        setStatusText('Khong khoi dong duoc camera QR. Ban van co the nhap ma thu cong.');
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
  }, []);

  return (
    <section className="shell py-12">
      <p className="section-kicker">QR USER FLOW</p>
      <h1 className="mt-2 text-4xl font-bold">Quet QR hoac nhap ma kich hoat</h1>
      <p className="mt-3 max-w-2xl text-slate-500">
        Website-user nay da duoc noi vao public endpoint `qr-activations/resolve`, co camera scan va fallback nhap ma POI / deep link.
      </p>

      <div className="mt-8 grid gap-6 lg:grid-cols-[1.1fr_.9fr]">
        <div className="rounded-[2rem] bg-white p-6 shadow-soft dark:bg-slate-900">
          <div className="flex items-center gap-3">
            <div className="grid h-12 w-12 place-items-center rounded-2xl bg-coral/10 text-coral">
              <QrCode />
            </div>
            <div>
              <p className="font-bold">Camera scanner</p>
              <p className="text-sm text-slate-500">{statusText}</p>
            </div>
          </div>
          <div id="qr-reader" className="mt-5 overflow-hidden rounded-3xl border border-slate-200 dark:border-slate-700" />
        </div>

        <div className="rounded-[2rem] bg-white p-6 shadow-soft dark:bg-slate-900">
          <h2 className="text-xl font-bold">Nhap ma thu cong</h2>
          <p className="mt-2 text-sm text-slate-500">
            Ho tro `KHANHHOI-01`, `quan4tourism://qr/...`, `quan4tourism://poi/...` hoac Mongo POI id 24 ky tu.
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
              placeholder="Nhap ma QR, deep link hoac poi id"
            />
            <button className="btn-primary justify-center">
              <QrCode size={16} />
              Mo noi dung tu QR
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
        <h1 className="mt-2 text-4xl font-bold">Dang nhap</h1>
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
            Dang nhap
          </button>
        </form>

        <p className="mt-5 text-sm text-slate-500">
          Chua co tai khoan?{' '}
          <Link to="/register" className="font-bold text-coral">
            Dang ky ngay
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
        <h1 className="mt-2 text-4xl font-bold">Tao tai khoan user</h1>
        <p className="mt-2 text-slate-500">Mo FE cho API auth/register va luon dang nhap sau khi tao tai khoan thanh cong.</p>

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
            Tao tai khoan va dang nhap
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
      alert('Da gui dang ky owner thanh cong.');
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
              Dang xuat
            </button>
          </div>
        </div>

        <div className="rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
          {canOpenOwnerWorkspace ? (
            <>
              <p className="section-kicker">OWNER DA DUOC DUYET</p>
              <h2 className="mt-2 text-3xl font-bold">Tai khoan nay da la owner</h2>
              <p className="mt-2 text-slate-500">
                Admin da xac nhan quyen owner cho tai khoan nay. Ban khong can gui lai form dang ky.
              </p>
              <div className="mt-6 rounded-2xl bg-emerald-50 p-5 text-emerald-800">
                <p className="font-bold">Trang quan ly owner da san sang.</p>
                <p className="mt-1 text-sm">Ban co the vao workspace owner de xem dia diem, luot xem, luot nghe audio va luot quet QR.</p>
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
              <h2 className="mt-2 text-3xl font-bold">Tai khoan nay chua co quyen owner de vao workspace</h2>
              <p className="mt-2 text-slate-500">
                He thong dang ghi nhan trang thai owner da duoc duyet, nhung tai khoan hien tai chua co role `OWNER` de mo trang quan ly owner.
              </p>
              <div className="mt-6 rounded-2xl bg-amber-50 p-5 text-amber-900">
                <p className="font-bold">Neu day la tai khoan owner vua duoc duyet:</p>
                <p className="mt-1 text-sm">Hay dang xuat dang nhap lai. Neu van khong vao duoc, admin can kiem tra role `OWNER` tren tai khoan nay.</p>
              </div>
            </>
          ) : (
            <>
              <p className="section-kicker">REGISTER OWNER</p>
              <h2 className="mt-2 text-3xl font-bold">Gui yeu cau owner</h2>
              <p className="mt-2 text-slate-500">
                FE nay da noi thang vao `POST /api/v1/auth/register-owner`. Khi duoc approve, tai khoan se co role `OWNER`.
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
                <Field label="Dia chi kinh doanh">
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
      <h1 className="mt-2 text-4xl font-bold">Quan ly dia diem va submissions</h1>
      <p className="mt-2 max-w-3xl text-slate-500">
        Trang owner nay da co dashboard tong quan, danh sach dia diem cua chinh ban, luot vao trang, so nguoi da nghe audio, luot quet QR va khu gui yeu cau cap nhat noi dung.
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
            ['Luot vao trang', dashboardQuery.data?.totalViews || 0],
            ['Nguoi vao trang', dashboardQuery.data?.uniqueVisitors || 0],
            ['Luot nghe audio', dashboardQuery.data?.totalAudioPlays || 0],
            ['Nguoi da nghe', dashboardQuery.data?.uniqueAudioListeners || 0],
            ['Luot quet QR', dashboardQuery.data?.totalQrScans || 0],
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
            <h2 className="text-2xl font-bold">Dia diem cua toi</h2>
            <p className="mt-2 text-sm text-slate-500">
              Du lieu lay tu `GET /api/v1/owner/pois`, gom thong tin POI, luot vao trang, nguoi nghe audio va so lan quet QR theo tung dia diem.
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
            Tao submission moi
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
            <p className="font-bold">Chua co dia diem nao duoc gan cho owner nay</p>
            <p className="mt-1 text-sm text-slate-500">
              Ban van co the tao submission moi o phan ben duoi de de xuat dia diem moi hoac yeu cau cap nhat.
            </p>
          </div>
        )}
      </div>

      <div className="mt-8 grid gap-6 lg:grid-cols-[1.05fr_.95fr]">
        <div ref={submissionFormRef} className="rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
          <h2 className="text-2xl font-bold">Tao submission moi</h2>
          <p className="mt-2 text-sm text-slate-500">
            Ban co the tao submission moi, sua submission pending, hoac tu mot dia diem dang quan ly tao nhanh submission `update` da prefill san du lieu hien tai.
          </p>

          {editingSubmissionId ? (
            <div className="mt-4 flex flex-wrap items-center gap-3 rounded-2xl bg-amber-50 px-4 py-3 text-sm text-amber-800">
              <span>Dang sua submission {editingSubmissionId}</span>
              <button
                type="button"
                className="font-bold text-coral"
                onClick={() => {
                  setEditingSubmissionId(null);
                  setSubmissionForm(createEmptySubmissionForm());
                }}
              >
                Huy che do sua
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
            <Field label="Loai submission">
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
            <Field label="POI id (neu update)">
              <TextInput
                value={submissionForm.poiId}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, poiId: event.target.value }))}
              />
            </Field>
            <Field label="Ten dia diem">
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
                <option value="">Chon category</option>
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
            <Field label="Dia chi">
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
            <Field label="Noi dung thuyet minh (de trong se doc mo ta)">
              <TextArea
                value={submissionForm.ttsScript}
                onChange={(event) =>
                  setSubmissionForm((current) => ({ ...current, ttsScript: event.target.value }))
                }
                placeholder="Nhap kich ban rieng neu muon doc khac phan mo ta"
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
              <span>Bat auto narration</span>
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
              {editingSubmissionId ? 'Cap nhat submission owner' : 'Tao submission owner'}
            </button>
          </form>
        </div>

        <div className="rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
          <h2 className="text-2xl font-bold">Danh sach submissions</h2>
          <p className="mt-2 text-sm text-slate-500">Du lieu lay tu `GET /api/v1/owner/submissions`.</p>

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
              <p className="font-bold">Chua co submission nao</p>
              <p className="mt-1 text-sm text-slate-500">Gui submission dau tien cua ban bang form ben trai.</p>
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
            {poi.isActive ? 'Dang hien thi' : 'Tam an'}
          </span>
          <span className="pill">Audio: {poi.audioStatus}</span>
          {poi.activationRequested ? <span className="pill border-amber-300 text-amber-700">Cho duyet kich hoat</span> : null}
        </div>

        <h3 className="mt-4 text-2xl font-bold">{poi.name}</h3>
        <p className="mt-2 line-clamp-2 text-sm leading-6 text-slate-500">{poi.description}</p>

        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800">
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">Luot vao trang</p>
            <p className="mt-1 text-2xl font-bold">{poi.viewCount}</p>
          </div>
          <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800">
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-amber-600">Nguoi vao trang</p>
            <p className="mt-1 text-2xl font-bold">{poi.uniqueVisitorCount}</p>
          </div>
          <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800">
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-teal">Luot nghe</p>
            <p className="mt-1 text-2xl font-bold">{poi.audioPlayCount}</p>
          </div>
          <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800">
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-sky-600">Nguoi da nghe</p>
            <p className="mt-1 text-2xl font-bold">{poi.uniqueAudioListenerCount}</p>
          </div>
          <div className="rounded-2xl bg-slate-100 p-4 dark:bg-slate-800 md:col-span-2">
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-fuchsia-600">Luot quet QR</p>
            <p className="mt-1 text-2xl font-bold">{poi.qrScanCount}</p>
          </div>
        </div>

        <div className="mt-4 space-y-2 text-sm text-slate-500">
          <p className="flex items-start gap-2">
            <MapPin size={16} className="mt-0.5 shrink-0 text-coral" />
            <span>{poi.address}, {poi.ward}, {poi.district}</span>
          </p>
          <p>Gia: {poi.priceRange} · Priority: {poi.priority}</p>
          <p>Cap nhat: {new Date(poi.updatedAt).toLocaleString()}</p>
        </div>

        <div className="mt-5 flex flex-wrap gap-3">
          <Link className="btn-secondary !px-4 !py-2" to={`/poi/${poi.id}`}>
            Xem chi tiet
          </Link>
          <a className="btn-secondary !px-4 !py-2" href={mapUrl} target="_blank" rel="noreferrer">
            <Navigation size={16} />
            Chi duong
          </a>
          <button className="btn-primary !px-4 !py-2" onClick={onCreateUpdate}>
            Gui yeu cau cap nhat <ArrowRight size={16} />
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
        <p>Auto narration: {submission.autoNarrationEnabled ? 'bat' : 'tat'}</p>
        <p>Tao luc: {new Date(submission.createdAt).toLocaleString()}</p>
        {submission.adminNote ? <p>Admin note: {submission.adminNote}</p> : null}
      </div>

      {submission.status === 'pending' ? (
        <button onClick={onEdit} className="mt-4 text-sm font-bold text-coral">
          Sua submission pending
        </button>
      ) : null}
    </article>
  );
}

function About() {
  return (
    <section className="shell py-12">
      <p className="section-kicker">CAU CHUYEN DU AN</p>
      <h1 className="mt-2 max-w-3xl text-4xl font-bold leading-tight">
        Mot nguoi ban dong hanh cho hanh trinh an ngon o Quan 4.
      </h1>

      <div className="mt-10 grid gap-8 lg:grid-cols-2">
        <div className="rounded-[2rem] bg-orange-100 p-8 dark:bg-orange-500/10">
          <h2 className="text-2xl font-bold">Muc tieu</h2>
          <p className="mt-4 leading-7 text-slate-600 dark:text-slate-300">
            He thong giup du khach tim, nghe va cam nhan nhung diem am thuc dia phuong mot cach truc quan qua web, mobile va ban do thong minh.
          </p>
        </div>

        <div className="rounded-[2rem] bg-teal/10 p-8">
          <h2 className="text-2xl font-bold">Co gi trong trai nghiem?</h2>
          <ul className="mt-4 grid gap-3 text-slate-600 dark:text-slate-300">
            {[
              'POI am thuc duoc tuyen chon',
              'GPS va goi y gan ban',
              'Audio thuyet minh da ngon ngu',
              'QR user flow va deep-link resolve',
              'Tours cong khai va owner workspace',
            ].map((item) => (
              <li key={item} className="flex gap-2">
                <span className="text-teal">✦</span>
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
        <h1 className="mt-3 text-2xl font-bold">Khong tim thay trang nay</h1>
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
      <AnimatePresence mode="wait">
        <Routes>
          <Route element={<PublicLayout />}>
            <Route path="/" element={<Home />} />
            <Route path="/explore" element={<Explore />} />
            <Route path="/poi/:id" element={<Detail />} />
            <Route path="/nearby" element={<Nearby />} />
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
