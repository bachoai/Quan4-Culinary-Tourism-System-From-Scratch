# API Contract

## Base URL

- `http://localhost:5163`

## Auth Rule

- Public API: không cần token.
- Admin API: cần header `Authorization: Bearer <accessToken>` và role `Admin`.
- Owner API: cần header `Authorization: Bearer <accessToken>` và role `Owner`.
- Authenticated user API: cần Bearer token hợp lệ, không bắt buộc role cụ thể.

## Common Response Format

### Success

```json
{
  "success": true,
  "message": "...",
  "data": {}
}
```

### Error

```json
{
  "success": false,
  "message": "...",
  "errors": ["..."]
}
```

## Notes

- Tat ca controller deu wrap ket qua bang `ApiResponse<T>`.
- Error format den tu `ErrorResponse`.
- `GET /api/v1/categories` chi tra category active.
- `GET /api/v1/poi/load-all` va `GET /api/v1/poi/search` hien tra mang `PoiResponse[]`, khong phai `PagedResponse<T>`.
- Co 2 login endpoint cho admin:
  - `POST /api/v1/auth/login`: login chung, frontend can tu kiem tra role.
  - `POST /api/v1/admin/auth/login`: login admin va se tra `403` neu tai khoan khong co role `Admin`.
- `POST /api/v1/auth/register-owner` cần token hợp lệ, không cần role `Owner`.

## Data Models

### LoginRequest

```json
{
  "email": "admin@quan4tourism.local",
  "password": "Admin@123456"
}
```

### RegisterRequest

```json
{
  "fullName": "Nguyen Van A",
  "email": "user@example.com",
  "password": "123456",
  "phoneNumber": "0900000000"
}
```

### CategoryRequest

```json
{
  "code": "street-food",
  "name": "Street Food",
  "description": "Mon an duong pho",
  "iconUrl": "/uploads/images/icon.png",
  "sortOrder": 1,
  "isActive": true
}
```

### CreatePoiRequest / UpdatePoiRequest

```json
{
  "name": "Banh xeo Quan 4",
  "description": "Mon dac san pho am thuc",
  "categoryId": "6858ef...",
  "location": {
    "latitude": 10.759,
    "longitude": 106.707
  },
  "address": "123 Ton Dan",
  "ward": "Ward 8",
  "district": "Quan 4",
  "city": "TP.HCM",
  "priceRange": "$$",
  "priority": 5,
  "images": [
    {
      "url": "/uploads/images/poi-1.jpg",
      "caption": "Mat tien",
      "isThumbnail": true
    }
  ],
  "openingHours": [
    {
      "dayOfWeek": "Monday",
      "openTime": "08:00",
      "closeTime": "22:00",
      "isClosed": false
    }
  ],
  "contactInfo": {
    "phone": "0900000000",
    "email": "poi@example.com",
    "facebookUrl": "https://facebook.com/example",
    "websiteUrl": "https://example.com"
  },
  "ownerId": null,
  "tags": ["banh-xeo", "street-food"],
  "isActive": true,
  "activationRequested": false
}
```

### CreateOwnerRegistrationRequest

```json
{
  "businessName": "Quan An Co Ba",
  "businessAddress": "99 Vinh Khanh",
  "phoneNumber": "0900000001",
  "description": "Quan hai san"
}
```

### CreateOwnerSubmissionRequest

```json
{
  "submissionType": "create",
  "poiId": null,
  "poiName": "Oc Vu Nuong",
  "description": "Quan oc noi tieng",
  "categoryId": "6858ef...",
  "location": {
    "latitude": 10.758,
    "longitude": 106.705
  },
  "address": "200 Vinh Khanh",
  "ward": "Ward 10",
  "district": "Quan 4",
  "city": "TP.HCM",
  "priceRange": "$$",
  "images": [],
  "openingHours": [],
  "contactInfo": null,
  "tags": ["seafood"]
}
```


- `submissionType = "create"`: bo qua `poiId` hoac gui `null`.
- `submissionType = "update"`: `poiId` bat buoc phai la POI cua owner hien tai, frontend nen lay tu `GET /api/v1/owner/pois`.
### Approve / Reject Request

```json
{
  "adminNote": "Approved"
}
```

### CreatePoiLocalizationRequest / UpdatePoiLocalizationRequest

```json
{
  "lang": "en",
  "name": "English name",
  "description": "English description",
  "audioUrl": "/uploads/audio/poi-en.mp3",
  "isFallback": false
}
```

### UploadPoiAudioRequest

`multipart/form-data`

