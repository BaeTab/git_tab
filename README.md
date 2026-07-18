# Git Tab

**A modern Windows Git client with a color-coded commit graph, built in .NET 8 & WPF.**

[English](#english) | [한국어](#한국어)

---

## English

[![Build Status](https://github.com/BaeTab/git_tab/actions/workflows/build.yml/badge.svg)](https://github.com/BaeTab/git_tab/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### What is Git Tab?

Git Tab is a native Windows Git client that brings the power of **visual branch management** to your fingertips. Like GitKraken and SourceTree, Git Tab renders commits as an interactive, color-coded graph where branches "braid" together—making it easy to understand your repository's history at a glance. Built entirely in C# with WPF, Git Tab is lightweight, fast, and deeply integrated with Windows.

### Features

- **🎨 Color Commit Graph** — Branches are assigned distinct colors; commits line up in a virtualized, interactive graph that handles tens of thousands of commits without lag.
- **📖 Commit Details & Unified Diff** — Click any commit to see its message, author, date, and a full syntax-highlighted diff view (powered by AvalonEdit).
- **💾 Working Directory Staging** — Stage/unstage individual files, inspect changes, and commit (or amend) from the UI.
- **🌿 Branch Operations** — Checkout, create, delete, rename, merge, and rebase branches directly from the graph.
- **🔗 Remote Management** — Fetch, pull, and push to multiple remotes. Ahead/behind tracking and integrated credential handling via the system git credential helper.
- **🔍 Commit Search** — Filter commits by message, author, or hash in real time.
- **🌙 Light & Dark Themes** — Toggle between themes without restarting.
- **🇬🇧 🇰🇷 Bilingual UI** — Switch between English and Korean at runtime; tooltips on every action explain Git concepts for beginners.
- **⚡ Auto-Update** — The app checks GitHub Releases and downloads new versions with a single click.

### Screenshots

**Dark Theme:**
![Main Window (Dark)](/docs/screenshots/main-dark.png)

**Light Theme:**
![Main Window (Light)](/docs/screenshots/main-light.png)

### Requirements

- **Windows 10 or Windows 11**
- **.NET 8 Desktop Runtime** ([download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0))
- **Git** installed and available on PATH (`git --version` should work in cmd)

### Build from Source

#### 1. Clone the Repository

```bash
git clone https://github.com/BaeTab/git_tab.git
cd git_tab
```

#### 2. Build the Solution

```bash
dotnet build GitTab.sln -c Release
```

#### 3. Run Tests

```bash
dotnet test GitTab.sln
```

#### 4. Run the App

```bash
dotnet run --project src/GitTab.App
```

#### Solution Structure

```
GitTab.sln
├── src/
│   ├── GitTab.Core/              # Domain models, git adapters (LibGit2Sharp, git.exe)
│   ├── GitTab.Graph/             # Pure lane-layout algorithm for the commit graph
│   ├── GitTab.Graph.Cli/         # ASCII harness for graph validation
│   └── GitTab.App/               # WPF application (MVVM via CommunityToolkit.Mvvm)
└── tests/
    ├── GitTab.Graph.Tests/       # Unit tests for lane algorithm
    └── GitTab.Core.Tests/        # Unit tests for domain & adapters
```

### Architecture

Git Tab is split into clean, testable layers:

- **GitTab.Core** — Domain models (Repository, Commit, Branch, Remote, etc.) and two Git adapters:
  - *LibGit2Sharp* adapter for read-only operations (fast, in-process)
  - *git.exe* facade for all writes and network operations (push, pull, merge, rebase, etc.)
- **GitTab.Graph** — A pure, UI-agnostic algorithm that arranges commits into colored lanes. No WPF or external dependencies; easily unit-tested.
- **GitTab.App** — A WPF application using MVVM (via `CommunityToolkit.Mvvm`) that wires up the domain, graph algorithm, and UI. Dependency injection via `Microsoft.Extensions.DependencyInjection`; logging via Serilog.

This separation ensures that Core and Graph are fully testable in isolation and can be reused in other clients (CLI, web, etc.).

### Running the Tests

```bash
dotnet test GitTab.sln
```

Tests use **xUnit** and **FluentAssertions** for readable, expressive test code. Coverage includes the lane-layout algorithm (10+ unit tests), domain models, and adapter behavior.

### Install

Download the latest **InnoSetup installer** from the [GitHub Releases page](https://github.com/BaeTab/git_tab/releases) and run it. The installer will:
- Install Git Tab to `Program Files\Git Tab`
- Create a Start Menu shortcut
- Verify that .NET 8 Desktop Runtime is installed

**Auto-Update:** After installation, Git Tab will periodically check GitHub Releases. When a new version is available, you'll be prompted to download and install it with a single click.

### Third-Party Licenses

Git Tab depends on several excellent open-source projects. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for full details and license texts.

| Package | Version | License |
|---------|---------|---------|
| LibGit2Sharp | 0.31.0 | MIT |
| CommunityToolkit.Mvvm | 8.4.0 | MIT |
| ICSharpCode.AvalonEdit | 6.3.0.90 | MIT |
| Microsoft.Extensions.DependencyInjection | 8.0.x | MIT |
| Microsoft.Extensions.Logging | 8.0.x | MIT |
| Serilog | 4.2.0 | Apache-2.0 |
| Serilog.Extensions.Logging | 8.0.0 | Apache-2.0 |
| Serilog.Sinks.File | 6.0.0 | Apache-2.0 |

### License

Git Tab is released under the **MIT License**. See [LICENSE](LICENSE) for details.

© 2026 Hyun-woo Bae (H.Soft)

---

## 한국어

[![Build Status](https://github.com/BaeTab/git_tab/actions/workflows/build.yml/badge.svg)](https://github.com/BaeTab/git_tab/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Git Tab란?

Git Tab는 **시각적 브랜치 관리**의 힘을 제공하는 네이티브 Windows Git 클라이언트입니다. GitKraken과 SourceTree처럼, Git Tab는 커밋을 색상으로 구분된 인터랙티브 그래프로 렌더링하며, 브랜치들이 함께 "꼰다(braid)"는 개념으로 저장소의 히스토리를 한눈에 파악할 수 있습니다. C#과 WPF로 완전히 작성되었으며 가볍고 빠르며 Windows와 깊게 통합됩니다.

### 주요 기능

- **🎨 색상 커밋 그래프** — 각 브랜치에 고유 색상을 할당; 커밋들이 가상화된 그래프에 정렬되어 수만 개의 커밋도 지연 없이 처리합니다.
- **📖 커밋 상세 & 통합 Diff** — 커밋을 클릭하면 메시지, 작성자, 날짜, 그리고 구문 강조된 전체 diff를 볼 수 있습니다(AvalonEdit 기반).
- **💾 작업 디렉토리 스테이징** — 파일을 개별 스테이징/언스테이징하고, 변경 사항을 검사한 후 커밋(또는 수정)할 수 있습니다.
- **🌿 브랜치 작업** — 브랜치를 체크아웃, 생성, 삭제, 이름 변경, 병합, 리베이스합니다.
- **🔗 원격 저장소 관리** — 여러 원격에 대해 Fetch, Pull, Push를 수행합니다. Ahead/Behind 추적 및 시스템 Git 자격증명 도우미와의 통합.
- **🔍 커밋 검색** — 메시지, 작성자, 커밋 해시로 실시간 필터링합니다.
- **🌙 라이트 & 다크 테마** — 재시작 없이 테마를 전환합니다.
- **🇬🇧 🇰🇷 이중 언어 UI** — 런타임에 영어와 한국어 간 전환; 모든 Git 작업에 초보자 친화적 설명이 담긴 툴팁을 제공합니다.
- **⚡ 자동 업데이트** — GitHub Releases를 확인하고 새 버전을 한 클릭으로 다운로드합니다.

### 스크린샷

**다크 테마:**
![Main Window (Dark)](/docs/screenshots/main-dark.png)

**라이트 테마:**
![Main Window (Light)](/docs/screenshots/main-light.png)

### 시스템 요구사항

- **Windows 10 또는 Windows 11**
- **.NET 8 Desktop Runtime** ([다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/8.0))
- **Git** 설치 및 PATH에 등록 (`git --version`이 cmd에서 작동해야 함)

### 소스에서 빌드

#### 1. 저장소 복제

```bash
git clone https://github.com/BaeTab/git_tab.git
cd git_tab
```

#### 2. 솔루션 빌드

```bash
dotnet build GitTab.sln -c Release
```

#### 3. 테스트 실행

```bash
dotnet test GitTab.sln
```

#### 4. 앱 실행

```bash
dotnet run --project src/GitTab.App
```

#### 솔루션 구조

```
GitTab.sln
├── src/
│   ├── GitTab.Core/              # 도메인 모델, git 어댑터 (LibGit2Sharp, git.exe)
│   ├── GitTab.Graph/             # 커밋 그래프 레인 배치 알고리즘 (순수 함수)
│   ├── GitTab.Graph.Cli/         # 그래프 검증용 ASCII 도구
│   └── GitTab.App/               # WPF 애플리케이션 (CommunityToolkit.Mvvm MVVM)
└── tests/
    ├── GitTab.Graph.Tests/       # 레인 배치 알고리즘 단위 테스트
    └── GitTab.Core.Tests/        # 도메인 & 어댑터 단위 테스트
```

### 아키텍처

Git Tab는 깔끔하고 테스트 가능한 계층으로 분리됩니다:

- **GitTab.Core** — 도메인 모델(Repository, Commit, Branch, Remote 등)과 두 가지 Git 어댑터:
  - *LibGit2Sharp* 어댑터: 읽기 전용 작업 (빠르고 in-process)
  - *git.exe* 파사드: 모든 쓰기 및 네트워크 작업 (push, pull, merge, rebase 등)
- **GitTab.Graph** — 순수하고 UI에 무관한 알고리즘으로 커밋을 색상 레인으로 정렬합니다. WPF나 외부 의존성 없음; 단위 테스트 가능.
- **GitTab.App** — MVVM을 사용하는 WPF 애플리케이션(`CommunityToolkit.Mvvm`으로 구성). `Microsoft.Extensions.DependencyInjection`으로 DI; Serilog으로 로깅.

이러한 분리는 Core와 Graph가 완전히 격리된 상태에서 테스트 가능하며 다른 클라이언트(CLI, 웹 등)에서 재사용 가능하도록 합니다.

### 테스트 실행

```bash
dotnet test GitTab.sln
```

테스트는 **xUnit**과 **FluentAssertions**을 사용하여 읽기 쉽고 표현력 있는 테스트 코드를 작성합니다. 레인 배치 알고리즘(10개 이상의 단위 테스트), 도메인 모델, 어댑터 동작을 포함합니다.

### 설치

[GitHub Releases 페이지](https://github.com/BaeTab/git_tab/releases)에서 최신 **InnoSetup 설치 프로그램**을 다운로드하고 실행합니다. 설치 프로그램은 다음을 수행합니다:
- Git Tab를 `Program Files\Git Tab`에 설치
- 시작 메뉴 바로가기 생성
- .NET 8 Desktop Runtime이 설치되어 있는지 확인

**자동 업데이트:** 설치 후 Git Tab는 정기적으로 GitHub Releases를 확인합니다. 새 버전이 있으면 한 번의 클릭으로 다운로드 및 설치할 수 있습니다.

### 제3자 라이센스

Git Tab는 여러 우수한 오픈소스 프로젝트에 의존합니다. 자세한 내용과 라이센스 텍스트는 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)를 참조하세요.

| 패키지 | 버전 | 라이센스 |
|--------|------|---------|
| LibGit2Sharp | 0.31.0 | MIT |
| CommunityToolkit.Mvvm | 8.4.0 | MIT |
| ICSharpCode.AvalonEdit | 6.3.0.90 | MIT |
| Microsoft.Extensions.DependencyInjection | 8.0.x | MIT |
| Microsoft.Extensions.Logging | 8.0.x | MIT |
| Serilog | 4.2.0 | Apache-2.0 |
| Serilog.Extensions.Logging | 8.0.0 | Apache-2.0 |
| Serilog.Sinks.File | 6.0.0 | Apache-2.0 |

### 라이센스

Git Tab는 **MIT 라이센스** 하에 배포됩니다. 자세한 내용은 [LICENSE](LICENSE)를 참조하세요.

© 2026 Hyun-woo Bae (H.Soft)
