import type { ContactInfo, OpeningHour, PoiImage } from './common';

export interface CurrentUserResponse {
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
  user: CurrentUserResponse;
}

export interface CategoryResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  iconUrl?: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface PoiResponse {
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
}

export interface PoiDetailResponse extends PoiResponse {
  openingHours: OpeningHour[];
  contactInfo?: ContactInfo | null;
  ownerId?: string | null;
  audioStatus: string;
}

export interface NearbyPoiResponse extends PoiResponse {
  distanceMeters: number;
}

export interface UserResponse extends CurrentUserResponse {
  lastLoginAt?: string | null;
  createdAt: string;
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

export interface OwnerRegistrationAdminResponse extends OwnerRegistrationResponse {
  reviewedBy?: string | null;
  reviewedAt?: string | null;
}

export interface OwnerSubmissionResponse {
  id: string;
  ownerId: string;
  poiId?: string | null;
  submissionType: string;
  poiName: string;
  priority: number;
  mapUrl?: string | null;
  ttsScript?: string | null;
  geofenceRadiusMeters: number;
  autoNarrationEnabled: boolean;
  status: string;
  adminNote?: string | null;
  createdAt: string;
}

export interface AdminDashboardResponse {
  totalUsers: number;
  totalOwners: number;
  totalPois: number;
  totalActivePois: number;
  pendingOwnerRegistrations: number;
  pendingSubmissions: number;
  totalPoiViews: number;
  totalAudioPlays: number;
  activeVisitorsNow: number;
  anonymousVisitorsNow: number;
  activeWindowSeconds: number;
}

export interface PoiLocalizationResponse {
  id: string;
  poiId: string;
  lang: string;
  name: string;
  description: string;
  audioUrl?: string | null;
  ttsScript?: string | null;
  isFallback: boolean;
}

export interface PoiAudioResponse {
  id: string;
  poiId: string;
  lang: string;
  audioUrl: string;
  voiceName?: string | null;
  sourceType: string;
  status: string;
  durationSeconds: number;
  fileSizeBytes: number;
}

export interface AudioLanguageResponse {
  code: string;
  name: string;
}

export interface TopPoiAnalyticsResponse {
  poiId: string;
  count: number;
}

export interface AnalyticsHeatmapPointResponse {
  latitude: number;
  longitude: number;
  count: number;
  lastSeenAt: string;
}

export interface AnalyticsRoutePointResponse {
  latitude: number;
  longitude: number;
  isBackground: boolean;
  source: string;
  createdAt: string;
}

export interface AnalyticsRouteTraceResponse {
  anonymousId: string;
  sessionId?: string | null;
  pointCount: number;
  startedAt: string;
  endedAt: string;
  points: AnalyticsRoutePointResponse[];
}

export interface UsageHistoryEntryResponse {
  id: string;
  anonymousId?: string | null;
  sessionId?: string | null;
  pageViewId?: string | null;
  eventName: string;
  poiId?: string | null;
  lang?: string | null;
  metadata: Record<string, unknown>;
  createdAt: string;
}

export interface AnalyticsSummaryResponse {
  poiViewedCount: number;
  audioPlayedCount: number;
  searchExecutedCount: number;
  averageListenDurationSeconds: number;
  topPoiViews: TopPoiAnalyticsResponse[];
  topPoiAudioPlays: TopPoiAnalyticsResponse[];
  heatmapPoints: AnalyticsHeatmapPointResponse[];
  recentRouteTraces: AnalyticsRouteTraceResponse[];
  realtimeSnapshot: AnalyticsRealtimeSnapshotResponse;
}

export interface AnalyticsActiveVisitorResponse {
  visitorKey: string;
  anonymousId?: string | null;
  sessionId?: string | null;
  lang?: string | null;
  path?: string | null;
  pageTitle?: string | null;
  isAuthenticated: boolean;
  lastSeenAt: string;
}

export interface AnalyticsRealtimeSnapshotResponse {
  activeVisitorCount: number;
  anonymousVisitorCount: number;
  authenticatedVisitorCount: number;
  activeWindowSeconds: number;
  activeVisitors: AnalyticsActiveVisitorResponse[];
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

export interface MediaFileResponse {
  id: string;
  fileName: string;
  originalFileName: string;
  url: string;
  contentType: string;
  fileType: string;
  sizeBytes: number;
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

export interface AudioPackManifestResponse {
  version: string;
  generatedAt: string;
  items: Array<{
    poiId: string;
    poiName: string;
    audios: Array<{ lang: string; audioUrl: string; status: string }>;
  }>;
}
