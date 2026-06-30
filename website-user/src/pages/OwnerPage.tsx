import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, CheckCircle2, LoaderCircle, MapPin, Navigation } from 'lucide-react';
import { Link } from 'react-router-dom';
import { categoryApi } from '../api/categoryApi';
import { ownerApi } from '../api/ownerApi';
import { ErrorBox } from '../components/common/ErrorBox';
import { Field, TextArea, TextInput } from '../components/common/FormControls';
import { OwnerPoiStudio } from '../components/owner/OwnerPoiStudio';
import { Spinner } from '../components/common/Spinner';
import { StatusPill } from '../components/common/StatusPill';
import { useAppStore } from '../store/appStore';
import type { CreateOwnerSubmissionRequest } from '../types/requests';
import type { OpeningHour, OwnerManagedPoi, OwnerSubmissionResponse, PoiImage } from '../types/responses';
import { normalizeMediaUrl, poiImage } from '../utils/media';

function createEmptyOpeningHour(): OpeningHour {
  return {
    dayOfWeek: '',
    openTime: '',
    closeTime: '',
    isClosed: false,
  };
}

function OwnerPoiCard({
  poi,
  onCreateUpdate,
  onManageAudio,
}: {
  poi: OwnerManagedPoi;
  onCreateUpdate: () => void;
  onManageAudio: () => void;
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
          <span className="pill">Âm thanh: {poi.audioStatus}</span>
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
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-amber-600">Người truy cập</p>
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
          <p>Giá: {poi.priceRange} · Ưu tiên: {poi.priority}</p>
          <p>Cập nhật: {new Date(poi.updatedAt).toLocaleString()}</p>
        </div>

        <div className="mt-5 flex flex-wrap gap-3">
          <Link className="btn-secondary !px-4 !py-2" to={`/poi/${poi.id}`}>
            Xem chi tiết
          </Link>
          <a className="btn-secondary !px-4 !py-2" href={mapUrl} target="_blank" rel="noreferrer">
            <Navigation size={16} />
            Chỉ đường
          </a>
          <button type="button" className="btn-secondary !px-4 !py-2" onClick={onManageAudio}>
            Quan ly audio va ban dich
          </button>
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
        <p>Mức ưu tiên: {submission.priority}</p>
        <p>Bán kính geofence: {submission.geofenceRadiusMeters} m</p>
        <p>Tự động thuyết minh: {submission.autoNarrationEnabled ? 'bật' : 'tắt'}</p>
        <p>Tạo lúc: {new Date(submission.createdAt).toLocaleString()}</p>
        {submission.adminNote ? <p>Ghi chú từ admin: {submission.adminNote}</p> : null}
      </div>

      {submission.status === 'pending' ? (
        <button onClick={onEdit} className="mt-4 text-sm font-bold text-coral">
          Sửa đề xuất đang chờ duyệt
        </button>
      ) : null}
    </article>
  );
}

function mapSubmissionToForm(submission: OwnerSubmissionResponse): CreateOwnerSubmissionRequest {
  return {
    submissionType: submission.submissionType,
    poiId: submission.poiId || '',
    poiName: submission.poiName,
    description: submission.description,
    categoryId: submission.categoryId,
    location: {
      latitude: submission.latitude,
      longitude: submission.longitude,
    },
    address: submission.address,
    ward: submission.ward,
    district: submission.district,
    city: submission.city,
    priceRange:
      submission.priceRange === '$' || submission.priceRange === '$$' || submission.priceRange === '$$$'
        ? submission.priceRange
        : '$$',
    priority: submission.priority,
    mapUrl: submission.mapUrl || '',
    ttsScript: submission.ttsScript || '',
    geofenceRadiusMeters: submission.geofenceRadiusMeters,
    autoNarrationEnabled: submission.autoNarrationEnabled,
    images: submission.images || [],
    openingHours: submission.openingHours || [],
    contactInfo: submission.contactInfo || null,
    tags: submission.tags || [],
  };
}

function mapPoiToForm(poi: OwnerManagedPoi): CreateOwnerSubmissionRequest {
  return {
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
  };
}

function normalizeSubmissionImages(images: PoiImage[]): PoiImage[] {
  if (!images.length) {
    return [];
  }

  const thumbnailIndex = images.findIndex((image) => image.isThumbnail);
  const resolvedThumbnailIndex = thumbnailIndex >= 0 ? thumbnailIndex : 0;

  return images.map((image, index) => ({
    ...image,
    caption: image.caption || '',
    isThumbnail: index === resolvedThumbnailIndex,
  }));
}

