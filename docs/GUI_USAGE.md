# capstone_sql 데스크톱 GUI 사용 및 개발 가이드

.NET 10 (LTS) 및 C# 14 기반의 **Uno Platform**으로 개발된 크로스플랫폼(Windows, Linux, macOS) 데스크톱 대화형 GUI 애플리케이션입니다.

Claude Desktop 및 Cursor와 유사한 대화형 멀티 세션(Multi-session Chat) 인터페이스를 제공하며, 자연어 질문으로부터 SQL을 생성하고, 5단계 계단식 후처리 전략을 비교하며, 연결된 SQLite 데이터베이스(`shop.db`)에 즉각 쿼리를 실행해 결과를 확인할 수 있습니다.

---

## 1. 주요 기능 명세

1. **Claude Desktop 스타일 멀티 세션 관리 (좌측 사이드바)**:
   - `+ 새 대화 (New Chat)` 버튼으로 독립된 컨텍스트의 대화방 생성
   - 대화방 제목 인라인 수정(✏️ 아이콘 클릭 또는 더블 클릭) 및 삭제(🗑️) 지원
   - **대화방별 에셋 독립 바인딩**: 각 대화방마다 서로 다른 모델(`.gguf`), DB(`.db`), 임베딩(`.bin`) 및 전략 설정을 개별적으로 유지
2. **대화형 말풍선 뷰 (메인 피드)**:
   - **사용자 질문**: 우측 정렬(Right-aligned) 둥근 모서리 사각형 말풍선
   - **모델 답변 (SQL 카드)**:
     - 문법 강조 다크 테마 코드 블록
     - **▶ Run (Play) 버튼**: 클릭 시 연결된 `shop.db`에 해당 SQL을 즉시 실행하고 인라인 테이블(DataGrid)로 결과 레코드 렌더링
     - **📋 Copy 버튼**: 클립보드에 생성된 SQL 원클릭 복사
     - **교정 내역 (Diff)**: 후처리 전 원본 SQL(`raw_sql`)과 교정본(`sql`) 비교 표시
     - **실행 메트릭**: 추론 시간(ms), 토큰 수, 중단 사유(`stop_reason`) 배지 표시
3. **채팅바 및 하단 컨트롤 바**:
   - **`[+]` 버튼 (좌측)**: CLI 호환 JSON 파일(`--json`) 가져오기 지원
   - **입력 필드**: `Enter`로 즉시 전송, `Shift+Enter`로 줄바꿈 지원
   - **모드 전환 토글 (하단)**: `[ 질의 모드 (Query) | 단독 후처리 모드 (Postprocess) ]` 원클릭 전환
   - **5단계 후처리 전략 선택**: `none` → `keyword` → `typo` → `typo,ngram` → `typo,ngram,semantic`
4. **상단 에셋 바 및 스키마 브라우저**:
   - `assets/` 디렉터리 내 최신 stage/contract 번들 자동 감지 및 로드
   - `DB 스키마 보기` 플라이아웃으로 SQLite 테이블 및 컬럼 구조 즉시 열람

---

## 2. 프로젝트 아키텍처

```text
src/
├── bindings/
│   └── SqlEngine.Core/          # C 코어(sql_engine.dll / libsql_engine.so) P/Invoke 바인딩 (.NET 10 / .NET 8)
└── gui/
    ├── runtimes/                # OS별 네이티브 바이너리 배치 경로
    │   ├── win-x64/native/sql_engine.dll
    │   ├── linux-x64/native/libsql_engine.so
    │   └── osx-arm64/native/libsql_engine.dylib
    └── SqlApp/                  # Uno Platform 단일 프로젝트 (.NET 10)
        ├── Converters/          # XAML 바인딩 값 변환기 (Diff, 경과 시간, 파일명 등)
        ├── Models/              # 대화 세션, 메시지, 쿼리 결과 데이터 모델 (CommunityToolkit.Mvvm)
        ├── Services/            # 자산 자동 탐색, SQLite 쿼리 실행기, C 추론기 래퍼
        ├── ViewModels/          # 반응형 MVVM MainViewModel
        ├── MainPage.xaml        # Claude Desktop 스타일 메인 UI
        └── Platforms/           # Desktop (Linux/macOS/Win32) 및 Windows(WinUI 3) 진입점
```

---

## 3. 빌드 및 실행 방법

### 1) 사전 준비 (C 코어 빌드)
GUI가 사용할 네이티브 C 공유 라이브러리(`sql_engine.dll` 또는 `libsql_engine.so`)를 먼저 빌드합니다:

```bash
# C 공유 라이브러리 빌드 (Release)
cmake -B build -DSQL_BUILD_SHARED=ON -DCMAKE_BUILD_TYPE=Release
cmake --build build --target sql_engine --config Release
```

### 2) GUI 애플리케이션 실행 (개발 모드)

```bash
# 1. Windows 11 기본 네이티브 앱 (WinUI 3 / Windows App SDK) 실행:
dotnet run --project src/gui/SqlApp -f net10.0-windows10.0.26100

# 2. 크로스플랫폼 데스크톱 (Skia Desktop: Linux, macOS, Windows 공용) 실행:
dotnet run --project src/gui/SqlApp -f net10.0-desktop
```

---

## 4. Unpackaged 독립 실행형(.exe / 바이너리) 배포 방법

MSIX 패키징이나 개발자 인증서 서명 없이, 더블 클릭만으로 바로 실행되는 단독 실행 바이너리로 배포합니다.

### 1) Windows용 일반 `.exe` 배포 (Non-MSIX / Unpackaged)
```powershell
# Windows x64 단독 실행 폴더로 퍼블리시
dotnet publish src/gui/SqlApp -f net10.0-desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```
* **출력 폴더**: `src/gui/SqlApp/bin/Release/net10.0-desktop/win-x64/publish/`
* 생성된 `SqlApp.exe`를 더블 클릭하여 바로 실행할 수 있습니다.

### 2) Linux용 데스크톱 배포 (Ubuntu / Debian / Fedora)
```bash
# Linux x64 단독 실행 바이너리로 퍼블리시
dotnet publish src/gui/SqlApp -f net10.0-desktop -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```
* GTK3 런타임(`libgtk-3-dev`)이 설치된 Linux 데스크톱 환경에서 `./SqlApp`으로 즉시 실행됩니다.

### 3) macOS용 데스크톱 배포 (Apple Silicon / Intel)
```bash
# macOS arm64 (M1~M4) 단독 실행 바이너리로 퍼블리시
dotnet publish src/gui/SqlApp -f net10.0-desktop -c Release -r osx-arm64 --self-contained
```

---

## 5. 의존성 및 보안 조치

* **`SQLitePCLRaw.bundle_e_sqlite3` (v3.0.5)**:
  - CVE-2025-6965(GHSA-2m69-gcr7-jv3q) 취약점이 해결된 SQLite 최신 엔진을 탑재하여 경고 없이 안전하게 동작합니다.
* **`CommunityToolkit.Mvvm`**:
  - Roslyn C# 소스 생성기 기반의 고성능 MVVM 아키텍처로 UI 반응성을 보장합니다.
* **`SqlEngine.Core` Bailout 방어**:
  - 한글 등 비ASCII 입력이나 잘못된 파일 경로 전달 시에도 C 코어가 `exit(1)`하지 않고 C# 예외로 안전하게 포획되어 UI 안내 메시지가 출력됩니다.
