# Git Tab — 프로덕션급 로드맵

현재 상태(v0.5.0): 기능은 풍부하고 68개 회귀 테스트 통과, 릴리즈 파이프라인·설치본·자동 업데이트·번들 Git까지 갖춘 상태. 이 문서는 **"기능 완성" → "안심하고 배포·유지보수하는 제품"** 사이에 남은 격차를 우선순위별로 정리한다. 각 항목은 실제 코드 근거와 대략적 노력(작음/보통/큼)을 포함한다.

---

## P0 — 공개 배포 전 필수 (신뢰·보안 차단 요소)

### 1. 코드 서명 (Authenticode) — *가장 큰 차단 요소*
- **현상**: 설치본·exe가 서명되어 있지 않음 → 사용자가 받으면 Windows **SmartScreen "알 수 없는 게시자"** 경고. 다운로드 앱의 채택을 가장 크게 막는 요소.
- **필요**: OV 또는 EV 코드서명 인증서 구매 → `signtool`로 `GitTab.exe`와 `GitTab-Setup-*.exe` 서명(타임스탬프 포함). release.yml에 서명 단계 추가(인증서는 GitHub Secret).
- **부수 효과**: 서명된 설치본은 자동 업데이트의 무결성도 보증(아래 2번 일부 해결).
- 노력: 보통(+인증서 비용/조달).

### 2. 자동 업데이트 무결성 검증
- **현상**: `GitHubUpdateService.DownloadInstallerAsync`가 HTTPS로 설치본을 받아 `LaunchInstallerAndExit`가 **검증 없이 바로 실행**. TLS로 전송은 보호되지만, 다운로드 산출물의 서명/해시 확인이 없음.
- **필요**: (a) 실행 전 **Authenticode 서명 검증**(WinVerifyTrust), 그리고/또는 (b) 릴리즈에 게시한 **SHA-256 체크섬** 대조. 서명 주체가 예상과 다르면 실행 거부.
- 노력: 보통. (1번 코드 서명과 함께 하면 자연스럽다.)

### 3. 인자 주입(argument injection) 하드닝
- **현상**: `RepositoryService`의 쓰기 명령이 사용자 제어 ref/이름을 **`--` 구분자 없이** 전달. 예: `checkout <branch>`, `merge <branch>`, `rebase <branch>`, `reset <mode> <sha>`, `branch -m <old> <new>`, `tag -d <name>`, `push <remote> --delete <branch>`. `-`로 시작하는 이름이 플래그로 해석될 여지.
- **필요**: 위치 인자 앞에 `--` 삽입(예: `checkout <branch> --`, 또는 `-- <ref>`), 그리고 ref/원격/경로 이름 검증(`^-`, 제어문자, 공백 규칙). shell을 안 거치므로 셸 주입은 없지만 **git 플래그 주입**은 막아야 함.
- 노력: 작음(핵심), + 검증 유틸.

---

## P1 — 신뢰성·보안 하드닝 (배포 직후 우선)

### 4. 침묵 실패(silent failure) 감사
- 다수의 `catch { }` / `catch(...) { return null; }`가 오류를 삼킴(자격증명 저장, 임시파일 삭제, 브라우저 열기, blob 읽기 등). 사용자 피드백/로그가 필요한 지점과 진짜 무해한 지점을 구분해 정리.
- 노력: 보통.

### 5. 이벤트 핸들러 누수 점검
- 커스텀 컨트롤이 `LocalizationService.Current.LanguageChanged`, `ThemeService.ThemeChanged` 등 **정적 이벤트를 구독하고 해지하지 않음**. 현재는 단일 인스턴스라 실질 누수는 작지만, 대화상자 등에서 재생성되는 구독 주체는 누수 위험. 약한 이벤트(WeakEventManager) 또는 Unloaded 해지로 정리.
- 노력: 작음~보통.

### 6. 네트워크/장기 작업 취소·타임아웃 UX
- Fetch/Pull/Push/Clone 등은 취소 버튼이 없음(러너에 5분 타임아웃만 존재). 진행률·취소 가능한 UI(CancellationToken 연결) 필요.
- 노력: 보통.

### 7. 입력·경로·인코딩 엣지케이스
- 비-UTF8 파일, CRLF/LF, 심볼릭 링크, 공백·유니코드 경로, 대용량/바이너리 파일 diff, detached HEAD, bare repo, 얕은/부분 클론에서의 동작 검증. 대용량 파일 diff 가드(수 MB 초과 시 생략).
- 노력: 보통~큼.