- `lang`: string, required
- `audioUrl`: string, optional
- `voiceName`: string, optional
- `sourceType`: string, optional, mac dinh `uploaded`
- `file`: binary, optional

### UpdateUserStatusRequest

```json
{
  "isActive": true
}
```

### UpdateUserRolesRequest

```json
{
  "roles": ["Admin"]
}
```

### CollectAnalyticsRequest

```json
{
  "eventName": "poi_viewed",
  "anonymousId": "guest-1",
  "sessionId": "session-1",
  "pageViewId": "page-1",
  "poiId": "6858ef...",
  "lang": "vi",
  "metadata": {
    "source": "web-admin"
  }
}
```

## Endpoints

### Health

#### GET `/api/health`

- Access: Public
- Request body: none
- Query params: none
- Response body: `ApiResponse<HealthResponse>`
- Notes: `data` gom `status`, `mongoConnected`, `serverTimeUtc`.

### Auth

#### POST `/api/v1/auth/register`

- Access: Public
- Request body: `RegisterRequest`
- Query params: none
- Response body: `ApiResponse<AuthResponse>`
- Notes: tạo user role mặc định `User`, `ownerStatus = none`, và trả JWT ngay để frontend tự đăng nhập sau khi đăng ký.

#### POST `/api/v1/auth/login`

- Access: Public
- Request body: `LoginRequest`
- Query params: none
- Response body: `ApiResponse<AuthResponse>`
- Notes: login chung cho moi role.

#### GET `/api/v1/auth/me`

- Access: Bearer token
- Request body: none
- Query params: none
- Response body: `ApiResponse<CurrentUserResponse>`
- Notes: dung de khoi phuc session frontend.

#### POST `/api/v1/auth/register-owner`

- Access: Bearer token
- Request body: `CreateOwnerRegistrationRequest`
- Query params: none
- Response body: `ApiResponse<OwnerRegistrationResponse>`
- Notes: đây là flow đúng để user gửi yêu cầu owner; backend chặn tạo trùng yêu cầu đang chờ duyệt và cập nhật `ownerStatus = pending`.

### Category

#### GET `/api/v1/categories`

- Access: Public
- Request body: none
- Query params: none
- Response body: `ApiResponse<CategoryResponse[]>`
- Notes: chi lay category active.

#### GET `/api/v1/categories/{id}`

- Access: Public
- Request body: none
- Query params: none
- Response body: `ApiResponse<CategoryResponse>`
- Notes: lay chi tiet category theo id.

#### POST `/api/v1/categories`

- Access: Admin
- Request body: `CreateCategoryRequest`
- Query params: none
- Response body: `ApiResponse<CategoryResponse>`
- Notes: check trung `code`.

#### PUT `/api/v1/categories/{id}`

- Access: Admin
- Request body: `UpdateCategoryRequest`
- Query params: none
- Response body: `ApiResponse<CategoryResponse>`
- Notes: co `isActive`.

#### DELETE `/api/v1/categories/{id}`

- Access: Admin
- Request body: none
- Query params: none
- Response body: `ApiResponse<null>`
- Notes: soft delete.

### POI Public

#### GET `/api/v1/poi/load-all`

- Access: Public
- Request body: none
- Query params:
  - `lang?`
  - `keyword?`
  - `categoryId?`
  - `priceRange?`
  - `page?`
  - `pageSize?`
- Response body: `ApiResponse<PoiResponse[]>`
- Notes: service dang tra list, khong tra metadata phan trang.

#### GET `/api/v1/poi/{id}`

- Access: Public
- Request body: none
- Query params:
  - `lang?`
- Response body: `ApiResponse<PoiDetailResponse>`
- Notes: `data` co them `openingHours`, `contactInfo`, `ownerId`, `audioStatus`.

#### GET `/api/v1/poi/nearby`

- Access: Public
- Request body: none
- Query params:
  - `lat` number
  - `lng` number
  - `radius?` int, mac dinh `3000`, max `10000`
  - `limit?` int, mac dinh `20`
  - `lang?`
- Response body: `ApiResponse<NearbyPoiResponse[]>`
- Notes: them `distanceMeters`.

#### GET `/api/v1/poi/search`

- Access: Public
- Request body: none
- Query params:
  - `lang?`
  - `keyword?`
  - `categoryId?`
  - `priceRange?`
  - `page?`
  - `pageSize?`
- Response body: `ApiResponse<PoiResponse[]>`
- Notes: hien tai cung tra list.

### POI Admin

#### POST `/api/v1/admin/pois`

- Access: Admin
- Request body: `CreatePoiRequest`
- Query params: none
- Response body: `ApiResponse<PoiDetailResponse>`
- Notes: frontend co the tao POI truc tiep.

