# Release Guide / 릴리즈 가이드

This document explains how to cut a new release of Git Tab, both via the
automated GitHub Actions pipeline and manually on a local machine.

이 문서는 Git Tab의 새 릴리즈를 만드는 방법을 설명합니다. GitHub Actions
자동화 파이프라인을 이용하는 방법과, 로컬에서 수동으로 패키징하는 방법을
모두 다룁니다.

---

## 1. Cutting a release (automated) / 릴리즈 만들기 (자동화)

**EN**

1. Bump the version in two places so they stay in sync:
   - `<Version>` (and `<AssemblyVersion>` / `<FileVersion>` /
     `<InformationalVersion>`) in `src/GitTab.App/GitTab.App.csproj`.
   - `AppVersion` fallback in `installer/GitTab_installer.iss` (used only
     when the CI override is not supplied; keep it matching for local
     builds).
2. Commit the version bump:
   ```bash
   git add src/GitTab.App/GitTab.App.csproj installer/GitTab_installer.iss
   git commit -m "chore(release): bump version to X.Y.Z"
   ```
3. Tag and push:
   ```bash
   git tag vX.Y.Z
   git push --tags
   ```
4. Pushing a `vX.Y.Z` tag triggers `.github/workflows/release.yml`, which:
   - Runs the test suite (`dotnet test GitTab.sln -c Release`).
   - Publishes a framework-dependent single-file build
     (`dotnet publish ... -o publish`), producing `publish/GitTab.exe`.
   - Installs Inno Setup and compiles `installer/GitTab_installer.iss` with
     `/DAppVersion=X.Y.Z`, producing `dist/GitTab-Setup-X.Y.Z.exe`.
   - Creates a GitHub Release for the tag and uploads
     `dist/GitTab-Setup-X.Y.Z.exe` as a release asset, with auto-generated
     release notes.
5. The in-app auto-updater checks the **latest** GitHub release, finds the
   asset whose filename ends in `.exe` (i.e. `GitTab-Setup-X.Y.Z.exe`),
   downloads it, and runs it. No further steps are needed — as soon as the
   release is published, users on older versions will be offered the
   update.

You can also trigger the release workflow manually from the Actions tab
(`workflow_dispatch`) by supplying a `version` input (without the leading
`v`) — useful for re-running a release without re-tagging.

**KR**

1. 아래 두 곳의 버전을 함께 올립니다.
   - `src/GitTab.App/GitTab.App.csproj`의 `<Version>`
     (`<AssemblyVersion>` / `<FileVersion>` / `<InformationalVersion>` 포함)
   - `installer/GitTab_installer.iss`의 `AppVersion` 기본값 (CI에서 값을
     덮어쓰지 않을 때만 사용되므로, 로컬 빌드를 위해 동일하게 맞춰둡니다)
2. 버전 변경 사항을 커밋합니다.
   ```bash
   git add src/GitTab.App/GitTab.App.csproj installer/GitTab_installer.iss
   git commit -m "chore(release): bump version to X.Y.Z"
   ```
3. 태그를 생성하고 푸시합니다.
   ```bash
   git tag vX.Y.Z
   git push --tags
   ```
4. `vX.Y.Z` 태그가 푸시되면 `.github/workflows/release.yml`이 실행되어
   다음을 자동으로 수행합니다.
   - 테스트 실행 (`dotnet test GitTab.sln -c Release`)
   - 프레임워크 종속(단일 파일) 게시 (`dotnet publish ... -o publish`) →
     `publish/GitTab.exe` 생성
   - Inno Setup 설치 후 `/DAppVersion=X.Y.Z` 옵션으로
     `installer/GitTab_installer.iss` 컴파일 → `dist/GitTab-Setup-X.Y.Z.exe`
     생성
   - 해당 태그에 대한 GitHub Release 생성 및 `dist/GitTab-Setup-X.Y.Z.exe`를
     릴리즈 자산으로 업로드 (릴리즈 노트는 자동 생성)
5. 앱 내 자동 업데이트 기능은 **최신** GitHub 릴리즈를 확인하여 파일명이
   `.exe`로 끝나는 자산(`GitTab-Setup-X.Y.Z.exe`)을 찾아 다운로드 후
   실행합니다. 별도 조치 없이, 릴리즈가 게시되는 즉시 이전 버전 사용자에게
   업데이트가 안내됩니다.

Actions 탭에서 `workflow_dispatch`로 `version` 입력값(`v` 접두어 제외)을
지정해 수동으로 릴리즈 워크플로를 실행할 수도 있습니다. 태그를 다시 만들지
않고 릴리즈를 재실행할 때 유용합니다.

---

## 2. Manual local packaging / 로컬 수동 패키징

**EN**

Useful for testing the installer before tagging a release, or for building
outside of CI.

1. Publish the framework-dependent single-file build:
   ```bash
   dotnet publish src/GitTab.App/GitTab.App.csproj -c Release -r win-x64 \
     --self-contained false -p:PublishSingleFile=true -o publish
   ```
   This produces `publish/GitTab.exe`, which requires the .NET 8 Desktop
   Runtime to be installed on the target machine.
2. Compile the installer with Inno Setup (ISCC), optionally overriding the
   version:
   ```powershell
   & "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DAppVersion=X.Y.Z installer\GitTab_installer.iss
   ```
   (Omit `/DAppVersion=X.Y.Z` to use the fallback version baked into the
   `.iss` script.)
3. The compiled installer is written to `dist/GitTab-Setup-X.Y.Z.exe`.

**KR**

릴리즈 태그를 만들기 전에 설치 파일을 미리 테스트하거나, CI 없이 로컬에서
빌드할 때 사용합니다.

1. 프레임워크 종속(단일 파일) 게시:
   ```bash
   dotnet publish src/GitTab.App/GitTab.App.csproj -c Release -r win-x64 \
     --self-contained false -p:PublishSingleFile=true -o publish
   ```
   실행 시 대상 PC에 .NET 8 Desktop Runtime이 설치되어 있어야 하는
   `publish/GitTab.exe`가 생성됩니다.
2. Inno Setup(ISCC)으로 설치 파일을 컴파일합니다. 필요 시 버전을
   덮어쓸 수 있습니다.
   ```powershell
   & "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DAppVersion=X.Y.Z installer\GitTab_installer.iss
   ```
   (`/DAppVersion=X.Y.Z`를 생략하면 `.iss` 스크립트에 내장된 기본 버전이
   사용됩니다.)
3. 컴파일된 설치 파일은 `dist/GitTab-Setup-X.Y.Z.exe`로 생성됩니다.
