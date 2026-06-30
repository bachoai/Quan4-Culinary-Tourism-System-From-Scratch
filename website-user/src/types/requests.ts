import type { ContactInfo, OpeningHour, PoiImage } from './responses';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
  phoneNumber?: string;
}

export interface CreateOwnerRegistrationRequest {
  businessName: string;
  businessAddress: string;
  phoneNumber: string;
  description?: string;
}

export interface CoordinateRequest {
  latitude: number;
  longitude: number;
}

export interface CreateOwnerSubmissionRequest {
  submissionType: string;
  poiId?: string;
  poiName: string;
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
  tags: string[];
}

export interface CreatePoiLocalizationRequest {
  lang: string;
  name: string;
  description: string;
  audioUrl?: string;
  ttsScript?: string;
  isFallback: boolean;
}

export interface TranslatePoiLocalizationRequest {
  lang: string;
  sourceLang?: string;
  overwriteExisting?: boolean;
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