#### PUT `/api/v1/admin/pois/{id}`

- Access: Admin
- Request body: `UpdatePoiRequest`
- Query params: none
- Response body: `ApiResponse<PoiDetailResponse>`
- Notes: cho phep cap nhat `activationRequested`.

#### DELETE `/api/v1/admin/pois/{id}`

- Access: Admin
- Request body: none
- Query params: none
- Response body: `ApiResponse<null>`
- Notes: soft delete.

#### PATCH `/api/v1/admin/pois/{id}/active?isActive={bool}`

- Access: Admin
- Request body: none
- Query params:
  - `isActive` boolean
- Response body: `ApiResponse<null>`
- Notes: bat/tat active POI.

### Owner

#### GET `/api/v1/owner/dashboard`

- Access: Owner
- Request body: none
- Query params: none
- Response body: `ApiResponse<OwnerDashboardResponse>`
- Notes: thong ke cho owner.

#### GET `/api/v1/owner/pois`

- Access: Owner
- Request body: none
- Query params:
  - `lang?`
- Response body: `ApiResponse<OwnerManagedPoiResponse[]>`
- Notes: chi tra POI co `OwnerId == currentUserId`; frontend owner dung endpoint nay de hien dropdown "Dia diem cua toi" khi gui submission `update`.

#### POST `/api/v1/owner/submissions`

- Access: Owner
- Request body: `CreateOwnerSubmissionRequest`
- Query params: none
- Response body: `ApiResponse<OwnerSubmissionResponse>`
- Notes: `create` co the bo qua `poiId`; `update` bat buoc co `poiId` hop le trong danh sach POI cua chinh owner.

#### GET `/api/v1/owner/submissions`

- Access: Owner
- Request body: none
- Query params: none
- Response body: `ApiResponse<OwnerSubmissionResponse[]>`
- Notes: lay danh sach submission cua owner hien tai.

#### GET `/api/v1/owner/submissions/{id}`

- Access: Owner
- Request body: none
- Query params: none
- Response body: `ApiResponse<OwnerSubmissionResponse>`
- Notes: chi owner tao submission moi truy cap duoc.

#### PUT `/api/v1/owner/submissions/{id}`

- Access: Owner
- Request body: `CreateOwnerSubmissionRequest`
- Query params: none
- Response body: `ApiResponse<OwnerSubmissionResponse>`
- Notes: chi sua duoc submission dang `pending`; neu la `update` backend van kiem tra `poiId` co thuoc owner hien tai hay khong.

### Admin

#### POST `/api/v1/admin/auth/login`

- Access: Public
- Request body: `LoginRequest`
- Query params: none
- Response body: `ApiResponse<AuthResponse>`
- Notes: chi cho phep tai khoan co role `Admin`.

#### GET `/api/v1/admin/dashboard/stats`

- Access: Admin
- Request body: none
- Query params: none
- Response body: `ApiResponse<AdminDashboardResponse>`
- Notes: gom `totalUsers`, `totalOwners`, `totalPois`, `totalActivePois`, `pendingOwnerRegistrations`, `pendingSubmissions`, `totalPoiViews`, `totalAudioPlays`.

#### GET `/api/v1/admin/users`

- Access: Admin
- Request body: none
- Query params: none
- Response body: `ApiResponse<UserResponse[]>`
- Notes: chua co filter o backend; frontend loc local.

#### PUT `/api/v1/admin/users/{id}/status`

- Access: Admin
- Request body: `UpdateUserStatusRequest`
- Query params: none
- Response body: `ApiResponse<null>`
- Notes: khoa/mo khoa user.

#### PUT `/api/v1/admin/users/{id}/roles`

- Access: Admin
- Request body: `UpdateUserRolesRequest`
- Query params: none
- Response body: `ApiResponse<null>`
- Notes: ghi audit log `update_user_role`.

#### GET `/api/v1/admin/owner-registrations`

- Access: Admin
- Request body: none
- Query params:
  - `status?`
- Response body: `ApiResponse<OwnerRegistrationResponse[]>`
- Notes: `status` nhan `pending|approved|rejected`.

#### PUT `/api/v1/admin/owner-registrations/{id}/approve`

- Access: Admin
- Request body: `ApproveOwnerRegistrationRequest`
- Query params: none
- Response body: `ApiResponse<null>`
- Notes: se them role `Owner` cho user neu chua co.

#### PUT `/api/v1/admin/owner-registrations/{id}/reject`

