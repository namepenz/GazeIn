#  Gaze-In — AI 기반 학습자 몰입 상태 분석 시스템

<p align="center">
  <img src="https://img.shields.io/badge/Unity-6000.3-black?logo=unity" />
  <img src="https://img.shields.io/badge/Meta_Quest_Pro-XR-blue?logo=meta" />
  <img src="https://img.shields.io/badge/FastAPI-0.100+-green?logo=fastapi" />
  <img src="https://img.shields.io/badge/PyTorch-2.0+-orange?logo=pytorch" />
  <img src="https://img.shields.io/badge/Python-3.10+-yellow?logo=python" />
  <img src="https://img.shields.io/badge/Flutter-3.x-blue?logo=flutter" />
</p>

<br/>

> **VR 환경에서 학습자의 시선(Eye)·안면(Face) 데이터를 실시간 분석하여**  
> **졸음 / 집중 / 이해불능 상태를 자동 판별하고, LLM 기반 AI 튜터가 즉각 개입하는 시스템**  
> **+ Flutter 모바일 앱으로 학습 분석 데이터를 실시간 확인**

---

## 📌 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 프로젝트명 | Gaze-In (지능형 학습 몰입 케어 시스템) |
| 개발 기간 | 2025.03 ~ |
| 팀 구성 | 3인 (Unity / FastAPI / AI 모델) |
| 핵심 목표 | Meta Quest Pro의 90Hz 시선·안면 데이터로 학습 상태 실시간 판별 |