function appendSubmissionImage(images: PoiImage[], image: PoiImage): PoiImage[] {
  return normalizeSubmissionImages([...images, image]);
}

export default function OwnerPage() {
  const queryClient = useQueryClient();
  const { lang } = useAppStore();
  const submissionFormRef = useRef<HTMLDivElement>(null);
  const studioRef = useRef<HTMLDivElement>(null);
  const submissionImageInputRef = useRef<HTMLInputElement>(null);
  const [editingSubmissionId, setEditingSubmissionId] = useState<string | null>(null);
  const [managedPoiId, setManagedPoiId] = useState<string | null>(null);
  const [manualImageUrl, setManualImageUrl] = useState('');
  const [manualImageCaption, setManualImageCaption] = useState('');

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
    district: 'Quận 4',
    city: 'TP. Hồ Chí Minh',
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
  const [submissionNotice, setSubmissionNotice] = useState('');

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
      setSubmissionNotice('');
    },
  });
  const uploadSubmissionImageMutation = useMutation({
    mutationFn: ownerApi.uploadImage,
    onSuccess: (file) => {
      setSubmissionForm((current) => ({
        ...current,
        images: appendSubmissionImage(current.images, {
          url: file.url,
          caption: manualImageCaption.trim() || file.originalFileName,
          isThumbnail: current.images.length === 0,
        }),
      }));
      setManualImageUrl('');
      setManualImageCaption('');
      setSubmissionNotice('');
    },
  });

  const ownerPois = ownerPoisQuery.data || [];
  const submissions = submissionsQuery.data || [];
  const isUpdateSubmission = submissionForm.submissionType === 'update';
  const canSubmitSubmission =
    !saveSubmissionMutation.isPending &&
    (!isUpdateSubmission || (ownerPois.length > 0 && Boolean(submissionForm.poiId)));

  const resetSubmissionEditor = () => {
    setEditingSubmissionId(null);
    setSubmissionForm(createEmptySubmissionForm());
    setSubmissionNotice('');
    setManualImageUrl('');
    setManualImageCaption('');
  };

  const beginUpdateSubmissionFromPoi = (poi: OwnerManagedPoi) => {
    setEditingSubmissionId(null);
    setSubmissionForm(mapPoiToForm(poi));
    setSubmissionNotice('');
    submissionFormRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const handleSubmissionTypeChange = (value: string) => {
    const nextSubmissionType = value === 'update' ? 'update' : 'create';
    setSubmissionNotice('');
    setSubmissionForm((current) => ({
      ...current,
      submissionType: nextSubmissionType,
      poiId: nextSubmissionType === 'update' && ownerPois.some((poi) => poi.id === current.poiId) ? current.poiId : '',
    }));
  };

  const handleUpdatePoiChange = (poiId: string) => {
    setSubmissionNotice('');
    if (!poiId) {
      setSubmissionForm((current) => ({ ...current, poiId: '' }));
      return;
    }

    const poi = ownerPois.find((item) => item.id === poiId);
    if (!poi) {
      return;
    }

    setSubmissionForm(mapPoiToForm(poi));
  };

  const addManualSubmissionImage = () => {
    const trimmedUrl = manualImageUrl.trim();
    if (!trimmedUrl) {
      setSubmissionNotice('Vui long nhap URL anh truoc khi them.');
      return;
    }

    setSubmissionNotice('');
    setSubmissionForm((current) => ({
      ...current,
      images: appendSubmissionImage(current.images, {
        url: trimmedUrl,
        caption: manualImageCaption.trim(),
        isThumbnail: current.images.length === 0,
      }),
    }));
    setManualImageUrl('');
    setManualImageCaption('');
  };

  const addOpeningHourRow = () => {
    setSubmissionNotice('');
    setSubmissionForm((current) => ({
      ...current,
      openingHours: [...current.openingHours, createEmptyOpeningHour()],
    }));
  };

  const updateOpeningHourRow = (index: number, patch: Partial<OpeningHour>) => {
    setSubmissionNotice('');
    setSubmissionForm((current) => ({
      ...current,
      openingHours: current.openingHours.map((item, itemIndex) =>
        itemIndex === index ? { ...item, ...patch } : item,
      ),
    }));
  };

  const removeOpeningHourRow = (index: number) => {
    setSubmissionNotice('');
    setSubmissionForm((current) => ({
      ...current,
      openingHours: current.openingHours.filter((_, itemIndex) => itemIndex !== index),
    }));
  };

  return (
    <section className="shell py-12">
      <p className="section-kicker">KHÔNG GIAN ĐỐI TÁC</p>
      <h1 className="mt-2 text-4xl font-bold">Quản lý địa điểm và đề xuất</h1>
      <p className="mt-2 max-w-3xl text-slate-500">
        Trang đối tác này có bảng tổng quan, danh sách địa điểm của chính bạn, lượt vào trang, số người đã nghe âm
        thanh, lượt quét QR và khu gửi yêu cầu cập nhật nội dung.
      </p>

      <div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {dashboardQuery.isLoading ? (
          <Spinner />
        ) : dashboardQuery.isError ? (
          <ErrorBox text={(dashboardQuery.error as Error).message} />
        ) : (
          [
            ['Tổng POI', dashboardQuery.data?.totalPois || 0],
            ['Tổng đề xuất', dashboardQuery.data?.totalSubmissions || 0],
            ['Lượt vào trang', dashboardQuery.data?.totalViews || 0],
            ['Người truy cập', dashboardQuery.data?.uniqueVisitors || 0],
            ['Lượt nghe âm thanh', dashboardQuery.data?.totalAudioPlays || 0],
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
              Dữ liệu lấy từ `GET /api/v1/owner/pois`, gồm thông tin POI, lượt vào trang, người nghe âm thanh và số
              lần quét QR theo từng địa điểm.
            </p>
          </div>
          <button
            className="btn-secondary"
            onClick={() => {
              resetSubmissionEditor();
              submissionFormRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }}
          >
            Tạo đề xuất mới
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
              <OwnerPoiCard
                key={poi.id}
                poi={poi}
                onCreateUpdate={() => beginUpdateSubmissionFromPoi(poi)}
                onManageAudio={() => {
                  setManagedPoiId(poi.id);
                  studioRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }}
              />
            ))}
          </div>
        ) : (
          <div className="mt-6 rounded-3xl bg-slate-100 p-8 text-center dark:bg-slate-800">
            <p className="font-bold">Chưa có địa điểm nào được gán cho đối tác này</p>
            <p className="mt-1 text-sm text-slate-500">
              Bạn vẫn có thể tạo đề xuất mới ở phần bên dưới để đề xuất địa điểm mới hoặc yêu cầu cập nhật.
            </p>
          </div>
        )}
      </div>

      {ownerPois.length ? (
        <div ref={studioRef}>
          <OwnerPoiStudio pois={ownerPois} selectedPoiId={managedPoiId} />
        </div>
      ) : null}

      <div className="mt-8 grid gap-6 lg:grid-cols-[1.05fr_.95fr]">
        <div ref={submissionFormRef} className="rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
          <h2 className="text-2xl font-bold">{editingSubmissionId ? 'Cập nhật đề xuất' : 'Tạo đề xuất mới'}</h2>
          <p className="mt-2 text-sm text-slate-500">
            Bạn có thể tạo đề xuất mới, sửa đề xuất đang chờ duyệt, hoặc từ một địa điểm đang quản lý tạo nhanh đề
            xuất `update` đã điền sẵn dữ liệu hiện tại.
          </p>

          {editingSubmissionId ? (
            <div className="mt-4 flex flex-wrap items-center gap-3 rounded-2xl bg-amber-50 px-4 py-3 text-sm text-amber-800">
              <span>Đang sửa đề xuất {editingSubmissionId}</span>
              <button
                type="button"
                className="font-bold text-coral"
                onClick={resetSubmissionEditor}
              >
                Hủy chế độ sửa
              </button>
            </div>
          ) : null}

          <form
            className="mt-6 grid gap-4 md:grid-cols-2"
            onSubmit={(event) => {
              event.preventDefault();
              if (isUpdateSubmission) {
                if (ownerPoisQuery.isError) {
                  setSubmissionNotice((ownerPoisQuery.error as Error).message);
                  return;
                }

                if (!ownerPois.length) {
                  setSubmissionNotice('Bạn chưa có địa điểm nào để cập nhật.');
                  return;
                }

                if (!submissionForm.poiId) {
                  setSubmissionNotice('Vui lòng chọn địa điểm của bạn để gửi cập nhật.');
                  return;
                }
              }

              const normalizedOpeningHours = submissionForm.openingHours
                .map((item) => ({
                  dayOfWeek: item.dayOfWeek.trim(),
                  openTime: item.isClosed ? '' : item.openTime.trim(),
                  closeTime: item.isClosed ? '' : item.closeTime.trim(),
                  isClosed: item.isClosed,
                }))
                .filter((item) => item.dayOfWeek || item.openTime || item.closeTime || item.isClosed);

              const hasInvalidOpeningHours = normalizedOpeningHours.some(
                (item) => !item.dayOfWeek || (!item.isClosed && (!item.openTime || !item.closeTime)),
              );
              if (hasInvalidOpeningHours) {
                setSubmissionNotice('Vui long nhap day du ngay va gio mo/dong cua, hoac danh dau dang nghi.');
                return;
              }

              setSubmissionNotice('');
              saveSubmissionMutation.mutate({
                ...submissionForm,
                openingHours: normalizedOpeningHours,
                poiId: isUpdateSubmission ? submissionForm.poiId || undefined : undefined,
              });
            }}
          >
            <Field label="Loại đề xuất">
              <select
                value={submissionForm.submissionType}
                onChange={(event) => handleSubmissionTypeChange(event.target.value)}
                className="rounded-2xl border border-slate-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900"
              >
                <option value="create">Tạo mới</option>
                <option value="update">Cập nhật</option>
              </select>
            </Field>

            {isUpdateSubmission ? (
              <div className="md:col-span-2">
                <Field label="Địa điểm của tôi">
                  <select
                    value={submissionForm.poiId || ''}
                    onChange={(event) => handleUpdatePoiChange(event.target.value)}
                    className="rounded-2xl border border-slate-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900"
                  >
                    <option value="">Chọn địa điểm của bạn</option>
                    {ownerPois.map((poi) => (
                      <option key={poi.id} value={poi.id}>
                        {poi.name} - {poi.address}
                      </option>
                    ))}
                  </select>
                </Field>
                {ownerPoisQuery.isLoading ? (
                  <p className="mt-2 text-sm text-slate-500">Đang tải danh sách địa điểm của bạn...</p>
                ) : null}
                {ownerPoisQuery.isError ? (
                  <p className="mt-2 text-sm text-rose-700">{(ownerPoisQuery.error as Error).message}</p>
                ) : null}
                {!ownerPoisQuery.isLoading && !ownerPoisQuery.isError && ownerPois.length === 0 ? (
                  <p className="mt-2 text-sm text-amber-700">Bạn chưa có địa điểm nào để cập nhật.</p>
                ) : null}
              </div>
            ) : null}

            <Field label="Tên địa điểm">
              <TextInput
                value={submissionForm.poiName}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, poiName: event.target.value }))}
                required
              />
            </Field>
            <Field label="Danh mục">
              <select
                value={submissionForm.categoryId}
                onChange={(event) =>
                  setSubmissionForm((current) => ({ ...current, categoryId: event.target.value }))
                }
                required
                className="rounded-2xl border border-slate-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900"
              >
                <option value="">Chọn danh mục</option>
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
            <Field label="Phường">
              <TextInput
                value={submissionForm.ward}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, ward: event.target.value }))}
                required
              />
            </Field>
            <Field label="Quận">
              <TextInput
                value={submissionForm.district}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, district: event.target.value }))}
                required
              />
            </Field>
            <Field label="Thành phố">
              <TextInput
                value={submissionForm.city}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, city: event.target.value }))}
                required
              />
            </Field>
            <Field label="Khoảng giá">
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
            <Field label="Mức ưu tiên">
              <TextInput
                type="number"
                min={0}
                value={submissionForm.priority}
                onChange={(event) =>
                  setSubmissionForm((current) => ({ ...current, priority: Number(event.target.value) }))
                }
              />
            </Field>
            <Field label="Bán kính geofence">
              <TextInput
                type="number"
                min={20}
                max={1000}
                value={submissionForm.geofenceRadiusMeters}
                onChange={(event) =>
                  setSubmissionForm((current) => ({
                    ...current,
                    geofenceRadiusMeters: Number(event.target.value),
                  }))
                }
              />
            </Field>
            <Field label="Liên kết bản đồ">
              <TextInput
                value={submissionForm.mapUrl}
                onChange={(event) => setSubmissionForm((current) => ({ ...current, mapUrl: event.target.value }))}
              />
            </Field>
            <Field label="Thẻ (CSV)">
              <TextInput
                placeholder="Vi du: hai san, mon nuong, mo khuya"
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
              <span className="text-xs font-normal text-slate-500">
                Nhap moi the cach nhau bang dau phay. Vi du: hai san, check-in, quan 4
              </span>
            </Field>
            <Field label="Mô tả">
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
            <div className="rounded-3xl border border-slate-200 p-5 md:col-span-2 dark:border-slate-700">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="font-bold">Gio mo cua</p>
                  <p className="mt-1 text-sm text-slate-500">
                    Moi dong co the la mot ngay cu the hoac mot khoang ngay, vi du: Thu Hai - Chu Nhat.
                  </p>
                </div>
                <button type="button" className="btn-secondary !px-4 !py-2" onClick={addOpeningHourRow}>
                  Them khung gio
                </button>
              </div>

              {submissionForm.openingHours.length ? (
                <div className="mt-4 space-y-3">
                  {submissionForm.openingHours.map((item, index) => (
                    <div
                      key={`${item.dayOfWeek || 'opening-hour'}-${index}`}
                      className="rounded-2xl border border-slate-200 p-4 dark:border-slate-700"
                    >
                      <div className="grid gap-3 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,.8fr)_minmax(0,.8fr)_auto_auto]">
                        <TextInput
                          value={item.dayOfWeek}
                          onChange={(event) => updateOpeningHourRow(index, { dayOfWeek: event.target.value })}
                          placeholder="Ngay hoac khoang ngay"
                        />
                        <TextInput
                          type="time"
                          value={item.openTime}
                          onChange={(event) => updateOpeningHourRow(index, { openTime: event.target.value })}
                          disabled={item.isClosed}
                        />
                        <TextInput
                          type="time"
                          value={item.closeTime}
                          onChange={(event) => updateOpeningHourRow(index, { closeTime: event.target.value })}
                          disabled={item.isClosed}
                        />
                        <label className="flex items-center gap-2 rounded-2xl border border-slate-200 px-4 py-3 text-sm font-medium dark:border-slate-700">
                          <input
                            type="checkbox"
                            checked={item.isClosed}
                            onChange={(event) =>
                              updateOpeningHourRow(index, {
                                isClosed: event.target.checked,
                                openTime: event.target.checked ? '' : item.openTime,
                                closeTime: event.target.checked ? '' : item.closeTime,
                              })
                            }
                          />
                          <span>Nghi</span>
                        </label>
                        <button
                          type="button"
                          className="btn-secondary !px-4 !py-2"
                          onClick={() => removeOpeningHourRow(index)}
                        >
                          Xoa
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="mt-4 text-sm text-slate-500">Chua khai bao gio mo cua cho de xuat nay.</p>
              )}
            </div>
            <div className="rounded-3xl border border-slate-200 p-5 md:col-span-2 dark:border-slate-700">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="font-bold">Anh de xuat</p>
                  <p className="mt-1 text-sm text-slate-500">
                    Tai file anh len hoac nhap URL anh de bo sung vao payload submission.
                  </p>
                </div>
                <div className="flex flex-wrap gap-3">
                  <input
                    ref={submissionImageInputRef}
                    type="file"
                    accept="image/*"
                    className="hidden"
                    onChange={(event) => {
                      const file = event.target.files?.[0];
                      if (!file) {
                        return;
                      }

                      setSubmissionNotice('');
                      uploadSubmissionImageMutation.mutate(file);
                      event.target.value = '';
                    }}
                  />
                  <button
                    type="button"
                    className="btn-secondary !px-4 !py-2"
                    onClick={() => submissionImageInputRef.current?.click()}
                    disabled={uploadSubmissionImageMutation.isPending}
                  >
                    {uploadSubmissionImageMutation.isPending ? 'Dang tai anh...' : 'Tai anh len'}
                  </button>
                </div>
              </div>

              <div className="mt-4 grid gap-3 md:grid-cols-[1.2fr_.8fr_auto]">
                <TextInput
                  value={manualImageUrl}
                  onChange={(event) => setManualImageUrl(event.target.value)}
                  placeholder="Dan URL anh neu khong muon upload file"
                />
                <TextInput
                  value={manualImageCaption}
                  onChange={(event) => setManualImageCaption(event.target.value)}
                  placeholder="Caption ngan"
                />
                <button type="button" className="btn-secondary !px-4 !py-2" onClick={addManualSubmissionImage}>
                  Them URL anh
                </button>
              </div>

              {uploadSubmissionImageMutation.error ? (
                <div className="mt-4 rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700">
                  {(uploadSubmissionImageMutation.error as Error).message}
                </div>
              ) : null}

              {submissionForm.images.length ? (
                <div className="mt-4 grid gap-4 sm:grid-cols-2">
                  {submissionForm.images.map((image, index) => (
                    <div key={`${image.url}-${index}`} className="rounded-2xl border border-slate-200 p-3 dark:border-slate-700">
                      <img
                        src={normalizeMediaUrl(image.url)}
                        alt={image.caption || `Submission image ${index + 1}`}
                        className="h-40 w-full rounded-2xl object-cover"
                      />
                      <div className="mt-3 grid gap-3">
                        <TextInput
                          value={image.caption || ''}
                          onChange={(event) =>
                            setSubmissionForm((current) => ({
                              ...current,
                              images: normalizeSubmissionImages(
                                current.images.map((item, itemIndex) =>
                                  itemIndex === index ? { ...item, caption: event.target.value } : item,
                                ),
                              ),
                            }))
                          }
                          placeholder="Caption"
                        />
                        <p className="break-all text-xs text-slate-500">{image.url}</p>
                        <div className="flex flex-wrap gap-3">
                          <button
                            type="button"
                            className="btn-secondary !px-4 !py-2"
                            onClick={() =>
                              setSubmissionForm((current) => ({
                                ...current,
                                images: normalizeSubmissionImages(
                                  current.images.map((item, itemIndex) => ({
                                    ...item,
                                    isThumbnail: itemIndex === index,
                                  })),
                                ),
                              }))
                            }
                          >
                            {image.isThumbnail ? 'Anh chinh' : 'Dat lam anh chinh'}
                          </button>
                          <button
                            type="button"
                            className="btn-secondary !px-4 !py-2"
                            onClick={() =>
                              setSubmissionForm((current) => ({
                                ...current,
                                images: normalizeSubmissionImages(
                                  current.images.filter((_, itemIndex) => itemIndex !== index),
                                ),
                              }))
                            }
                          >
                            Xoa anh
                          </button>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="mt-4 text-sm text-slate-500">Chua co anh nao trong submission nay.</p>
              )}
            </div>
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
              <span>Bật tự động thuyết minh</span>
            </label>

            {submissionNotice ? (
              <div className="rounded-2xl bg-amber-50 px-4 py-3 text-sm text-amber-800 md:col-span-2">
                {submissionNotice}
              </div>
            ) : null}

            {saveSubmissionMutation.error ? (
              <div className="rounded-2xl bg-rose-50 px-4 py-3 text-sm text-rose-700 md:col-span-2">
                {(saveSubmissionMutation.error as Error).message}
              </div>
            ) : null}

            <button className="btn-primary justify-center md:col-span-2" disabled={!canSubmitSubmission}>
              {saveSubmissionMutation.isPending ? (
                <LoaderCircle className="animate-spin" size={18} />
              ) : (
                <CheckCircle2 size={18} />
              )}
              {editingSubmissionId ? 'Cập nhật đề xuất của đối tác' : 'Tạo đề xuất của đối tác'}
            </button>
          </form>
        </div>

        <div className="rounded-[2rem] bg-white p-8 shadow-soft dark:bg-slate-900">
          <h2 className="text-2xl font-bold">Danh sách đề xuất</h2>
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
                    setSubmissionForm(mapSubmissionToForm(submission));
                    setSubmissionNotice('');
                    submissionFormRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                  }}
                />
              ))}
            </div>
          ) : (
            <div className="mt-6 rounded-3xl bg-slate-100 p-8 text-center dark:bg-slate-800">
              <p className="font-bold">Chưa có đề xuất nào</p>
              <p className="mt-1 text-sm text-slate-500">Gửi đề xuất đầu tiên của bạn bằng biểu mẫu bên trái.</p>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