### 8. 스레드 안전성·리소스 수명
- `RepositoryService`는 `_sync`로 직렬화되어 있으나, 백그라운드 통계 캐시·업데이트·askpass 프로세스 수명 재점검. `Process`/`HttpClient`/`Repository` 해제 경로 확인.
- 노력: 작음~보통.

---

## P2 — 품질·테스트·엔지니어링 프로세스

### 9. 테스트 커버리지 심화
- 현재 68개 중 App 계층은 **순수 헬퍼(CredentialKey/GitAuth/RemoteWeb)** 위주. **ViewModel 오케스트레이션(MainViewModel/WorkingCopy/Branches)**은 미검증.
- **필요**: 목킹 프레임워크(**NSubstitute** — OSS 라이선스 안전) 도입, `IRepositoryService`/`IDialogService` 목으로 VM 로직 테스트. 커버리지 리포트(coverlet → CI 게시).
- 선택: **FlaUI** 기반 WPF UI 스모크(E2E) 테스트.
- 노력: 큼(가치 큼).

### 10. CI 강화
- 현재 build.yml + release.yml만 존재. 추가:
  - **Roslyn 분석기 + nullable/경고를 오류로**(`TreatWarningsAsErrors`), `dotnet format --verify-no-changes`(포맷 게이트).
  - **CodeQL**(보안 정적 분석), **Dependabot**(의존성/보안 업데이트).
  - 커버리지 업로드, PR 필수 체크, 브랜치 보호.
- 노력: 보통.

### 11. 로깅·관측성·크래시 제출
- Serilog 파일 로깅은 있음. 크래시 리포트는 로컬 저장까지만 → **선택적 이슈 제출/복사** 흐름 추가. (옵트인) 익명 사용 지표는 신중히(개인정보 최소화)만 고려.
- 노력: 작음~보통.

### 12. 번들 Git·런타임 버전 관리
- MinGit은 릴리즈 시 "latest"를 받음 → **버전 고정 + 무결성 확인**으로 재현성 확보. .NET 런타임 요구사항 안내 유지.
- 노력: 작음.

---

## P3 — 기능 완성도·UX·접근성 심화

### 13. Git 기능 공백
- 파일 히스토리 / 라인 히스토리, `git bisect` UI, 커밋 reword, worktree 관리, 서브모듈 리치 UI, **Git LFS**, sparse-checkout, GPG/SSH 서명, 태그 주석 편집, pickaxe(내용) 검색.
- 노력: 항목별 보통~큼.

### 14. Diff·그래프 심화
- diff **단어 단위 하이라이트**, 구문 강조, 이미지 diff, 대용량 파일 처리. 그래프 필터(작성자/날짜/브랜치), 두 커밋 경로 하이라이트, 미니맵.
- 노력: 보통~큼.

### 15. 접근성 2차
- 커밋 그래프 **항목 단위 AutomationPeer**(현재는 컨테이너+알림만), 고대비 테마, 키보드 전용 완전 조작, 포커스 관리, 폰트 배율.
- 노력: 큼.

### 16. 국제화 확장
- ko/en 외 언어, 로케일별 날짜/숫자, 복수형 처리. (RTL은 후순위.)
- 노력: 보통.

### 17. 온보딩·UX 일관성
- 최초 실행 가이드, 빈/로딩/오류 상태 일관성, 모든 파괴적 작업의 확인·되돌리기 경로, 고DPI/멀티모니터 검증.
- 노력: 보통.

---

## 권장 착수 순서

1. **P0 전부** — 코드 서명(1) → 업데이트 검증(2) → 인자 주입 하드닝(3). 공개 배포 신뢰의 3종.
2. **P1의 4·5·6** — 침묵 실패 감사, 이벤트 누수, 취소 UX.
3. **P2의 9·10** — 목 기반 VM 테스트 + CI 분석기/CodeQL. 이후의 모든 변경을 안전하게 만드는 토대.
4. 이후 P3를 가치·수요 순으로.

> 코드 서명 인증서 조달은 리드타임이 있으니 **1번을 가장 먼저 시작**하고, 그 사이 3(인자 주입)·9·10을 병행하는 것을 권장.