- Access: Admin
- Request body: `RejectOwnerRegistrationRequest`
- Query params: none
- Response body: `ApiResponse<null>`
- Notes: `adminNote` bat buoc.

#### GET `/api/v1/admin/submissions`

- Access: Admin
- Request body: none
- Query params:
  - `status?`
- Response body: `ApiResponse<OwnerSubmissionResponse[]>`
- Notes: chua co filter submissionType o backend; frontend loc local.

#### PUT `/api/v1/admin/submissions/{id}/approve`

- Access: Admin
- Request body: `ApproveSubmissionRequest`
- Query params: none
- Response body: `ApiResponse<null>`
- Notes: neu `submissionType = create` se tao POI moi; neu `update` se cap nhat POI hien co.

#### PUT `/api/v1/admin/submissions/{id}/reject`

- Access: Admin
- Request body: `RejectSubmissionRequest`
- Query params: none
- Response body: `ApiResponse<null>`
- Notes: `adminNote` bat buoc.

### Audio

#### GET `/api/v1/audio/languages`

- Access: Public
- Request body: none
- Query params: none
- Response body: `ApiResponse<AudioLanguageResponse[]>`
- Notes: backend tra ma ngon ngu tu `SharedConstants.SupportedLanguages`.

#### GET `/api/v1/poi/{id}/audio`

- Access: Public
- Request body: none
- Query params:
  - `lang?`
- Response body: `ApiResponse<PoiAudioResponse | null>`
- Notes: neu khong co audio theo `lang`, backend lay audio dau tien neu co.

#### POST `/api/v1/admin/pois/{id}/audio`

- Access: Admin
- Request body: `multipart/form-data`
- Query params: none
- Response body: `ApiResponse<PoiAudioResponse>`
- Notes: upload file hoac truyen `audioUrl`; it nhat phai co 1 trong 2.

#### GET `/api/v1/audio/pack-manifest`

- Access: Public
- Request body: none
- Query params: none
- Response body: `ApiResponse<object>`
- Notes: `data` co `version`, `generatedAt`, `items[]`; moi item gom `poiId`, `poiName`, `audios[]`.

### Localization

#### GET `/api/v1/admin/pois/{id}/localizations`

- Access: Admin
- Request body: none
- Query params: none
- Response body: `ApiResponse<PoiLocalizationResponse[]>`
- Notes: id la `poiId`.

#### POST `/api/v1/admin/pois/{id}/localizations`

- Access: Admin
- Request body: `CreatePoiLocalizationRequest`
- Query params: none
- Response body: `ApiResponse<PoiLocalizationResponse>`
- Notes: khong cho tao trung `lang`.

#### PUT `/api/v1/admin/pois/{id}/localizations/{lang}`

- Access: Admin
- Request body: `UpdatePoiLocalizationRequest`
- Query params: none
- Response body: `ApiResponse<PoiLocalizationResponse>`
- Notes: `lang` nam o path.

#### DELETE `/api/v1/admin/pois/{id}/localizations/{lang}`

- Access: Admin
- Request body: none
- Query params: none
- Response body: `ApiResponse<null>`
- Notes: xoa theo `poiId + lang`.

### Media

#### POST `/api/v1/admin/media/upload-image`

- Access: Admin
- Request body: `multipart/form-data` voi field `file`
- Query params: none
- Response body: `ApiResponse<MediaFileResponse>`
- Notes: validate kich thuoc va loai file theo `UploadSettings`.

#### POST `/api/v1/admin/media/upload-audio`

- Access: Admin
- Request body: `multipart/form-data` voi field `file`
- Query params: none
- Response body: `ApiResponse<MediaFileResponse>`
- Notes: luu file local vao `wwwroot/uploads`.

### Analytics

#### POST `/api/v1/analytics/collect`

- Access: Public
- Request body: `CollectAnalyticsRequest`
- Query params: none
- Response body: `ApiResponse<null>`
- Notes: event hop le theo convention hien co gom `poi_viewed`, `audio_played`, `search_executed`, `nearby_requested`, `language_changed`.

#### GET `/api/v1/admin/analytics/summary`

- Access: Admin
- Request body: none
- Query params: none
- Response body: `ApiResponse<AnalyticsSummaryResponse>`
- Notes: gom `poiViewedCount`, `audioPlayedCount`, `searchExecutedCount`, `topPoiViews`, `topPoiAudioPlays`.

### Maps

#### GET `/api/v1/maps/pack-manifest`

- Access: Public
- Request body: none
- Query params: none
- Response body: `ApiResponse<MapPackResponse | null>`
- Notes: co the tra `data = null` neu chua co active map pack.

