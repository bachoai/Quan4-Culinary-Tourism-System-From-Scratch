import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Clock3, MapPinned, Plus, Route as RouteIcon, Trash2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import { poiApi } from '../api/poiApi';
import { tourApi } from '../api/tourApi';
import { ErrorBox } from '../components/common/ErrorBox';
import { Field, TextArea, TextInput } from '../components/common/FormControls';
import { Spinner } from '../components/common/Spinner';
import { heroImage } from '../constants/heroImage';
import { useAppStore } from '../store/appStore';
import type { CreateTourRequest } from '../types/requests';
import type { Poi, TourResponse } from '../types/responses';
import { normalizeMediaUrl, poiImage } from '../utils/media';

type DraftTourStop = {
  poiId: string;
  title: string;
  estimatedStayMinutes: number;
};

function sortStops<T extends { order: number }>(stops: T[]) {
  return stops.slice().sort((left, right) => left.order - right.order);
}

function buildTourMapLink(tour: TourResponse) {
  const params = new URLSearchParams();
  const stopIds = sortStops(tour.stops).map((stop) => stop.poiId).join(',');
  if (stopIds) {
    params.set('tour', stopIds);
  }
  if (tour.title) {
    params.set('tourTitle', tour.title);
  }
  const firstPoiId = sortStops(tour.stops)[0]?.poiId;
  if (firstPoiId) {
    params.set('poi', firstPoiId);
  }
  return `/map?${params.toString()}`;
}

