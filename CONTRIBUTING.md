# Contributing to Git Tab

Thank you for considering contributing to Git Tab! We welcome bug reports, feature requests, and code contributions. This guide will help you get started.

## Getting Started

### Prerequisites

- **Windows 10 or Windows 11**
- **.NET 8 SDK** ([download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0))
- **Git** installed and available on PATH (verify with `git --version` in cmd)

### Setup

1. Fork and clone the repository:
   ```bash
   git clone https://github.com/YOUR_USERNAME/git_tab.git
   cd git_tab
   ```

2. Build the solution:
   ```bash
   dotnet build GitTab.sln -c Release
   ```

3. Run the tests to ensure everything works:
   ```bash
   dotnet test GitTab.sln
   ```

4. Launch the app:
   ```bash
   dotnet run --project src/GitTab.App
   ```

## Project Layout

- **GitTab.Core** — Domain models (Repository, Commit, Branch, etc.) and Git adapters (LibGit2Sharp for reads; git.exe facade for writes/network operations). No WPF dependencies.
- **GitTab.Graph** — Pure, UI-agnostic lane-layout algorithm for rendering the commit graph. Fully unit-tested.
- **GitTab.App** — WPF UI layer using MVVM (CommunityToolkit.Mvvm). Dependency injection via Microsoft.Extensions.DependencyInjection.
- **tests/** — Unit tests using xUnit and FluentAssertions.

## Workflow

1. **Create a feature branch** from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Keep the build and tests green**:
   - After each commit, verify: `dotnet build GitTab.sln -c Release`
   - Ensure all tests pass: `dotnet test GitTab.sln`
   - Target zero build warnings.

3. **Push and open a pull request** against `main`.

4. **CI must pass** — GitHub Actions will run the `build` workflow on every push. All checks must pass before merging.

## Coding Conventions

### C# Style

- **Nullable reference types enabled** — use `string?` for nullable, `string` for non-nullable. Check with `is not null`.
- **File-scoped namespaces** — place `namespace GitTab.Core.Models;` at the top (no braces).
- **No build warnings** — treat warnings as errors in CI; fix them immediately.

### Architecture

- **Core and Graph must never reference** `System.Windows` or `PresentationFramework`. Keep them UI-agnostic.
- **Git operations** — Use `LibGit2Sharp` for reads (open repository, inspect commits, branches). Use `IGitCommandRunner` (delegates to `git.exe`) for all writes, network operations, and complex workflows (merge, rebase, push, pull).

### MVVM and UI

- **CommunityToolkit.Mvvm** — Use `[ObservableProperty]` and `[RelayCommand]` attributes in partial classes inheriting `ObservableObject`. **No business logic in code-behind** (`.xaml.cs`); only view concerns belong there.
- **Localization** — All user-facing strings go through `ILocalizationService`. Add both `ko` (Korean) and `en` (English) entries in `LocalizationService.cs` for any new UI text.

### Testing

- Aim for high unit test coverage on domain logic, adapters, and algorithms.
- Use xUnit and FluentAssertions for readable assertions.
- Place tests in `tests/GitTab.Core.Tests` or `tests/GitTab.Graph.Tests` as appropriate.

## Commit Messages

- **Subject (first line):** English prefix + concise description. Use: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`.
  - Example: `feat: add interactive rebase planner`
  - Example: `fix: resolve conflict detection race condition`
  
- **Body (optional):** May be in Korean or English; explain *why*, not *what*.

- **No AI/assistant signatures** — do not include `Co-Authored-By` or `Claude-Session` lines.

## Reporting Issues

- **Bugs:** Open a GitHub Issue with:
  - Steps to reproduce
  - Expected behavior
  - Actual behavior
  - App version (visible in the title bar or Help menu)
  
- **Features:** Open a GitHub Issue with a clear description and use case.

Visit [Issues](https://github.com/BaeTab/git_tab/issues) to browse or file a new one.

## License

Contributions are accepted under the [MIT License](LICENSE). By contributing, you agree that your code will be licensed under MIT.

---

# Git Tab 컨트리뷰팅 가이드

Git Tab에 기여해주실 관심에 감사합니다! 버그 리포트, 기능 요청, 코드 기여를 환영합니다. 이 가이드가 시작을 도와드리겠습니다.

## 시작하기

### 필수 요구사항

- **Windows 10 또는 Windows 11**
- **.NET 8 SDK** ([다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/8.0))
- **Git** 설치 및 PATH 등록 (cmd에서 `git --version` 확인)

### 설정

1. 저장소를 포크하고 복제:
   ```bash
   git clone https://github.com/YOUR_USERNAME/git_tab.git
   cd git_tab
   ```

2. 솔루션 빌드:
   ```bash
   dotnet build GitTab.sln -c Release
   ```

3. 모든 것이 정상인지 확인하려면 테스트를 실행:
   ```bash
   dotnet test GitTab.sln
   ```

4. 앱 실행:
   ```bash
   dotnet run --project src/GitTab.App
   ```

## 프로젝트 구조

- **GitTab.Core** — 도메인 모델(Repository, Commit, Branch 등)과 Git 어댑터(LibGit2Sharp 읽기 전용, git.exe 파사드 쓰기/네트워크). WPF 의존성 없음.
- **GitTab.Graph** — 순수 UI-무관 알고리즘으로 커밋 그래프 렌더링 레인 배치. 완전 단위 테스트.
- **GitTab.App** — MVVM을 사용하는 WPF UI 계층 (CommunityToolkit.Mvvm). Microsoft.Extensions.DependencyInjection으로 의존성 주입.
- **tests/** — xUnit과 FluentAssertions를 사용하는 단위 테스트.

## 워크플로우

1. **`main`에서 피처 브랜치 생성**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **빌드와 테스트 유지**:
   - 각 커밋 후 확인: `dotnet build GitTab.sln -c Release`
   - 모든 테스트 통과 확인: `dotnet test GitTab.sln`
   - 빌드 경고 0개 목표.

3. **푸시 및 `main`을 대상으로 PR 오픈**.

4. **CI 통과 필수** — GitHub Actions가 모든 푸시에서 `build` 워크플로우를 실행합니다. 병합 전 모든 검사를 통과해야 합니다.

## 코딩 규약

### C# 스타일

- **Nullable 참조 타입 활성화** — nullable은 `string?`, 비-nullable은 `string`. `is not null`로 검사.
- **파일 범위 네임스페이스** — 맨 위에 `namespace GitTab.Core.Models;` 배치 (중괄호 없음).
- **빌드 경고 없음** — CI에서는 경고를 오류로 취급; 즉시 수정.

### 아키텍처

- **Core와 Graph는 절대 `System.Windows` 또는 `PresentationFramework` 참조 금지**. UI-무관 유지.
- **Git 작업** — 읽기는 `LibGit2Sharp` 사용(저장소 열기, 커밋/브랜치 검사). 쓰기·네트워크·복잡한 워크플로우(병합, 리베이스, 푸시, 풀)는 `IGitCommandRunner`(`git.exe` 위임) 사용.

### MVVM과 UI

- **CommunityToolkit.Mvvm** — `ObservableObject` 상속 partial 클래스에서 `[ObservableProperty]`와 `[RelayCommand]` 속성 사용. **코드비하인드(`.xaml.cs`)에 비즈니스 로직 금지**; 뷰 관심사만 배치.
- **지역화** — 모든 사용자 화면 텍스트는 `ILocalizationService`를 통해. 새 UI 텍스트는 `LocalizationService.cs`에 `ko`(한국어)와 `en`(영어) 항목 추가.

### 테스트

- 도메인 로직, 어댑터, 알고리즘에 높은 단위 테스트 커버리지 목표.
- xUnit과 FluentAssertions으로 읽기 쉬운 어설션.
- `tests/GitTab.Core.Tests` 또는 `tests/GitTab.Graph.Tests`에 테스트 배치.

## 커밋 메시지

- **주제(첫 번째 줄):** 영문 prefix + 간결한 설명. 사용: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`.
  - 예: `feat: 대화형 리베이스 플래너 추가`
  - 예: `fix: 충돌 감지 race condition 해결`
  
- **본문(선택):** 한국어 또는 영문; *무엇*이 아닌 *왜*를 설명.

- **AI/어시스턴트 서명 금지** — `Co-Authored-By` 또는 `Claude-Session` 라인 포함 금지.

## 이슈 보고

- **버그:** GitHub Issue를 열고 다음 포함:
  - 재현 단계
  - 예상 동작
  - 실제 동작
  - 앱 버전 (제목 표시줄 또는 도움말 메뉴에 표시)
  
- **기능:** GitHub Issue를 열고 명확한 설명과 사용 사례 포함.

[Issues](https://github.com/BaeTab/git_tab/issues) 페이지를 방문하여 검색하거나 새로 열어보세요.

## 라이센스

기여는 [MIT 라이센스](LICENSE) 하에 수락됩니다. 기여함으로써 귀사의 코드가 MIT로 라이센스될 것에 동의합니다.
