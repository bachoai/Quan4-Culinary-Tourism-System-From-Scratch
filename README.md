# Quan4 Culinary Tourism System

Monorepo gồm:

- Backend API ASP.NET Core + MongoDB
- Admin web React/Vite
- Public web React/Vite
- Mobile app .NET MAUI

## Cấu trúc chính

- `backend/Quan4CulinaryTourism.Api`: backend API
- `website-admin`: trang quản trị
- `website-user`: website public cho du khách
- `mobile/Quan4CulinaryTourism.Mobile`: ứng dụng mobile
- `docs/API_CONTRACT.md`: hợp đồng API và các flow chính
- `scripts/create-clean-zip.ps1`: tạo file zip sạch trước khi bàn giao/demo

## Cổng mặc định khi chạy local

| Thành phần | URL / kết nối |
| --- | --- |
| MongoDB | `mongodb://localhost:27017` |
| API backend | `http://localhost:5163` |
| Swagger | `http://localhost:5163/swagger` |
| Admin web | `http://localhost:5173` |
| Public web | `http://localhost:5174` |

## Yêu cầu môi trường

- Docker Desktop hoặc Docker Engine + Docker Compose
- .NET SDK 10
- Node.js 20+
- .NET MAUI workload nếu cần chạy mobile

## Chạy local

Các lệnh dưới đây viết theo PowerShell và nên chạy từ repo root:

```powershell
cd D:\bac\PhoAmThucVinhKhanh
```

### 1. MongoDB

```powershell
docker compose up mongo -d
```

### 2. Backend API

```powershell
cd backend\Quan4CulinaryTourism.Api
dotnet restore
dotnet run
```

Ghi chú:

- Swagger chỉ bật trong `Development`.
- Backend tự load file `.env` trong thư mục backend nếu có.
- Template backend nằm ở `backend/Quan4CulinaryTourism.Api/.env.example`.
- `appsettings.Development.json` đã có cấu hình local để chạy nhanh.
- Nếu database trống, backend sẽ seed role, admin, category và dữ liệu demo.

Tài khoản admin local mặc định:

- Email: `admin@quan4tourism.local`
- Password: `Admin@123456`

### 3. Admin web

```powershell
Copy-Item website-admin\.env.example website-admin\.env
cd website-admin
npm ci
npm run dev
```

Admin web chạy tại `http://localhost:5173`.

### 4. Public web

```powershell
Copy-Item website-user\.env.example website-user\.env
cd website-user
npm ci
npm run dev
```

Public web chạy tại `http://localhost:5174`.

### 5. Mobile

Build Windows:

```powershell
cd mobile\Quan4CulinaryTourism.Mobile
dotnet build -f net10.0-windows10.0.19041.0
```

Build Android:

```powershell
cd mobile\Quan4CulinaryTourism.Mobile
dotnet build -f net10.0-android
```

API base URL nên dùng:

- Windows: `http://localhost:5163`
- Android Emulator: `http://10.0.2.2:5163`
- Máy thật: LAN IP của máy đang chạy backend, ví dụ `http://192.168.1.50:5163`

## Chạy backend bằng Docker

Nếu muốn chạy cả MongoDB và API trong Docker:

```powershell
Copy-Item backend\Quan4CulinaryTourism.Api\.env.example backend\Quan4CulinaryTourism.Api\.env
docker compose up --build
```

Biến cần có trong `backend/Quan4CulinaryTourism.Api/.env`:

- `JwtSettings__SecretKey`
- `DefaultAdmin__Email`
- `DefaultAdmin__Password`
- `Cors__AllowedOrigins__0`
- `Cors__AllowedOrigins__1`

Lưu ý:

- `docker compose up mongo -d` không cần file env nào.
- `docker compose up --build` dùng `backend/Quan4CulinaryTourism.Api/.env` cho API container.
- API container chạy `Production`, nên không có Swagger.

## Deploy tách backend/admin/public

### Backend API

- Deploy API riêng.
- Cấu hình `JwtSettings__SecretKey` bằng secret thật, tối thiểu 32 ký tự.
- Không để `DefaultAdmin__Password` rỗng trong production nếu vẫn bật seed admin.
- Chỉ bật CORS cho đúng domain admin và public.
- Không commit `.env`, secret, hoặc file upload demo.

### Admin web

- Deploy `website-admin` riêng, domain riêng.
- Chỉ cấu hình `VITE_API_BASE_URL` trỏ tới domain API.
- Không hardcode production API URL trong source.

### Public web

- Deploy `website-user` riêng, domain riêng.
- Cấu hình:
  - `VITE_API_BASE_URL`
  - `VITE_MAPTILER_KEY`
  - `VITE_OSRM_BASE_URL`
  - `VITE_OSRM_PROFILE`
- Backend `PublicSiteSettings:BaseUrl` phải trỏ đúng domain public để QR/deep link mở đúng trang khách.

### CORS backend

Backend phải thêm đúng domain của:

- Admin web
- Public web

Ví dụ production:

```env
Cors__AllowedOrigins__0=https://admin.example.com
Cors__AllowedOrigins__1=https://www.example.com
```

## Build kiểm tra

### Backend

```powershell
dotnet build backend/Quan4CulinaryTourism.Api/Quan4CulinaryTourism.Api.csproj
```

### Admin web

```powershell
cd website-admin
Remove-Item -Recurse -Force node_modules, dist -ErrorAction SilentlyContinue
npm ci
npm run build
```

### Public web

```powershell
cd website-user
Remove-Item -Recurse -Force node_modules, dist -ErrorAction SilentlyContinue
npm ci
npm run build
```

## Tạo zip sạch trước khi bàn giao

Script này tự loại trừ:

- `.git/`
- `.codegraph/`
- `.codex/`
- `.agents/`
- `node_modules/`
- `dist/`
- `bin/`
- `obj/`
- `tmp/`
- `backend/_build_verify*/`
- `backend/**/_build_verify*/`
- `backend/Quan4CulinaryTourism.Api/wwwroot/uploads/`
- `.env`
- `.env.*` trừ `.env.example`

Ngoài ra, nếu file zip đầu ra nằm trong một thư mục con của repo, script sẽ tự bỏ qua toàn bộ thư mục đầu ra đó để không cuốn zip cũ hoặc zip mới tạo vào chính archive mới.

Chạy nhanh:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\create-clean-zip.ps1
```

Khuyến nghị khi bàn giao:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\create-clean-zip.ps1 -OutputPath .\release\PhoAmThucVinhKhanh-clean.zip
```

## Tài liệu từng app

- [Backend API](backend/Quan4CulinaryTourism.Api/README.md)
- [Admin website](website-admin/README.md)
- [Public website](website-user/README.md)
- [Mobile app](mobile/Quan4CulinaryTourism.Mobile/README.md)
