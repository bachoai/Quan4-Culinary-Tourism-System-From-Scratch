import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { motion } from 'framer-motion';
import { Globe, Mail, MapPin, Navigation, Phone, Star } from 'lucide-react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { audioApi } from '../api/audioApi';
import { categoryApi } from '../api/categoryApi';
import { poiApi } from '../api/poiApi';
import { AudioPlayer } from '../components/common/AudioPlayer';
import { ErrorBox } from '../components/common/ErrorBox';
import { PoiCard } from '../components/common/PoiCard';
import { Spinner } from '../components/common/Spinner';
import { LANGUAGE_OPTIONS } from '../constants/languages';
import { getCopy } from '../i18n/copy';
import { useAppStore } from '../store/appStore';
import { track } from '../utils/analytics';
import { poiImage } from '../utils/media';

export default function PoiDetailPage() {
  const { id = '' } = useParams();
  const location = useLocation();
  const { lang, audioLang, setAudioLang } = useAppStore();
  const ui = getCopy(lang);
  const queryParams = new URLSearchParams(location.search);
  const autoplay = queryParams.get('autoplay');
  const source = queryParams.get('source');
  const { data: poi, isLoading, isError, error } = useQuery({
    queryKey: ['detail', id, lang],
    queryFn: () => poiApi.detail(id, lang),
  });
  const audioLanguagesQuery = useQuery({
    queryKey: ['audio-languages'],
    queryFn: audioApi.getLanguages,
    retry: false,
    staleTime: 300000,
  });
  const audioQuery = useQuery({
    queryKey: ['audio', id, audioLang],
    queryFn: () => audioApi.getPoiAudio(id, audioLang),
    enabled: Boolean(id),
    retry: false,
  });
  const categoriesQuery = useQuery({
    queryKey: ['categories'],
    queryFn: categoryApi.list,
  });
  const relatedQuery = useQuery({
    queryKey: ['related-pois', id, lang, poi?.categoryId],
    enabled: Boolean(poi?.categoryId),
    queryFn: async () => {
      const related = await poiApi.list({ lang, categoryId: poi!.categoryId });
      return related.filter((item) => item.id !== id).slice(0, 3);
    },
  });

  useEffect(() => {
    if (id) {
      track('poi_viewed', lang, id, source ? { source } : undefined);
    }
  }, [id, lang, source]);

  const supportedAudioLanguageCodes = new Set(
    audioLanguagesQuery.data?.map((item) => item.code) ?? LANGUAGE_OPTIONS.map((item) => item.value),
  );
  const audioLanguageOptions = LANGUAGE_OPTIONS.filter((option) => supportedAudioLanguageCodes.has(option.value));

  if (isLoading) {
    return <Spinner />;
  }

  if (isError || !poi) {
    return (
      <section className="shell py-20">
        <ErrorBox text={(error as Error | undefined)?.message || 'Không tải được chi tiết địa điểm.'} />
      </section>
    );
  }

  const categoryName = categoriesQuery.data?.find((category) => category.id === poi.categoryId)?.name;
  const mapUrl = `https://www.google.com/maps/dir/?api=1&destination=${poi.latitude},${poi.longitude}`;
  const narrationText = poi.ttsScript || poi.description || '';
  const relatedTitle = lang === 'en' ? 'Related places' : 'Địa điểm liên quan';
  const relatedEmpty = lang === 'en' ? 'No related places yet.' : 'Chưa có địa điểm liên quan.';
  const categoryLabel = lang === 'en' ? 'Category' : 'Danh mục';
  const contactLabel = lang === 'en' ? 'Contact' : 'Liên hệ';

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
            {categoryName ? <span className="pill">{categoryLabel}: {categoryName}</span> : null}
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
            {poi.address}, {poi.ward}, {poi.district}, {poi.city}
          </p>

          <div className="mt-6">
            <AudioPlayer
              audioUrl={audioQuery.data?.audioUrl}
              text={narrationText}
              uiLang={lang}
              narrationLang={audioLang}
              autoplay={Boolean(autoplay)}
              loading={audioQuery.isLoading}
              errorText={audioQuery.isError ? ui.detail.audioError : undefined}
              languageOptions={audioLanguageOptions}
              onLanguageChange={setAudioLang}
              onPlay={(mode) =>
                track('audio_played', audioLang, poi.id, autoplay ? { autoplay, mode } : { mode })
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

      <div className="mt-9 grid gap-6 lg:grid-cols-[1fr_1fr]">
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

          <div className="mt-5 space-y-2 text-sm text-slate-500">
            <p className="font-semibold text-slate-700 dark:text-slate-200">{contactLabel}</p>
            {poi.contactInfo?.phone ? (
              <p className="flex items-center gap-2">
                <Phone size={15} className="text-coral" />
                {poi.contactInfo.phone}
              </p>
            ) : null}
            {poi.contactInfo?.email ? (
              <p className="flex items-center gap-2">
                <Mail size={15} className="text-coral" />
                <a href={`mailto:${poi.contactInfo.email}`} className="hover:text-coral">
                  {poi.contactInfo.email}
                </a>
              </p>
            ) : null}
            {poi.contactInfo?.websiteUrl ? (
              <p className="flex items-center gap-2">
                <Globe size={15} className="text-coral" />
                <a href={poi.contactInfo.websiteUrl} target="_blank" rel="noreferrer" className="hover:text-coral">
                  {poi.contactInfo.websiteUrl}
                </a>
              </p>
            ) : null}
            {poi.contactInfo?.facebookUrl ? (
              <p className="flex items-center gap-2">
                <Globe size={15} className="text-coral" />
                <a href={poi.contactInfo.facebookUrl} target="_blank" rel="noreferrer" className="hover:text-coral">
                  {poi.contactInfo.facebookUrl}
                </a>
              </p>
            ) : null}
          </div>
        </div>
      </div>

      <div className="mt-10">
        <div className="flex items-center justify-between gap-4">
          <div>
            <p className="section-kicker">{relatedTitle}</p>
            <h2 className="mt-2 text-3xl font-bold">{poi.name}</h2>
          </div>
        </div>
        {relatedQuery.isLoading ? (
          <Spinner />
        ) : relatedQuery.data?.length ? (
          <div className="mt-6 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {relatedQuery.data.map((item) => (
              <PoiCard key={item.id} poi={item} category={categoryName} />
            ))}
          </div>
        ) : (
          <div className="mt-6 rounded-3xl bg-slate-100 p-8 text-center dark:bg-slate-900">
            <p className="font-bold">{relatedEmpty}</p>
          </div>
        )}
      </div>
    </section>
  );
}
