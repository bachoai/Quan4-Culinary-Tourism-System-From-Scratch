export type Lang = 'vi' | 'en' | 'zh' | 'ja' | 'ko';

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
}

export interface Category {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  iconUrl?: string | null;
}

export interface PoiImage {
  url: string;
  caption?: string | null;
  isThumbnail?: boolean;
}

export interface ContactInfo {
  phone?: string | null;
  email?: string | null;
  facebookUrl?: string | null;
  websiteUrl?: string | null;
}

export interface OpeningHour {
  dayOfWeek: string;
  openTime: string;
  closeTime: string;
  isClosed: boolean;
}

export interface Poi {
  id: string;
  name: string;
  description: string;
  categoryId: string;
  address: string;
  ward?: string;
  district?: string;
  city?: string;
  priceRange: string;
  rating: number;
  reviewCount: number;
  priority: number;
  mapUrl?: string | null;
  ttsScript?: string | null;
  latitude: number;
  longitude: number;
  geofenceRadiusMeters?: number;
  autoNarrationEnabled?: boolean;
  tags: string[];
  images: PoiImage[];
  isActive?: boolean;
}

export interface PoiDetail extends Poi {
  openingHours: OpeningHour[];
  contactInfo?: ContactInfo | null;
  audioStatus?: string;
}

export interface NearbyPoi extends Poi {
  distanceMeters: number;
}

export interface PoiAudio {
  id: string;
  poiId: string;
  lang: string;
  audioUrl: string;
  voiceName?: string | null;
  durationSeconds?: number;
}

export interface AudioLanguageResponse {
  code: string;
  name: string;
}

export interface AudioPackManifestResponse {
  version: string;
  generatedAt: string;
  items: Array<{
    poiId: string;
    poiName: string;
    audios: Array<{ lang: string; audioUrl: string; status: string }>;
  }>;
}

export interface CurrentUser {
  id: string;
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  avatarUrl?: string | null;
  roles: string[];
  isActive: boolean;
  emailVerified: boolean;
  ownerStatus: string;
}

export interface AuthResponse {
  token: string;
  user: CurrentUser;
}

export interface OwnerRegistrationResponse {
  id: string;
  userId: string;
  businessName: string;
  businessAddress: string;
  phoneNumber: string;
  description?: string | null;
  status: string;
  adminNote?: string | null;
  createdAt: string;
}

export interface OwnerSubmissionResponse {
  id: string;
  ownerId: string;
  poiId?: string | null;
  submissionType: string;
  poiName: string;
  description: string;
  categoryId: string;
  latitude: number;
  longitude: number;
  address: string;
  ward: string;
  district: string;
  city: string;
  priceRange: string;
  priority: number;
  mapUrl?: string | null;
  ttsScript?: string | null;
  geofenceRadiusMeters: number;
  autoNarrationEnabled: boolean;
  images: PoiImage[];
  openingHours: OpeningHour[];
  contactInfo?: ContactInfo | null;
  tags: string[];
  status: string;
  adminNote?: string | null;
  createdAt: string;
}

export interface OwnerDashboardResponse {
  totalPois: number;
  totalSubmissions: number;
  pendingSubmissions: number;
  approvedSubmissions: number;
  rejectedSubmissions: number;
  totalViews: number;
  uniqueVisitors: number;
  totalAudioPlays: number;
  uniqueAudioListeners: number;
  totalQrScans: number;
}

export interface OwnerManagedPoi {
  id: string;
  name: string;
  description: string;
  categoryId: string;
  address: string;
  ward: string;
  district: string;
  city: string;
  priceRange: string;
  rating: number;
  reviewCount: number;
  priority: number;
  mapUrl?: string | null;
  ttsScript?: string | null;
  latitude: number;
  longitude: number;
  geofenceRadiusMeters: number;
  autoNarrationEnabled: boolean;
  tags: string[];
  images: PoiImage[];
  isActive: boolean;
  openingHours: OpeningHour[];
  contactInfo?: ContactInfo | null;
  ownerId?: string | null;
  audioStatus: string;
  activationRequested: boolean;
  createdAt: string;
  updatedAt: string;
  viewCount: number;
  uniqueVisitorCount: number;
  audioPlayCount: number;
  uniqueAudioListenerCount: number;
  qrScanCount: number;
}

export interface TourStopResponse {
  poiId: string;
  title?: string | null;
  order: number;
  estimatedStayMinutes: number;
}

export interface TourResponse {
  id: string;
  title: string;
  description: string;
  lang: string;
  coverImageUrl?: string | null;
  estimatedDurationMinutes: number;
  isActive: boolean;
  stops: TourStopResponse[];
  updatedAt: string;
}

export interface QrActivationResponse {
  id: string;
  code: string;
  poiId: string;
  poiName: string;
  poiAddress: string;
  poiWard: string;
  title: string;
  stopZone: string;
  stopAddress?: string | null;
  sortOrder: number;
  description?: string | null;
  scanMode: 'prefer_audio' | 'audio' | 'tts';
  deepLink: string;
  isActive: boolean;
  updatedAt: string;
}

export interface MapPackResponse {
  id: string;
  version: string;
  name: string;
  downloadUrl: string;
  sha256: string;
  sizeBytes: number;
  isActive: boolean;
  publishedAt?: string | null;
}

export interface HealthResponse {
  status: string;
  mongoConnected: boolean;
  serverTimeUtc: string;
}
