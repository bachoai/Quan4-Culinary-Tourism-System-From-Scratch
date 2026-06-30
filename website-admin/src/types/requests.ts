import type { ContactInfo, CoordinateRequest, OpeningHour, PoiImage } from './common';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface CreateCategoryRequest {
  code: string;
  name: string;
  description?: string;
  iconUrl?: string;
  sortOrder: number;
}

export interface UpdateCategoryRequest extends CreateCategoryRequest {
  isActive: boolean;
}

export interface PoiPayloadBase {
  name: string;
  description: string;
  categoryId: string;
  location: CoordinateRequest;
  address: string;
  ward: string;
  district: string;
  city: string;
  priceRange: '$' | '$$' | '$$$';
  priority: number;
  mapUrl?: string;
  ttsScript?: string;
  geofenceRadiusMeters: number;
  autoNarrationEnabled: boolean;
  images: PoiImage[];
  openingHours: OpeningHour[];
  contactInfo?: ContactInfo | null;
  ownerId?: string | null;
  tags: string[];
  isActive: boolean;
  autoTranslateAudioContent: boolean;
  overwriteAutoTranslations: boolean;
  autoTranslateLanguages: string[];
}

export interface CreatePoiRequest extends PoiPayloadBase {}

export interface UpdatePoiRequest extends PoiPayloadBase {
  activationRequested: boolean;
}

export interface CreateLocalizationRequest {
  lang: string;
  name: string;
  description: string;
  audioUrl?: string;
  ttsScript?: string;
  isFallback: boolean;
}

export interface UpdateLocalizationRequest extends CreateLocalizationRequest {}

export interface TranslatePoiLocalizationRequest {
  lang: string;
  sourceLang: string;
  overwriteExisting: boolean;
}

export interface UploadPoiAudioRequest {
  lang: string;
  audioUrl?: string;
  voiceName?: string;
  sourceType: string;
}

export interface GeneratePoiAudioRequest {
  lang: string;
  voiceName?: string;
}

export interface TourStopRequest {
  poiId: string;
  title?: string;
  order: number;
  estimatedStayMinutes: number;
}

export interface CreateTourRequest {
  title: string;
  description: string;
  lang: string;
  coverImageUrl?: string;
  estimatedDurationMinutes: number;
  isActive: boolean;
  stops: TourStopRequest[];
}

export interface UpdateTourRequest extends CreateTourRequest {}

export interface CreateQrActivationRequest {
  code: string;
  poiId: string;
  title: string;
  stopZone: string;
  stopAddress?: string;
  sortOrder: number;
  description?: string;
  scanMode: 'prefer_audio' | 'audio' | 'tts';
  isActive: boolean;
}

export interface UpdateQrActivationRequest extends CreateQrActivationRequest {}

export interface RejectRequest {
  adminNote: string;
}

export interface CollectAnalyticsRequest {
  eventName: string;
  anonymousId?: string;
  sessionId?: string;
  pageViewId?: string;
  poiId?: string;
  lang?: string;
  metadata: Record<string, unknown>;
}

export interface UpdateUserStatusRequest {
  isActive: boolean;
}

export interface UpdateUserRolesRequest {
  roles: string[];
}