function TourCard({
  tour,
  poiLookup,
  accentLabel,
}: {
  tour: TourResponse;
  poiLookup: Record<string, Poi>;
  accentLabel: string;
}) {
  const orderedStops = sortStops(tour.stops);
  const cover = tour.coverImageUrl
    ? normalizeMediaUrl(tour.coverImageUrl)
    : poiLookup[orderedStops[0]?.poiId]
      ? poiImage(poiLookup[orderedStops[0].poiId])
      : heroImage;

  return (
    <article className="overflow-hidden rounded-[2rem] bg-white shadow-soft dark:bg-slate-900">
      <img src={cover} alt={tour.title} className="h-56 w-full object-cover" />
      <div className="p-6">
        <div className="flex flex-wrap items-center gap-2">
          <span className="pill border-coral text-coral">{accentLabel}</span>
          <span className="pill">{tour.lang.toUpperCase()}</span>
          <span className="pill">{tour.estimatedDurationMinutes} phut</span>
          <span className="pill">{tour.stops.length} diem dung</span>
        </div>

        <h2 className="mt-4 text-2xl font-bold">{tour.title}</h2>
        <p className="mt-2 leading-7 text-slate-600 dark:text-slate-300">{tour.description}</p>

        <div className="mt-5 flex flex-wrap gap-2">
          <Link className="pill border-teal text-teal" to={buildTourMapLink(tour)}>
            <MapPinned size={14} className="mr-1 inline" />
            Xem lo trinh
          </Link>
        </div>

        <div className="mt-5 space-y-3">
          {orderedStops.map((stop) => {
            const poi = poiLookup[stop.poiId];
            return (
              <div
                key={`${tour.id}-${stop.poiId}-${stop.order}`}
                className="rounded-2xl border border-slate-200 p-4 dark:border-slate-800"
              >
                <div className="flex items-center justify-between gap-4">
                  <div>
                    <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">Diem dung {stop.order + 1}</p>
                    <h3 className="mt-1 font-bold">{stop.title || poi?.name || stop.poiId}</h3>
                    <p className="mt-1 text-sm text-slate-500">{poi?.address || 'POI se duoc tai tren ban do tour.'}</p>
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

export default function ToursPage() {
  const queryClient = useQueryClient();
  const { lang, isAuthenticated } = useAppStore();
  const [form, setForm] = useState({
    title: '',
    description: '',
    selectedPoiId: '',
    stopTitle: '',
    estimatedStayMinutes: 20,
  });
  const [draftStops, setDraftStops] = useState<DraftTourStop[]>([]);
  const [formError, setFormError] = useState('');

  const toursQuery = useQuery({
    queryKey: ['public-tours', lang],
    queryFn: () => tourApi.list(lang),
  });
  const myToursQuery = useQuery({
    queryKey: ['my-tours'],
    queryFn: tourApi.listMine,
    enabled: isAuthenticated,
  });
  const poisQuery = useQuery({
    queryKey: ['tour-pois', lang],
    queryFn: () => poiApi.list({ lang }),
  });

  const poiLookup = Object.fromEntries((poisQuery.data || []).map((poi) => [poi.id, poi])) as Record<string, Poi>;
  const availablePois = useMemo(
    () => (poisQuery.data || []).filter((poi) => !draftStops.some((stop) => stop.poiId === poi.id)),
    [draftStops, poisQuery.data],
  );
  const totalDurationMinutes = draftStops.reduce((total, stop) => total + stop.estimatedStayMinutes, 0);

  const createTourMutation = useMutation({
    mutationFn: (payload: CreateTourRequest) => tourApi.createMine(payload),
    onSuccess: async () => {
      setForm({
        title: '',
        description: '',
        selectedPoiId: '',
        stopTitle: '',
        estimatedStayMinutes: 20,
      });
      setDraftStops([]);
      setFormError('');
      await queryClient.invalidateQueries({ queryKey: ['my-tours'] });
    },
  });

  const addStop = () => {
    if (!form.selectedPoiId) {
      setFormError('Hay chon mot dia diem truoc khi them vao tour.');
      return;
    }

    const poi = poiLookup[form.selectedPoiId];
    if (!poi) {
      setFormError('Dia diem da chon khong con cong khai de them vao tour.');
      return;
    }

    setDraftStops((current) => [
      ...current,
      {
        poiId: poi.id,
        title: form.stopTitle.trim(),
        estimatedStayMinutes: Math.max(1, form.estimatedStayMinutes || 15),
      },
    ]);
    setForm((current) => ({
      ...current,
      selectedPoiId: '',
      stopTitle: '',
      estimatedStayMinutes: 20,
    }));
    setFormError('');
  };

  const moveStop = (index: number, direction: -1 | 1) => {
    const nextIndex = index + direction;
    if (nextIndex < 0 || nextIndex >= draftStops.length) {
      return;
    }

    setDraftStops((current) => {
      const next = current.slice();
      [next[index], next[nextIndex]] = [next[nextIndex], next[index]];
      return next;
    });
  };

  const updateStop = (index: number, patch: Partial<DraftTourStop>) => {
    setDraftStops((current) =>
      current.map((stop, currentIndex) => (currentIndex === index ? { ...stop, ...patch } : stop)),
    );
  };

  const removeStop = (index: number) => {
    setDraftStops((current) => current.filter((_, currentIndex) => currentIndex !== index));
  };

  const submitMyTour = (event: React.FormEvent) => {
    event.preventDefault();
    setFormError('');

    if (!form.title.trim()) {
      setFormError('Hay nhap ten tour.');
      return;
    }

    if (!form.description.trim()) {
      setFormError('Hay nhap mo ta tour.');
      return;
    }

    if (draftStops.length === 0) {
      setFormError('Tour cua ban can it nhat mot diem dung.');
      return;
    }

    createTourMutation.mutate({
      title: form.title.trim(),
      description: form.description.trim(),
      lang,
      estimatedDurationMinutes: totalDurationMinutes || 15,
      isActive: true,
      stops: draftStops.map((stop, index) => ({
        poiId: stop.poiId,
        title: stop.title.trim() || undefined,
        order: index,
        estimatedStayMinutes: Math.max(1, stop.estimatedStayMinutes || 15),
      })),
    });
  };

  return (
    <section className="shell py-12">
      <p className="section-kicker">TOUR AM THUC</p>
      <h1 className="mt-2 text-4xl font-bold">Lich trinh am thuc cong khai va tour cua ban</h1>
      <p className="mt-2 max-w-2xl text-slate-500">
        Ban co the xem tour cong khai, tu tao tour rieng cho tai khoan cua minh, va mo map de xem lo trinh qua cac POI da chon.
      </p>

      <div className="mt-8 grid gap-6 xl:grid-cols-[1fr_1fr]">
        <div className="rounded-[2rem] bg-white p-6 shadow-soft dark:bg-slate-900">
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">Tour cua toi</p>
          <h2 className="mt-2 text-2xl font-bold">Tao mot tour rieng chi hien cho ban</h2>
          <p className="mt-2 text-sm text-slate-500">
            Moi tour user tao se chi duoc tra ve trong `GET /api/v1/tours/my` cho chinh tai khoan do.
          </p>

          {!isAuthenticated ? (
            <div className="mt-6 rounded-2xl bg-slate-100 p-5 dark:bg-slate-800">
              <p className="font-bold">Hay dang nhap de tao tour ca nhan.</p>
              <p className="mt-2 text-sm text-slate-500">Sau khi dang nhap, tour ban tao se chi hien thi trong danh sach "Tour cua toi".</p>
              <Link className="btn-primary mt-4 inline-flex" to={`/login?next=${encodeURIComponent('/tours')}`}>
                Dang nhap de tao tour
              </Link>
            </div>
          ) : (
            <form className="mt-6 grid gap-4" onSubmit={submitMyTour}>
              <Field label="Ten tour">
                <TextInput
                  value={form.title}
                  onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))}
                  required
                />
              </Field>

              <Field label="Mo ta tour">
                <TextArea
                  value={form.description}
                  onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
                  required
                />
              </Field>

              <div className="grid gap-3 md:grid-cols-[1.2fr_.9fr_.7fr_auto] md:items-end">
                <Field label="Chon dia diem">
                  <select
                    value={form.selectedPoiId}
                    onChange={(event) => setForm((current) => ({ ...current, selectedPoiId: event.target.value }))}
                    className="rounded-2xl border border-slate-200 bg-white px-4 py-3 outline-none transition focus:border-coral dark:border-slate-700 dark:bg-slate-900"
                  >
                    <option value="">Chon POI cong khai</option>
                    {availablePois.map((poi) => (
                      <option key={poi.id} value={poi.id}>
                        {poi.name}
                      </option>
                    ))}
                  </select>
                </Field>

                <Field label="Ten hien thi">
                  <TextInput
                    value={form.stopTitle}
                    onChange={(event) => setForm((current) => ({ ...current, stopTitle: event.target.value }))}
                    placeholder="De trong de dung ten POI"
                  />
                </Field>

                <Field label="So phut dung">
                  <TextInput
                    type="number"
                    min={1}
                    max={1440}
                    value={form.estimatedStayMinutes}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        estimatedStayMinutes: Number.parseInt(event.target.value || '20', 10) || 20,
                      }))
                    }
                  />
                </Field>

                <button type="button" className="btn-secondary h-[50px] justify-center" onClick={addStop}>
                  <Plus size={18} />
                  Them diem
                </button>
              </div>

              <div className="rounded-2xl bg-slate-50 p-4 dark:bg-slate-800">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="font-bold">Dia diem da chon</p>
                  <span className="pill">{totalDurationMinutes || 0} phut</span>
                </div>

                {draftStops.length ? (
                  <div className="mt-4 space-y-3">
                    {draftStops.map((stop, index) => {
                      const poi = poiLookup[stop.poiId];
                      return (
                        <div key={`${stop.poiId}-${index}`} className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900">
                          <div className="flex flex-wrap items-start justify-between gap-3">
                            <div>
                              <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">Diem dung {index + 1}</p>
                              <p className="mt-1 font-bold">{stop.title || poi?.name || stop.poiId}</p>
                              <p className="mt-1 text-sm text-slate-500">{poi?.address || 'POI khong con san trong danh sach cong khai.'}</p>
                            </div>
                            <div className="flex flex-wrap gap-2">
                              <button type="button" className="pill" onClick={() => moveStop(index, -1)} disabled={index === 0}>
                                Len
                              </button>
                              <button
                                type="button"
                                className="pill"
                                onClick={() => moveStop(index, 1)}
                                disabled={index === draftStops.length - 1}
                              >
                                Xuong
                              </button>
                              <button type="button" className="pill border-rose-300 text-rose-600" onClick={() => removeStop(index)}>
                                <Trash2 size={14} className="mr-1 inline" />
                                Xoa
                              </button>
                            </div>
                          </div>

                          <div className="mt-3 grid gap-3 md:grid-cols-2">
                            <Field label="Ten hien thi tren tour">
                              <TextInput
                                value={stop.title}
                                onChange={(event) => updateStop(index, { title: event.target.value })}
                                placeholder={poi?.name || stop.poiId}
                              />
                            </Field>
                            <Field label="So phut dung">
                              <TextInput
                                type="number"
                                min={1}
                                max={1440}
                                value={stop.estimatedStayMinutes}
                                onChange={(event) =>
                                  updateStop(index, {
                                    estimatedStayMinutes: Number.parseInt(event.target.value || '15', 10) || 15,
                                  })
                                }
                              />
                            </Field>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                ) : (
                  <p className="mt-3 text-sm text-slate-500">Chua co diem dung nao. Hay them POI cong khai vao tour cua ban.</p>
                )}
              </div>

              {formError ? (
                <div className="rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700">
                  {formError}
                </div>
              ) : null}

              {createTourMutation.error ? (
                <div className="rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700">
                  {(createTourMutation.error as Error).message}
                </div>
              ) : null}

              <button className="btn-primary justify-center" disabled={createTourMutation.isPending}>
                {createTourMutation.isPending ? 'Dang tao tour...' : 'Tao tour cua toi'}
              </button>
            </form>
          )}
        </div>

        <div className="rounded-[2rem] bg-white p-6 shadow-soft dark:bg-slate-900">
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-coral">Danh sach rieng</p>
          <h2 className="mt-2 text-2xl font-bold">Chi minh ban nhin thay</h2>
          <p className="mt-2 text-sm text-slate-500">
            Nhan "Xem lo trinh" de mo map va hien toan bo cac dia diem ban da chon cho tour do.
          </p>

          {!isAuthenticated ? (
            <div className="mt-6 rounded-2xl bg-slate-100 p-5 dark:bg-slate-800">
              <p className="font-bold">Danh sach nay chi hien sau khi dang nhap.</p>
            </div>
          ) : myToursQuery.isLoading ? (
            <div className="mt-6">
              <Spinner />
            </div>
          ) : myToursQuery.isError ? (
            <div className="mt-6">
              <ErrorBox text={(myToursQuery.error as Error).message} />
            </div>
          ) : myToursQuery.data?.length ? (
            <div className="mt-6 grid gap-6">
              {myToursQuery.data.map((tour) => (
                <TourCard key={tour.id} tour={tour} poiLookup={poiLookup} accentLabel="Tour cua toi" />
              ))}
            </div>
          ) : (
            <div className="mt-6 rounded-2xl bg-slate-100 p-5 dark:bg-slate-800">
              <p className="font-bold">Chua co tour ca nhan nao.</p>
              <p className="mt-2 text-sm text-slate-500">Tao tour o khung ben trai, tour do se chi hien cho chinh tai khoan nay.</p>
            </div>
          )}
        </div>
      </div>

      <div className="mt-10">
        <p className="section-kicker">TOUR CONG KHAI</p>
        <h2 className="mt-2 text-3xl font-bold">Lich trinh am thuc cong khai</h2>
        <p className="mt-2 max-w-2xl text-slate-500">
          Cac tour nay den tu endpoint cong khai hien co. Ban van co the nhan xem lo trinh tren map de thay cac diem dung.
        </p>

        {toursQuery.isLoading || poisQuery.isLoading ? (
          <Spinner />
        ) : toursQuery.isError ? (
          <ErrorBox text={(toursQuery.error as Error).message} />
        ) : toursQuery.data?.length ? (
          <div className="mt-8 grid gap-6 lg:grid-cols-2">
            {toursQuery.data.map((tour) => (
              <TourCard key={tour.id} tour={tour} poiLookup={poiLookup} accentLabel="Tour cong khai" />
            ))}
          </div>
        ) : (
          <div className="mt-8 rounded-3xl bg-slate-100 p-12 text-center dark:bg-slate-900">
            <RouteIcon className="mx-auto text-teal" />
            <h2 className="mt-3 text-xl font-bold">Chua co tour cong khai</h2>
            <p className="mt-1 text-slate-500">Admin co the tao tour moi trong trang quan tri.</p>
          </div>
        )}
      </div>
    </section>
  );
}
