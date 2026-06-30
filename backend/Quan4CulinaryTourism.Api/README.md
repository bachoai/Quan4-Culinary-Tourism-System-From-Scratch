# Quan4CulinaryTourism.Api

Backend ASP.NET Core Web API cho dự án du lịch ẩm thực Quận 4, dùng MongoDB, JWT Bearer, media lưu trên Cloudinary, và TTS Python cho audio thuyết minh.

## Yêu cầu

- .NET SDK 10
- MongoDB tại `mongodb://localhost:27017`
- Python nếu muốn dùng TTS tự động
- Tài khoản Cloudinary nếu muốn upload ảnh/audio

Nếu chưa có MongoDB local, từ repo root chạy:

```powershell
docker compose up mongo -d
```

## Cài Python cho TTS

Backend dùng Python + `gTTS` để tự sinh file audio. Trên Windows, cài Python bằng `winget`:

```powershell
winget install Python.Python.3.12
```

Đóng mở lại terminal, rồi kiểm tra:

```powershell
python --version
python -m pip --version
```

Sau khi vào thư mục backend, cài thư viện TTS:

```powershell
cd backend\Quan4CulinaryTourism.Api
python -m pip install -r tools\requirements-tts.txt
```

## Chạy backend local

```powershell
cd backend\Quan4CulinaryTourism.Api
dotnet restore
python -m pip install -r tools\requirements-tts.txt
dotnet run
```

If Windows shows `An Application Control policy has blocked this file.`, use the Docker fallback from the backend folder:

```powershell
.\run-in-docker.ps1
```

That script starts `mongo` + `api` through repo-root Compose and waits for `http://localhost:5163/api/health`.

Khi chạy bằng `dotnet run`:

- profile mặc định là `Development`
- API ở `http://localhost:5163`
- Swagger ở `http://localhost:5163/swagger`

## Tài khoản admin local

Trong `Development`, backend tự seed admin từ `appsettings.Development.json`:

- Email: `admin@quan4tourism.local`
- Password: `Admin@123456`

## Cấu hình

Nguồn cấu hình chính:

- `appsettings.json`: giá trị cơ sở
- `appsettings.Development.json`: giá trị để chạy local
- biến môi trường: dùng để override khi cần

Tóm tắt:

- `dotnet run` không tự đọc file `.env.example`
- backend có `EnvFileLoader`, nên nếu tạo file `.env` thật trong `backend/Quan4CulinaryTourism.Api` thì API sẽ tự nạp
- `backend/Quan4CulinaryTourism.Api/.env.example` là template duy nhất của backend để copy thành `.env` hoặc import vào shell/IDE/server
- `docker compose up --build` ở repo root cũng dùng chính file `backend/Quan4CulinaryTourism.Api/.env`

Section quan trọng:

- `MongoDbSettings`
- `JwtSettings`
- `UploadSettings`
- `CloudinarySettings`
- `TextToSpeechSettings`
- `Ai`
- `DefaultAdmin`
- `Cors.AllowedOrigins`

### Cloudinary

Media upload tay và audio TTS đều đi qua Cloudinary.

Cần cấu hình tối thiểu:

- `CloudinarySettings:CloudName`
- `CloudinarySettings:ApiKey`
- `CloudinarySettings:ApiSecret`

Tuỳ chọn:

- `CloudinarySettings:RootFolder`

Ví dụ biến môi trường:

```env
CloudinarySettings__CloudName=your-cloud-name
CloudinarySettings__ApiKey=your-api-key
CloudinarySettings__ApiSecret=your-api-secret
CloudinarySettings__RootFolder=quan4-culinary-tourism
```

Nếu thiếu cấu hình Cloudinary, các request upload ảnh/audio sẽ fail với lỗi cấu hình rõ ràng.

## Chatbot AI + Database

Chatbot gợi ý địa điểm ăn uống đi theo luồng:

- nhận message từ frontend
- lấy POI public từ MongoDB
- chấm điểm candidate ở backend
- chỉ đưa candidate list đó cho AI để viết câu trả lời
- fallback rule-based nếu AI lỗi hoặc thiếu cấu hình

Thiết lập AI:

- section `Ai` trong `appsettings.json`
- biến môi trường `AI_ENABLED`, `AI_BASE_URL`, `AI_API_KEY`, `AI_MODEL`, `AI_TIMEOUT_SECONDS`
- API key thật chỉ nên đặt ở backend qua biến môi trường hoặc file `.env` local

Nếu `AI_ENABLED=false` hoặc thiếu `AI_API_KEY`, endpoint chat vẫn trả kết quả bằng fallback rule-based.

## Audio tiếng Việt từ Python

Backend có script Python `tools/tts_generate.py` dùng `gTTS` để tạo MP3 tiếng Việt từ `ttsScript` hoặc `description` của POI.

Luồng hiện tại:

- FE gọi `GET /api/v1/poi/{id}/audio?lang=vi`
- nếu Mongo chưa có `PoiAudio` phù hợp, backend sẽ sinh file MP3 tạm
- file tạm được upload lên Cloudinary
- URL Cloudinary và metadata storage được lưu vào `poi_audios`

Thiết lập chính:

- `TextToSpeechSettings:Enabled`
- `TextToSpeechSettings:PythonCommand`
- `TextToSpeechSettings:DefaultVoice`

## API chính

- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `GET /api/v1/auth/me`
- `GET /api/v1/categories`
- `GET /api/v1/poi/load-all`
- `GET /api/v1/poi/nearby`
- `POST /api/v1/owner/submissions`
- `GET /api/v1/admin/dashboard/stats`
- `POST /api/v1/admin/pois`
- `POST /api/v1/admin/media/upload-image`
- `POST /api/v1/analytics/collect`
- `GET /api/health`

## Kiểm tra nhanh

1. `GET /api/health`
2. `POST /api/v1/auth/login` bằng tài khoản admin local
3. `GET /api/v1/categories`
4. `GET /api/v1/poi/load-all`
5. upload thử ảnh hoặc audio sau khi đã cấu hình Cloudinary

## Build

```powershell
dotnet build
```
