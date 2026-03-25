#  Gaze-In — AI 기반 학습자 몰입 상태 분석 시스템

<p align="center">
  <img src="https://img.shields.io/badge/Unity-6000.3-black?logo=unity" />
  <img src="https://img.shields.io/badge/Meta_Quest_Pro-XR-blue?logo=meta" />
  <img src="https://img.shields.io/badge/FastAPI-0.100+-green?logo=fastapi" />
  <img src="https://img.shields.io/badge/PyTorch-2.0+-orange?logo=pytorch" />
  <img src="https://img.shields.io/badge/Python-3.10+-yellow?logo=python" />
</p>

<br/>

> **VR 환경에서 학습자의 시선(Eye)·안면(Face) 데이터를 실시간 분석하여**  
> **졸음 / 집중 / 이해불능 상태를 자동 판별하고, LLM 기반 AI 튜터가 즉각 개입하는 시스템**

---

## 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 프로젝트명 | Gaze-In (지능형 학습 몰입 케어 시스템) |
| 개발 기간 | 2025.03 ~ |
| 팀 구성 | 3인 (Unity / FastAPI / AI 모델) |
| 핵심 목표 | Meta Quest Pro의 90Hz 시선·안면 데이터로 학습 상태 실시간 판별 |

---

## 시스템 아키텍처

```
Meta Quest Pro (90Hz)
 ├─ Eye Tracking      → 시선 방향, 눈 떠짐
 └─ Face Tracking     → BlendShape 63채널 (표정 데이터)
         │
         ▼
 Unity C# (GazeDataFeeder)
 ├─ Intelligent Slicing: Angular Velocity 기반 Fixation 감지
 ├─ Fixation 시작 시점에 청크(Chunk) 생성
 └─ HTTP POST → FastAPI 서버 (비동기 Coroutine)
         │
         ▼
 FastAPI Backend
 ├─ Pydantic 데이터 파싱
 ├─ Transformer 모델 추론 (focused / drowsy / disengaged)
 └─ 상태 판별 결과 반환
         │
         ▼
 Gemini LLM AI 튜터
 └─ 학습 상태에 따른 능동 개입 (질문 생성 / 격려 / 경고)
```

---

## 핵심 기능

### 1. Intelligent Slicing
단순 시간 단위가 아닌 시선 각속도(Angular Velocity) 기반으로 Saccade/Fixation 구간을 감지합니다.
- Saccade(속도 급증): 시선 이동 중 → 노이즈 구간
- Fixation(속도 정체): 시선 고정 → 의미 있는 데이터 구간
- Fixation 시작 시점을 트리거로 데이터 청크 생성 → 데이터 품질 극대화

### 2. 멀티모달 데이터 수집
```
시선 방향 (left/right gaze direction)  →┐
눈 떠짐 정도 (PERCLOS 기반)            →┤→ Transformer 분류 모델
얼굴 BlendShape 63채널                 →┘
```

### 3. PDF 좌표 매핑
- PyMuPDF로 PDF 텍스트 좌표 추출
- Unity 3D Plane의 UV 좌표와 1:1 매핑
- 학습자가 어느 텍스트를 보다가 졸았는지 추적 가능

### 4. 실시간 상태 분류
| 상태 | 설명 |
|------|------|
| 🟢 Focused | 정상 집중 상태 |
| 🟡 Drowsy | 졸음 감지 (PERCLOS 기반) |
| 🔴 Disengaged | 몰입 이탈 상태 |

---

## 기술 스택

### Client (VR)
*Unity 6 (C#)
- Meta XR SDK (OVREyeGaze, OVRFaceExpressions)
- OpenXR (Meta Quest Pro)

### Server
- FastAPI (Python)
- PyTorch (Transformer 분류 모델)
- Pydantic (데이터 검증)
- Uvicorn (ASGI 서버)

### Data Processing
- PyMuPDF (PDF 좌표 추출)
- Newtonsoft.Json (Unity JSON 직렬화)

---

## 프로젝트 구조

```
GazeIn/
├── Assets/
│   └── Scripts/
│       ├── GazeDataFeeder.cs     # 시선 데이터 수집 및 전송
│       └── PdfCoordMapper.cs     # PDF ↔ Unity 좌표 매핑
├── gazein_backend/
│   ├── main.py                   # FastAPI 엔드포인트
│   ├── models.py                 # Pydantic 데이터 모델
│   └── inference.py              # Transformer 추론 모듈
└── pytest/
    ├── pdf_extractor.py          # PDF 텍스트 좌표 추출
    └── text_coords.json          # 추출된 좌표 데이터
```

---

## 실행 방법

### FastAPI 서버 실행
```bash
cd gazein_backend
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

### PDF 좌표 추출
```bash
pip install pymupdf
python pytest/pdf_extractor.py "학습자료.pdf"
# → text_coords.json 생성
```

### Unity 빌드
1. Meta Quest Pro USB 연결
2. `File → Build Settings → Build And Run`

---

## 데이터 파이프라인

```
90Hz 시선 데이터 수집
    ↓
Angular Velocity 계산
    ↓
Saccade / Fixation 판별
    ↓
Fixation 시작 시 청크 생성 (평균 15~50 샘플)
    ↓
JSON 직렬화 → HTTP POST
    ↓
FastAPI 수신 → Transformer 추론
    ↓
[focused / drowsy / disengaged] 상태 반환
    ↓
Gemini AI 튜터 개입
```

---

| 역할 | 담당 |
|------|------|
| Unity / XR 개발 | @namepenz |
| FastAPI 백엔드 | @namepenz |
| AI 모델 학습 | @namepenz |

---

## 개발 현황

- [x] Meta Quest Pro Eye/Face Tracking 연동
- [x] Intelligent Slicing (Angular Velocity 기반)
- [x] FastAPI 비동기 데이터 수신
- [x] PDF ↔ Unity 좌표 매핑
- [x] Vuplex 웹 브라우저 기반 시선 추적 전환
- [x] 브라우저 픽셀 좌표 + URL 수집 구조
- [x] DOM 스냅샷 수집 시스템
- [x] 규칙 기반 자동 라벨링 설계
- [ ] Quest 빌드 실기기 테스트
- [ ] 자동 라벨링 파이프라인 실행 및 검증
- [ ] Transformer 모델 학습 (데이터 수집 중)
- [ ] Gemini LLM 튜터 연동

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