### 연관 저장소
| 저장소 | 설명 |
|--------|------|
| **이 저장소 (GazeIn)** | Unity VR 클라이언트 + FastAPI 백엔드 |
| [true_study_tracker](https://github.com/namepenz/true_study_tracker) | Flutter 학습 분석 모바일 앱 |

---

## 🏗️ 시스템 아키텍처

```
Meta Quest Pro (90Hz)
 ├─ Eye Tracking      → 시선 방향, 눈 떠짐
 └─ Face Tracking     → BlendShape 63채널 (표정 데이터)
         │
         ▼
 Unity C# (GazeDataFeeder)
 ├─ Vuplex CanvasWebViewPrefab (VR 내 웹 브라우저)
 ├─ 시선 Raycast → 브라우저 픽셀 좌표(px, py) 변환
 ├─ Intelligent Slicing: Angular Velocity 기반 Fixation 감지
 ├─ DOM 스냅샷 수집 (DomSnapshotCollector)
 └─ HTTP POST → FastAPI 서버 (비동기 Coroutine)
         │
         ▼
 FastAPI Backend (AWS EC2)
 ├─ /ingest          : VR 시선 데이터 수신 + S3 저장
 ├─ /tutor/check-state : Flutter 앱에 학습 분석 결과 제공
 ├─ Pydantic 데이터 파싱
 ├─ 규칙 기반 자동 라벨링 (DOM 요소 매칭)
 └─ Transformer 모델 추론 (focused / drowsy / disengaged)
         │
         ▼
 Gemini LLM AI 튜터              Flutter 모바일 앱
 └─ 상태별 능동 개입          └─ 시간대별 집중도 그래프 + 분석 리포트
    (질문 생성 / 격려 / 경고)
```

---

## 📡 서버 API

### `POST /ingest` — VR → 서버 (시선 데이터 수신)
Unity 클라이언트가 시선 데이터를 실시간으로 전송하는 엔드포인트.

```json
{
  "userId": "user001",
  "timestamp": 1700000000.0,
  "gaze_points": [
    {
      "browser_pixel_x": 640,
      "browser_pixel_y": 360,
      "hit_canvas": true,
      "angular_velocity": 3.2,
      "eye_openness": 0.95,
      "face_blend_shapes": { "eyeBlinkLeft": 0.1, "jawOpen": 0.0 }
    }
  ],
  "url": "https://example.com/study",
  "dom_snapshot": []
}
```

---

### `POST /tutor/check-state` — 서버 → Flutter 앱 (분석 결과 제공)
Flutter 앱이 오늘의 학습 분석 데이터를 요청하는 엔드포인트.

**요청:**
```json
{ "userId": "user001" }
```

**응답:**
```json
{
  "status": 200,
  "data": {
    "userId": "user001",
    "date": "2026-05-09",
    "vrTotalTime": 7200,
    "pureFocusTime": 5400,
    "distractionCount": 3,
    "gazeTimeline": [
      { "hour": 9,  "score": 95 },
      { "hour": 10, "score": 72 },
      { "hour": 11, "score": 40 }
    ]
  }
}
```

| 필드 | 타입 | 설명 |
|------|------|------|
| `vrTotalTime` | int | VR 총 접속 시간 (초 단위) |
| `pureFocusTime` | int | 실제 집중 시간 (초 단위) |
| `distractionCount` | int | 시선 이탈 횟수 |
| `gazeTimeline[].hour` | int | 시각 (0~23) |
| `gazeTimeline[].score` | int | 집중 점수 (0~100) |

---

## ✨ 핵심 기능

### 1. Intelligent Slicing
단순 시간 단위가 아닌 시선 각속도(Angular Velocity) 기반으로 Saccade/Fixation 구간을 감지합니다.
- Saccade(속도 급증): 시선 이동 중 → 노이즈 구간
- Fixation(속도 정체): 시선 고정 → 의미 있는 데이터 구간
- Fixation 시작 시점을 트리거로 데이터 청크 생성 → 데이터 품질 극대화

### 2. 멀티모달 데이터 수집
```
시선 방향 (left/right gaze direction)  →┐
눈 떠짐 정도 (PERCLOS 기반)            →┤→ Transformer 분류 모델
얼굴 BlendShape 63채널                 →┤
브라우저 픽셀 좌표 (px, py)             →┘
```

### 3. VR 웹 브라우저 기반 학습 환경
- Vuplex CanvasWebViewPrefab으로 VR 내 웹 브라우저 구현
- 시선 Raycast → Canvas UV → 브라우저 픽셀 좌표 변환
- 현재 URL + DOM 요소 좌표 자동 수집

### 4. 규칙 기반 자동 라벨링
| 라벨 | 조건 |
|------|------|
| `reading_text` | P, SPAN, LI 위 Fixation |
| `reading_heading` | H1~H6 위 Fixation |
| `reading_code` | CODE, PRE 위 Fixation |
| `viewing_image` | IMG 위 Fixation |
| `scanning` | Saccade 구간 |
| `deep_focus` | Fixation + Angular Velocity < 5°/s |

### 5. 실시간 상태 분류
| 상태 | 설명 |
|------|------|
| 🟢 Focused | 정상 집중 상태 |
| 🟡 Drowsy | 졸음 감지 (PERCLOS 기반) |
| 🔴 Disengaged | 몰입 이탈 상태 |

### 6. Flutter 학습 분석 앱
VR 세션 종료 후, 모바일 앱([true_study_tracker](https://github.com/namepenz/true_study_tracker))에서:
- 원형 집중 효율 게이지 (S~D 등급)
- 시간대별 시선 집중도 그래프
- AI 기반 학습 피드백

---

## 🛠️ 기술 스택

### Client (VR)
- Unity 6 (C#)
- Meta XR SDK (OVREyeGaze, OVRFaceExpressions)
- Vuplex 3D WebView (CanvasWebViewPrefab)
- OpenXR (Meta Quest Pro)

### Server (AWS EC2)
- FastAPI (Python)
- PyTorch (Transformer 분류 모델)
- Pydantic (데이터 검증)
- Uvicorn (ASGI 서버)
- AWS S3 (데이터 저장)

### Mobile (Flutter)
- Flutter 3.x (Dart)
- Provider (상태 관리)
- fl_chart (집중도 그래프)
- http (REST API 통신)

---

## 📁 프로젝트 구조

```
GazeIn/
├── Assets/
│   └── Scripts/
│       ├── GazeDataFeeder.cs        # 시선 데이터 수집 + 서버 전송
│       ├── TutorManager.cs          # AI 튜터 응답 관리
│       ├── DomSnapshotCollector.cs  # DOM 요소 좌표 스냅샷
│       └── ControllerLaserPointer.cs # VR 컨트롤러 레이캐스트
├── gazein_server/
│   ├── main.py                      # FastAPI 엔드포인트
│   └── models.py                    # Pydantic 데이터 모델
└── pytest/
    └── pdf_extractor.py             # PDF 좌표 추출 (레거시)
```

---

## 🚀 실행 방법

### FastAPI 서버 실행
```bash
cd gazein_server
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

### Unity Quest 빌드
1. Meta Quest Pro USB 연결
2. Build Settings → Android → ARM64 / IL2CPP
3. `File → Build Settings → Build And Run`

---

## 📊 데이터 파이프라인

```
90Hz 시선 데이터 수집
    ↓
Angular Velocity → Fixation 감지 → 청크 생성
    ↓
JSON POST → FastAPI → S3 저장
    ↓
DOM 스냅샷 매칭 → 자동 라벨링
    ↓
Transformer 추론 → [focused / drowsy / disengaged]
    ↓
/tutor/check-state → Flutter 앱 분석 결과 제공
```

---

## 📝 개발 현황

- [x] Meta Quest Pro Eye/Face Tracking 연동
- [x] Intelligent Slicing (Angular Velocity 기반)
- [x] FastAPI 비동기 데이터 수신
- [x] Vuplex 웹 브라우저 기반 시선 추적 전환
- [x] 브라우저 픽셀 좌표 + URL 수집 구조
- [x] DOM 스냅샷 수집 시스템
- [x] 규칙 기반 자동 라벨링 설계
- [x] Flutter 모바일 앱 개발 (`true_study_tracker`)
- [x] `/tutor/check-state` API 설계 (Flutter 연동)
- [ ] Quest 빌드 실기기 테스트
- [ ] 자동 라벨링 파이프라인 검증
- [ ] Transformer 모델 학습 (데이터 수집 중)
- [ ] Gemini LLM 튜터 연동

---

## 👥 팀

| 역할 | 담당 |
|------|------|
| Unity / XR 개발 | @namepenz |
| FastAPI 백엔드 | 팀원 |
| AI 모델 학습 | 팀원 |

---

## 📅 개발 일지

### 2026-03-25 (화) — Vuplex 웹 브라우저 기반 시선 추적 시스템 전환

#### 목표
- 기존 StudyMaterial Plane(PDF 기반) → VR 웹 브라우저 기반으로 전환
- 브라우저 내 시선 좌표(픽셀) 수집 및 DOM 요소 매핑 구조 설계

#### 작업 내용

**1. Unity 씬 구성**
- Vuplex `CanvasWebViewPrefab`을 World Space Canvas에 배치
- Canvas 설정: 1920×1080, Scale 0.001, BoxCollider 추가 (시선 Raycast용)
- OVRCameraRig에 `OVRFaceExpressions` 컴포넌트 추가

**2. 스크립트 작성/수정 (3개)**
| 스크립트 | 역할 |
|---------|------|
| `GazeDataFeeder.cs` | 시선 → 브라우저 픽셀 좌표 변환, Angular Velocity 기반 청크 슬라이싱, FastAPI 전송 |
| `WebPanelController.cs` | Vuplex 초기화, URL 관리, JS 실행 (DOM 좌표 추출) |
| `DomSnapshotCollector.cs` | 주기적 DOM 요소 좌표 스냅샷 저장 (자동 라벨링용) |

**3. 데이터 구조 변경**
- `GazeDataPoint`에 `browser_pixel_x`, `browser_pixel_y`, `hit_canvas` 필드 추가
- `GazeChunk`에 `url` 필드 추가
- FastAPI `models.py`, `main.py` 동기화 수정

**4. 자동 라벨링 설계**
- 수집 흐름: 시선 픽셀 좌표 + DOM 스냅샷 → 규칙 기반 매칭
- 라벨 종류: `reading_text`, `reading_heading`, `viewing_image`, `scanning`, `deep_focus` 등 11개

#### 이슈 / 메모
- Unity 에디터에서 Vuplex는 Mock 모드로 동작 (Windows/macOS용 별도 라이선스 필요)
- 실제 테스트는 Quest 빌드로만 가능
- 서버 엔드포인트: `http://54.180.244.47:8000/ingest`

#### 다음 할 일
- [ ] Quest 빌드 후 브라우저 렌더링 + 시선 데이터 수집 테스트
- [ ] S3 저장 데이터 확인
- [ ] DOM 스냅샷 기반 자동 라벨링 파이프라인 실행
- [ ] 라벨링된 CSV 검증

---

### 2026-05-26 (화) — 시스템 고도화 및 캘리브레이션/튜터 기능 추가

#### 목표
- 눈 깜빡임(PERCLOS) 측정 기반 캘리브레이션 시스템 구축
- AI 튜터 매니저 및 상호작용 기능 설계
- VR 컨트롤러 레이저 포인터 연동 및 데이터 구조 개선

#### 작업 내용

**1. Unity 스크립트 추가/수정**
- `CalibrationManager.cs`: 안내 UI 제공 및 10초간 눈 깜빡임 측정, 서버로 맞춤형 기준값 전송 흐름 구현
- `TutorManager.cs`: AI 튜터 시스템 기본 구조 구축
- `ControllerLaserPointer.cs`: VR 환경 내 상호작용 강화를 위한 레이저 포인터 추가
- `GazeDataFeeder.cs`, `DomSnapshotCollector.cs`: 데이터 수집 최적화 및 픽셀 좌표 수집 로직 개선
- 레거시 코드 정리: `PdfCoordMapper.cs`, `PdfCoordMapper1.cs` 등 기존 PDF 기반 클래스 삭제

**2. FastAPI 백엔드 연동 변경**
- `models.py`: `GazeSample` 모델에 `browser_pixel_x`, `browser_pixel_y`, `hit_canvas` 필드 추가
- `main.py`: 수집된 `browser_pixel` 및 캔버스 히트 정보를 저장 포맷에 반영되도록 수정

#### 다음 할 일
- [ ] Quest 빌드 후 전체 파이프라인 실기기 테스트 진행
- [ ] AI 튜터 피드백 및 상호작용 시나리오 고도화
