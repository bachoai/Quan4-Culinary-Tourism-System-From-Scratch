import axiosClient from './axiosClient';
import type { GeneratePoiAudioRequest, UploadPoiAudioRequest } from '../types/requests';
import type { AudioLanguageResponse, AudioPackManifestResponse, PoiAudioResponse } from '../types/responses';

export const audioApi = {
  getLanguages: () => axiosClient.get<never, AudioLanguageResponse[]>('/api/v1/audio/languages'),
  getPoiAudio: (id: string, lang?: string) => axiosClient.get<never, PoiAudioResponse | null>(`/api/v1/poi/${id}/audio`, { params: { lang } }),
  uploadPoiAudio: (id: string, payload: UploadPoiAudioRequest, file?: File) => {
    const formData = new FormData();
    formData.append('lang', payload.lang);
    if (payload.audioUrl) formData.append('audioUrl', payload.audioUrl);
    if (payload.voiceName) formData.append('voiceName', payload.voiceName);
    formData.append('sourceType', payload.sourceType ?? 'uploaded');
    if (file) formData.append('file', file);
    return axiosClient.post<never, PoiAudioResponse>(`/api/v1/admin/pois/${id}/audio`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
  generatePoiAudio: (id: string, payload: GeneratePoiAudioRequest) =>
    axiosClient.post<never, PoiAudioResponse>(`/api/v1/admin/pois/${id}/audio/generate`, payload),
  deletePoiAudio: (id: string, lang: string) =>
    axiosClient.delete<never, null>(`/api/v1/admin/pois/${id}/audio`, { params: { lang } }),
  getPackManifest: () => axiosClient.get<never, AudioPackManifestResponse>('/api/v1/audio/pack-manifest'),
};
