import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router-dom';
import { MapPin, Navigation } from 'lucide-react';
import { audioApi } from '../api/audioApi';
import { mapsApi } from '../api/mapsApi';
import { poiApi } from '../api/poiApi';
import { routeApi } from '../api/routeApi';
import { AudioPlayer } from '../components/common/AudioPlayer';
import { Categories } from '../components/common/Categories';
import { ErrorBox } from '../components/common/ErrorBox';
import { Spinner } from '../components/common/Spinner';
import { PoiMap } from '../components/map/PoiMap';
import { useAppStore } from '../store/appStore';
import { distance, track } from '../utils/analytics';

const EARTH_RADIUS_METERS = 6371000;
const DEFAULT_ARRIVAL_RADIUS_METERS = 60;

function toRadians(degrees: number) {
  return (degrees * Math.PI) / 180;
}

function calculateDistanceMeters(from: { lat: number; lng: number }, to: { lat: number; lng: number }) {
  const latDelta = toRadians(to.lat - from.lat);
  const lngDelta = toRadians(to.lng - from.lng);
  const fromLat = toRadians(from.lat);
  const toLat = toRadians(to.lat);
  const a =
    Math.sin(latDelta / 2) * Math.sin(latDelta / 2) +
    Math.cos(fromLat) * Math.cos(toLat) * Math.sin(lngDelta / 2) * Math.sin(lngDelta / 2);

  return 2 * EARTH_RADIUS_METERS * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

export default function MapPage() {
  const { lang, location, setLocation } = useAppStore();
  const [searchParams, setSearchParams] = useSearchParams();
  const [geoError, setGeoError] = useState('');
  const [isLocating, setIsLocating] = useState(false);
  const [categoryId, setCategoryId] = useState(searchParams.get('category') || '');
  const [priceRange, setPriceRange] = useState(searchParams.get('price') || '');
  const selectedPoiId = searchParams.get('poi') || '';
  const navigationMode = searchParams.get('nav') === '1' && Boolean(selectedPoiId);

  const { data: pois = [], isLoading, isError, error } = useQuery({
    queryKey: ['map-pois', lang],
    queryFn: () => poiApi.list({ lang }),
  });
  const mapPackQuery = useQuery({
    queryKey: ['map-pack-manifest'],
    queryFn: mapsApi.getPackManifest,
    retry: false,
    staleTime: 300000,
  });

  useEffect(() => {
    setCategoryId(searchParams.get('category') || '');
    setPriceRange(searchParams.get('price') || '');
  }, [searchParams]);

  const filteredPois = useMemo(
    () =>
      pois.filter((poi) => {
        if (categoryId && poi.categoryId !== categoryId) {
          return false;
        }
        if (priceRange && poi.priceRange !== priceRange) {
          return false;
        }
        return true;
      }),
    [categoryId, pois, priceRange],
  );

  const selectedPoi = pois.find((poi) => poi.id === selectedPoiId);
  const visiblePois = useMemo(() => {
    if (!selectedPoi) {
      return filteredPois;
    }

    if (navigationMode) {
      return [selectedPoi];
    }

    if (filteredPois.some((poi) => poi.id === selectedPoi.id)) {
      return filteredPois;
    }

    return [selectedPoi, ...filteredPois];
  }, [filteredPois, navigationMode, selectedPoi]);

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
  const audioQuery = useQuery({
    queryKey: ['map-navigation-audio', selectedPoiId, lang],
    queryFn: () => audioApi.getPoiAudio(selectedPoiId, lang),
    enabled: Boolean(navigationMode && selectedPoiId),
    retry: false,
  });

  const arrivalRadiusMeters = selectedPoi?.geofenceRadiusMeters && selectedPoi.geofenceRadiusMeters > 0
    ? selectedPoi.geofenceRadiusMeters
    : DEFAULT_ARRIVAL_RADIUS_METERS;
  const arrivalDistanceMeters = useMemo(() => {
    if (!location || !selectedPoi) {
      return null;
    }

    return calculateDistanceMeters(location, {
      lat: selectedPoi.latitude,
      lng: selectedPoi.longitude,
    });
  }, [location, selectedPoi]);
  const hasArrived = Boolean(
    navigationMode &&
      arrivalDistanceMeters !== null &&
      selectedPoi &&
      arrivalDistanceMeters <= arrivalRadiusMeters,
  );
  const narrationText = selectedPoi?.ttsScript?.trim() || selectedPoi?.description?.trim() || '';

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
      setGeoError('Khong lay duoc vi tri hien tai. Hay bat GPS va thu lai.');
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
    if (isLoading || !selectedPoiId || selectedPoi || !pois.length) {
      return;
    }

    const next = new URLSearchParams(searchParams);
    next.delete('poi');
    next.delete('nav');
    setSearchParams(next, { replace: true });
  }, [isLoading, pois.length, searchParams, selectedPoi, selectedPoiId, setSearchParams]);

  const updateFilters = (nextCategoryId: string, nextPriceRange: string) => {
    const next = new URLSearchParams(searchParams);
    if (nextCategoryId) {
      next.set('category', nextCategoryId);
    } else {
      next.delete('category');
    }
    if (nextPriceRange) {
      next.set('price', nextPriceRange);
    } else {
      next.delete('price');
    }
    setSearchParams(next, { replace: true });
  };

  const focusPoi = (poiId: string) => {
    const next = new URLSearchParams(searchParams);
    next.set('poi', poiId);
    if (poiId !== selectedPoiId) {
      next.delete('nav');
    }
    setSearchParams(next, { replace: true });
  };

  const startNavigation = (poiId = selectedPoiId) => {
    if (!poiId) {
      return;
    }

    const next = new URLSearchParams(searchParams);
    next.set('poi', poiId);
    next.set('nav', '1');
    setSearchParams(next, { replace: true });
  };

  const stopNavigation = () => {
    const next = new URLSearchParams(searchParams);
    next.delete('nav');
    setSearchParams(next, { replace: true });
  };

  const clearFocusPoi = () => {
    const next = new URLSearchParams(searchParams);
    next.delete('poi');
    next.delete('nav');
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
        setGeoError('Khong lay duoc vi tri hien tai. Hay bat GPS va thu lai.');
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
      <h1 className="mt-2 text-4xl font-bold">Ban do am thuc</h1>
      <p className="mt-2 text-slate-500">Lay vi tri that cua ban, chon mot POI va xem duong di ngay tren ban do.</p>

      <div className="mt-5 grid gap-3 rounded-[2rem] bg-white p-5 shadow-soft dark:bg-slate-900">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">Bo loc ban do</p>
            <p className="mt-1 text-sm text-slate-500">
              {mapPackQuery.data
                ? `Offline pack san sang: ${mapPackQuery.data.name} v${mapPackQuery.data.version}`
                : 'Dang dung tile online cho local/demo. Co the cau hinh MapTiler bang VITE_MAPTILER_KEY.'}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <button type="button" className="btn-primary" onClick={refreshLocation} disabled={isLocating}>
              <Navigation size={17} />
              {isLocating ? 'Dang lay GPS' : 'Tim quanh toi'}
            </button>
            <Link className="btn-secondary" to="/nearby">
              <MapPin size={17} />
              Mo Nearby
            </Link>
          </div>
        </div>

        <Categories selected={categoryId} onSelect={(id) => updateFilters(id || '', priceRange)} />

        <div className="flex flex-wrap gap-2">
          {['$', '$$', '$$$'].map((price) => (
            <button
              type="button"
              key={price}
              onClick={() => updateFilters(categoryId, priceRange === price ? '' : price)}
              className={`pill ${priceRange === price ? 'border-coral bg-orange-50 text-coral' : ''}`}
            >
              {price}
            </button>
          ))}
        </div>
      </div>

      <div className="mt-6 grid gap-4 rounded-[2rem] bg-white p-5 shadow-soft dark:bg-slate-900 md:grid-cols-[1.2fr_.8fr_auto] md:items-center">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">GPS thoi gian thuc</p>
          <p className="mt-2 text-sm text-slate-500">
            {location
              ? `Vi tri hien tai: ${location.lat.toFixed(5)}, ${location.lng.toFixed(5)}`
              : isLocating
                ? 'Dang lay vi tri hien tai cua ban...'
                : 'Chua lay duoc vi tri. Bam nut ben phai de bat dinh vi.'}
          </p>
          {geoError ? <p className="mt-2 text-sm text-rose-600">{geoError}</p> : null}
        </div>

        <div className="rounded-2xl bg-slate-50 p-4 dark:bg-slate-800">
          {selectedPoi ? (
            <>
              <p className="text-xs font-bold uppercase tracking-[0.2em] text-teal">
                {navigationMode ? 'Che do dan duong' : 'POI dang chon'}
              </p>
              <p className="mt-2 text-lg font-bold">{selectedPoi.name}</p>
              <p className="mt-1 text-sm text-slate-500">{selectedPoi.address}</p>

              {navigationMode && arrivalDistanceMeters !== null ? (
                <div className="mt-3 flex flex-wrap gap-2 text-sm">
                  <span className={`pill ${hasArrived ? 'border-coral text-coral' : 'border-teal text-teal'}`}>
                    {hasArrived ? 'Da den noi' : `Con ${distance(arrivalDistanceMeters)}`}
                  </span>
                  <span className="pill">Ban kinh den noi {distance(arrivalRadiusMeters)}</span>
                </div>
              ) : null}

              {routeQuery.data ? (
                <div className="mt-3 flex flex-wrap gap-2 text-sm">
                  <span className="pill border-teal text-teal">{distance(routeQuery.data.distanceMeters)}</span>
                  <span className="pill">{durationText}</span>
                </div>
              ) : routeQuery.isLoading ? (
                <p className="mt-2 text-sm text-slate-500">Dang tim duong di tren mang luoi duong pho...</p>
              ) : routeQuery.isError ? (
                <p className="mt-2 text-sm text-rose-600">{(routeQuery.error as Error).message}</p>
              ) : location ? (
                <p className="mt-2 text-sm text-slate-500">Dang san sang chi duong ngay khi co route.</p>
              ) : (
                <p className="mt-2 text-sm text-slate-500">Can vi tri that cua ban de ve duong di.</p>
              )}
            </>
          ) : (
            <>
              <p className="text-xs font-bold uppercase tracking-[0.2em] text-teal">Chua chon diem den</p>
              <p className="mt-2 text-sm text-slate-500">Hay bam vao marker hoac mot dong trong danh sach de xem route va so km.</p>
            </>
          )}
        </div>

        <div className="flex flex-wrap gap-2 md:justify-end">
          {selectedPoi && !navigationMode ? (
            <button type="button" className="btn-primary" onClick={() => startNavigation(selectedPoi.id)}>
              <Navigation size={17} />
              Bat dau dan duong
            </button>
          ) : null}
          {navigationMode ? (
            <button type="button" className="btn-secondary" onClick={stopNavigation}>
              Thoat che do
            </button>
          ) : null}
          {selectedPoi ? (
            <button type="button" className="btn-secondary" onClick={clearFocusPoi}>
              Bo chon diem
            </button>
          ) : null}
        </div>
      </div>

      {navigationMode && selectedPoi ? (
        <div className="mt-4 rounded-[2rem] border border-teal/20 bg-teal/10 p-5">
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-teal">
            {hasArrived ? 'Da den noi' : 'Dang dan duong'}
          </p>
          <h2 className="mt-2 text-2xl font-bold">
            {hasArrived ? `Ban da den ${selectedPoi.name}` : `Dang dan duong den ${selectedPoi.name}`}
          </h2>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-200">
            {hasArrived
              ? 'Trang web se tu phat audio gioi thieu cho diem den nay.'
              : arrivalDistanceMeters !== null
                ? `Con ${distance(arrivalDistanceMeters)} de toi diem den. Cac POI khac dang duoc an de ban tap trung theo duong di.`
                : 'Can GPS thoi gian thuc de theo doi qua trinh di chuyen den diem den.'}
          </p>

          {hasArrived ? (
            <div className="mt-4">
              <AudioPlayer
                key={`map-navigation-${selectedPoi.id}`}
                audioUrl={audioQuery.data?.audioUrl}
                text={narrationText}
                lang={lang}
                autoplay
                loading={audioQuery.isLoading}
                errorText={audioQuery.isError ? 'Khong tai duoc audio gioi thieu luc den noi.' : undefined}
                onPlay={(mode) =>
                  track('audio_played', lang, selectedPoi.id, {
                    source: 'navigation_mode',
                    autoplay: 'arrived',
                    mode,
                  })
                }
              />
            </div>
          ) : null}
        </div>
      ) : null}

      <div className="mt-7 grid gap-5 lg:grid-cols-[1.3fr_.7fr]">
        <PoiMap
          pois={visiblePois}
          userLocation={location}
          selectedPoiId={selectedPoiId || undefined}
          routeGeometry={routeQuery.data?.geometry}
          onSelectPoi={focusPoi}
        />

        <div className="max-h-[620px] space-y-3 overflow-y-auto pr-1 md:max-h-[700px] xl:max-h-[760px]">
          {isLoading ? (
            <Spinner />
          ) : isError ? (
            <ErrorBox text={(error as Error | undefined)?.message || 'Khong tai duoc du lieu ban do.'} />
          ) : navigationMode && selectedPoi ? (
            <div className="rounded-2xl bg-white p-5 shadow-soft dark:bg-slate-900">
              <p className="text-xs font-bold uppercase tracking-[0.2em] text-teal">Diem den hien tai</p>
              <p className="mt-2 text-xl font-bold">{selectedPoi.name}</p>
              <p className="mt-1 text-sm text-slate-500">
                {selectedPoi.address}
                {selectedPoi.ward ? `, ${selectedPoi.ward}` : ''}
                {selectedPoi.district ? `, ${selectedPoi.district}` : ''}
              </p>

              {arrivalDistanceMeters !== null ? (
                <div className="mt-3 flex flex-wrap gap-2 text-sm">
                  <span className={`pill ${hasArrived ? 'border-coral text-coral' : 'border-teal text-teal'}`}>
                    {hasArrived ? 'Da den noi' : `Con ${distance(arrivalDistanceMeters)}`}
                  </span>
                  {routeQuery.data ? <span className="pill">{distance(routeQuery.data.distanceMeters)}</span> : null}
                  {durationText ? <span className="pill">{durationText}</span> : null}
                </div>
              ) : null}

              <p className="mt-4 text-sm text-slate-500">
                {hasArrived
                  ? 'Audio gioi thieu dang san sang tren khung thong bao ben trai.'
                  : 'Ban do chi hien user, diem den va duong di. Cac dia diem khac tam an trong khi dan duong.'}
              </p>

              <div className="mt-4 flex flex-wrap gap-2">
                <button type="button" className="btn-secondary" onClick={stopNavigation}>
                  Thoat che do
                </button>
                <Link to={`/poi/${selectedPoi.id}`} className="pill">
                  Xem chi tiet
                </Link>
              </div>
            </div>
          ) : visiblePois.length ? (
            visiblePois.map((poi) => (
              <div
                key={poi.id}
                className={`rounded-2xl bg-white p-4 shadow-sm transition dark:bg-slate-900 ${selectedPoiId === poi.id ? 'ring-2 ring-teal' : 'hover:shadow-soft'}`}
              >
                <button type="button" className="block w-full text-left" onClick={() => focusPoi(poi.id)}>
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-bold">{poi.name}</p>
                    <span className="pill border-coral text-coral">{poi.priceRange}</span>
                  </div>
                  <p className="mt-1 flex items-center gap-1 text-sm text-slate-500">
                    <MapPin size={14} />
                    {poi.address}
                  </p>
                </button>

                <div className="mt-3 flex flex-wrap gap-2">
                  <button type="button" className="pill border-teal text-teal" onClick={() => focusPoi(poi.id)}>
                    {selectedPoiId === poi.id ? 'Dang duoc chon' : 'Xem route tren ban do'}
                  </button>
                  <button type="button" className="pill border-coral text-coral" onClick={() => startNavigation(poi.id)}>
                    {selectedPoiId === poi.id ? 'Bat dau dan duong' : 'Dan duong ngay'}
                  </button>
                  <Link to={`/poi/${poi.id}`} className="pill">
                    Xem chi tiet
                  </Link>
                </div>
              </div>
            ))
          ) : (
            <div className="rounded-3xl bg-slate-100 p-8 text-center dark:bg-slate-900">
              <p className="font-bold">Khong co POI phu hop bo loc hien tai</p>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
