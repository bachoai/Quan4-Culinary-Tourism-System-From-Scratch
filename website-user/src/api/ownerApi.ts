import { deleteData, getData, postData, postFormData, putData } from './axiosClient';
import type {
  MediaFileResponse,
  OwnerDashboardResponse,
  OwnerManagedPoi,
  OwnerSubmissionResponse,
  PoiAudio,
  PoiLocalizationResponse,
} from '../types/responses';
import type {
  CreateOwnerSubmissionRequest,
  CreatePoiLocalizationRequest,
  GeneratePoiAudioRequest,
  TranslatePoiLocalizationRequest,
  UploadPoiAudioRequest,
} from '../types/requests';

export const ownerApi = {
  dashboard: () => getData<OwnerDashboardResponse>('/api/v1/owner/dashboard'),
  pois: (lang?: string) => getData<OwnerManagedPoi[]>('/api/v1/owner/pois', lang ? { lang } : undefined),
  poiLocalizations: (poiId: string) => getData<PoiLocalizationResponse[]>(`/api/v1/owner/pois/${poiId}/localizations`),
  savePoiLocalization: (poiId: string, lang: string, payload: CreatePoiLocalizationRequest) =>
    putData<PoiLocalizationResponse>(`/api/v1/owner/pois/${poiId}/localizations/${lang}`, payload),
  translatePoiLocalization: (poiId: string, payload: TranslatePoiLocalizationRequest) =>
    postData<PoiLocalizationResponse>(`/api/v1/owner/pois/${poiId}/localizations/translate`, payload),
  uploadPoiAudio: (poiId: string, payload: UploadPoiAudioRequest, file?: File) => {
    const formData = new FormData();
    formData.append('lang', payload.lang);
    if (payload.audioUrl) formData.append('audioUrl', payload.audioUrl);
    if (payload.voiceName) formData.append('voiceName', payload.voiceName);
    formData.append('sourceType', payload.sourceType ?? 'uploaded');
    if (file) formData.append('file', file);
    return postFormData<PoiAudio>(`/api/v1/owner/pois/${poiId}/audio`, formData);
  },
  generatePoiAudio: (poiId: string, payload: GeneratePoiAudioRequest) =>
    postData<PoiAudio>(`/api/v1/owner/pois/${poiId}/audio/generate`, payload),
  deletePoiAudio: (poiId: string, lang: string) =>
    deleteData<null>(`/api/v1/owner/pois/${poiId}/audio?lang=${encodeURIComponent(lang)}`),
  uploadImage: (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return postFormData<MediaFileResponse>('/api/v1/owner/media/upload-image', formData);
  },
  submissions: () => getData<OwnerSubmissionResponse[]>('/api/v1/owner/submissions'),
  submissionById: (id: string) => getData<OwnerSubmissionResponse>(`/api/v1/owner/submissions/${id}`),
  createSubmission: (payload: CreateOwnerSubmissionRequest) =>
    postData<OwnerSubmissionResponse>('/api/v1/owner/submissions', payload),
  updateSubmission: (id: string, payload: CreateOwnerSubmissionRequest) =>
    putData<OwnerSubmissionResponse>(`/api/v1/owner/submissions/${id}`, payload),
};
