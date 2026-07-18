using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace GitTab.App.Localization;

/// <summary>
/// In-memory ko/en string tables with a live language toggle. Beginner-oriented ".Tip" strings
/// explain each Git action in plain language for first-time users.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    public static LocalizationService Current { get; private set; } = new();

    private AppLanguage _language = AppLanguage.Korean;

    public LocalizationService() => Current = this;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            // Refresh every indexer binding and notify listeners.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsKorean)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnglish)));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsKorean => _language == AppLanguage.Korean;
    public bool IsEnglish => _language == AppLanguage.English;

    public string this[string key] => T(key);

    public string T(string key)
    {
        var table = _language == AppLanguage.Korean ? Ko : En;
        if (table.TryGetValue(key, out var value)) return value;
        // Fall back to the other language, then the key itself.
        var other = _language == AppLanguage.Korean ? En : Ko;
        return other.TryGetValue(key, out var alt) ? alt : key;
    }

    public string T(string key, params object[] args)
    {
        try { return string.Format(CultureInfo.CurrentCulture, T(key), args); }
        catch (FormatException) { return T(key); }
    }

    public void Toggle() => Language = _language == AppLanguage.Korean ? AppLanguage.English : AppLanguage.Korean;

    // ---------------------------------------------------------------- string tables

    private static readonly Dictionary<string, string> Ko = new(StringComparer.Ordinal)
    {
        ["App.Title"] = "Git Tab",

        // Toolbar / actions
        ["Action.Open"] = "저장소 열기",
        ["Action.Open.Tip"] = "컴퓨터에서 Git 저장소 폴더를 골라 열어요.",
        ["Action.Refresh"] = "새로고침",
        ["Action.Refresh.Tip"] = "저장소의 최신 상태를 다시 읽어와요.",
        ["Action.Recent"] = "최근 저장소",
        ["Action.Recent.Empty"] = "최근 연 저장소가 없어요",
        ["Action.Fetch"] = "가져오기",
        ["Action.Fetch.Tip"] = "원격 저장소의 최신 커밋 정보를 내려받아요. 내 파일이나 브랜치는 바뀌지 않으니 안심하고 눌러도 돼요.",
        ["Action.Pull"] = "받아오기",
        ["Action.Pull.Tip"] = "원격의 변경을 가져와서 현재 브랜치에 합쳐요(fetch + merge).",
        ["Action.Push"] = "올리기",
        ["Action.Push.Tip"] = "내 커밋을 원격 저장소로 보내요. 인증은 시스템 Git 자격 증명 도우미가 처리해요.",
        ["Action.Commit"] = "커밋",

        // Language / theme / search
        ["Language"] = "언어",
        ["Language.Korean"] = "한국어",
        ["Language.English"] = "English",
        ["Theme.Toggle"] = "테마 전환",
        ["Theme.Light"] = "라이트",
        ["Theme.Dark"] = "다크",
        ["Search.Placeholder"] = "메시지 · 작성자 · 해시로 검색",
        ["Search.Tip"] = "커밋 메시지, 작성자 이름, 또는 커밋 해시로 걸러서 찾아요.",

        // Panels
        ["Panel.History"] = "히스토리",
        ["Panel.Changes"] = "변경사항",
        ["Panel.Branches"] = "브랜치",
        ["Panel.Details"] = "커밋 상세",
        ["Panel.Diff"] = "변경 내용",

        // Welcome / onboarding
        ["Welcome.Title"] = "Git Tab에 오신 걸 환영해요",
        ["Welcome.Subtitle"] = "저장소 폴더를 열거나, 이 창으로 폴더를 끌어다 놓으세요.",
        ["Welcome.Open"] = "저장소 열기",
        ["Welcome.Hint"] = "Git이 처음인가요? 각 버튼에 마우스를 올리면 무엇을 하는지 설명이 나와요.",

        // Working copy / staging
        ["WC.Staged"] = "스테이지됨 (커밋에 포함될 변경)",
        ["WC.Unstaged"] = "스테이지 안 됨 (아직 커밋에 포함 안 됨)",
        ["WC.StageAll"] = "모두 스테이지",
        ["WC.StageAll.Tip"] = "모든 변경을 다음 커밋에 포함하도록 올려요.",
        ["WC.UnstageAll"] = "모두 내리기",
        ["WC.Stage.Tip"] = "이 파일의 변경을 다음 커밋에 포함하도록 올려둬요.",
        ["WC.Unstage.Tip"] = "이 파일을 다음 커밋에서 빼요(변경 내용은 그대로 남아요).",
        ["WC.Discard"] = "변경 되돌리기",
        ["WC.Discard.Tip"] = "이 파일에서 수정한 내용을 버리고 마지막 커밋 상태로 되돌려요. ⚠ 되돌린 내용은 복구할 수 없어요.",
        ["WC.CommitMessage.Placeholder"] = "무엇을, 왜 바꿨는지 적어요",
        ["WC.Amend"] = "마지막 커밋 고치기 (amend)",
        ["WC.Amend.Tip"] = "새 커밋을 만들지 않고, 방금 만든 마지막 커밋에 변경을 합치거나 메시지를 고쳐요.",
        ["WC.Commit"] = "커밋하기",
        ["WC.Commit.Tip"] = "스테이지된 변경을 하나의 커밋(스냅샷)으로 저장해요. 커밋은 나중에 다시 돌아올 수 있는 안전한 기록점이에요.",
        ["WC.NothingToCommit"] = "커밋할 변경이 없어요",
        ["WC.Clean"] = "변경사항이 없어요. 깨끗한 상태예요!",

        // Branches
        ["Branch.Local"] = "로컬 브랜치",
        ["Branch.Remote"] = "원격 브랜치",
        ["Branch.Checkout"] = "이 브랜치로 이동",
        ["Branch.Checkout.Tip"] = "이 브랜치로 전환해요. 작업 폴더의 파일이 그 브랜치의 상태로 바뀌어요.",
        ["Branch.New"] = "새 브랜치",
        ["Branch.New.Tip"] = "지금 위치에서 새 브랜치를 만들어요. 브랜치는 안전하게 실험할 수 있는 평행 작업 공간이에요.",
        ["Branch.Delete"] = "브랜치 삭제",
        ["Branch.Delete.Tip"] = "이 브랜치를 삭제해요. 아직 합쳐지지 않은 커밋이 있으면 경고해요.",
        ["Branch.Rename"] = "이름 바꾸기",
        ["Branch.Rename.Tip"] = "이 브랜치의 이름을 바꿔요.",
        ["Branch.Merge"] = "현재 브랜치에 병합",
        ["Branch.Merge.Tip"] = "선택한 브랜치의 변경을 지금 브랜치로 합쳐요.",
        ["Branch.Rebase"] = "리베이스",
        ["Branch.Rebase.Tip"] = "현재 브랜치의 커밋을 선택한 브랜치 위로 다시 쌓아요. 히스토리를 다시 쓰므로 주의하세요.",
        ["Branch.NewName.Prompt"] = "새 브랜치 이름을 입력하세요",
        ["Branch.RenamePrompt"] = "새 이름을 입력하세요",

        // Context-menu (TortoiseGit-style right-click)
        ["Ctx.CheckoutCommit"] = "이 커밋으로 체크아웃 (detached)",
        ["Ctx.CreateBranch"] = "여기서 브랜치 생성",
        ["Ctx.CreateTag"] = "여기서 태그 생성",
        ["Ctx.ResetSoft"] = "여기로 Reset — soft (변경 보존)",
        ["Ctx.ResetMixed"] = "여기로 Reset — mixed (스테이지 해제)",
        ["Ctx.ResetHard"] = "여기로 Reset — hard ⚠ (변경 삭제)",
        ["Ctx.Revert"] = "이 커밋 되돌리기 (revert)",
        ["Ctx.CherryPick"] = "이 커밋 체리픽 (cherry-pick)",
        ["Ctx.CopySha"] = "커밋 해시(SHA) 복사",
        ["Ctx.CopyMessage"] = "커밋 메시지 복사",
        ["Ctx.OpenFile"] = "파일 열기",
        ["Ctx.CopyPath"] = "경로 복사",

        // .gitignore generator
        ["Gitignore.Button"] = ".gitignore",
        ["Gitignore.Title"] = ".gitignore 만들기",
        ["Gitignore.Tip"] = "프로젝트 스택을 감지해 알맞은 .gitignore를 만들어요. 빌드 산출물·캐시 등 버전관리에 넣지 않을 파일을 자동으로 제외해요.",
        ["Gitignore.Detected"] = "포함할 스택 (감지된 항목이 미리 선택됨)",
        ["Gitignore.Write"] = "생성 / 덮어쓰기",
        ["Gitignore.Append"] = "기존 파일에 추가",
        ["Gitignore.Exists"] = "이미 .gitignore가 있어요.",

        // Commit details
        ["Details.Author"] = "작성자",
        ["Details.Date"] = "날짜",
        ["Details.Commit"] = "커밋",
        ["Details.Parents"] = "부모",
        ["Details.Message"] = "메시지",
        ["Details.ChangedFiles"] = "변경된 파일",
        ["Details.SelectCommit"] = "왼쪽에서 커밋을 선택하면 상세 내용이 보여요.",

        // Diff
        ["Diff.Binary"] = "바이너리 파일이라 변경 내용을 미리 볼 수 없어요.",
        ["Diff.NoChanges"] = "표시할 변경이 없어요.",
        ["Diff.SelectFile"] = "파일을 선택하면 바뀐 줄이 색으로 표시돼요.",

        // Remote / status
        ["Remote.Ahead"] = "▲{0}",
        ["Remote.Behind"] = "▼{0}",
        ["Remote.AheadBehind.Tip"] = "원격보다 앞선 커밋 수(▲)와 뒤처진 커밋 수(▼)예요.",
        ["Status.NoRepo"] = "저장소를 열어 시작하세요",
        ["Status.Ready"] = "준비됨",
        ["Status.Loading"] = "불러오는 중…",
        ["Status.Working"] = "작업 중…",

        // Update
        ["Update.Available"] = "새 버전 {0} 이(가) 있어요",
        ["Update.Download"] = "다운로드 후 설치",
        ["Update.Later"] = "나중에",
        ["Update.UpToDate"] = "최신 버전을 쓰고 있어요",
        ["Update.Checking"] = "업데이트 확인 중…",
        ["Update.CheckFailed"] = "업데이트를 확인하지 못했어요",

        // Common / dialogs
        ["Common.OK"] = "확인",
        ["Common.Cancel"] = "취소",
        ["Common.Yes"] = "예",
        ["Common.No"] = "아니오",
        ["Common.Close"] = "닫기",
        ["Common.Error"] = "오류",
        ["Common.Warning"] = "주의",
        ["Common.Success"] = "완료",
        ["Common.Delete"] = "삭제",
        ["Confirm.DiscardTitle"] = "변경을 되돌릴까요?",
        ["Confirm.DiscardBody"] = "‘{0}’의 수정 내용이 사라지고 복구할 수 없어요. 계속할까요?",
        ["Confirm.DeleteBranchTitle"] = "브랜치를 삭제할까요?",
        ["Confirm.DeleteBranchBody"] = "‘{0}’ 브랜치를 삭제해요. 계속할까요?",
        ["Error.OpenFailed"] = "저장소를 열 수 없어요",
        ["Error.GitFailed"] = "Git 명령이 실패했어요",
        ["Error.NotARepo"] = "선택한 폴더는 Git 저장소가 아니에요.",

        // Relative time
        ["Time.JustNow"] = "방금 전",
        ["Time.MinutesAgo"] = "{0}분 전",
        ["Time.HoursAgo"] = "{0}시간 전",
        ["Time.DaysAgo"] = "{0}일 전",
        ["Time.MonthsAgo"] = "{0}개월 전",
        ["Time.YearsAgo"] = "{0}년 전",
    };

    private static readonly Dictionary<string, string> En = new(StringComparer.Ordinal)
    {
        ["App.Title"] = "Git Tab",

        ["Action.Open"] = "Open repository",
        ["Action.Open.Tip"] = "Pick a Git repository folder on your computer to open.",
        ["Action.Refresh"] = "Refresh",
        ["Action.Refresh.Tip"] = "Re-read the repository's latest state.",
        ["Action.Recent"] = "Recent",
        ["Action.Recent.Empty"] = "No recent repositories",
        ["Action.Fetch"] = "Fetch",
        ["Action.Fetch.Tip"] = "Downloads the latest commit info from the remote. It does NOT change your files or branches — safe to click anytime.",
        ["Action.Pull"] = "Pull",
        ["Action.Pull.Tip"] = "Fetches remote changes and merges them into your current branch (fetch + merge).",
        ["Action.Push"] = "Push",
        ["Action.Push.Tip"] = "Sends your commits to the remote. Authentication is handled by your system Git credential helper.",
        ["Action.Commit"] = "Commit",

        ["Language"] = "Language",
        ["Language.Korean"] = "한국어",
        ["Language.English"] = "English",
        ["Theme.Toggle"] = "Toggle theme",
        ["Theme.Light"] = "Light",
        ["Theme.Dark"] = "Dark",
        ["Search.Placeholder"] = "Search message · author · hash",
        ["Search.Tip"] = "Filter commits by message text, author name, or commit hash.",

        ["Panel.History"] = "History",
        ["Panel.Changes"] = "Changes",
        ["Panel.Branches"] = "Branches",
        ["Panel.Details"] = "Commit details",
        ["Panel.Diff"] = "Diff",

        ["Welcome.Title"] = "Welcome to Git Tab",
        ["Welcome.Subtitle"] = "Open a repository folder, or drag a folder onto this window.",
        ["Welcome.Open"] = "Open repository",
        ["Welcome.Hint"] = "New to Git? Hover any button to see what it does.",

        ["WC.Staged"] = "Staged (will be in the next commit)",
        ["WC.Unstaged"] = "Unstaged (not in the commit yet)",
        ["WC.StageAll"] = "Stage all",
        ["WC.StageAll.Tip"] = "Add all changes to the next commit.",
        ["WC.UnstageAll"] = "Unstage all",
        ["WC.Stage.Tip"] = "Add this file's changes to the next commit.",
        ["WC.Unstage.Tip"] = "Remove this file from the next commit (your edits stay).",
        ["WC.Discard"] = "Discard changes",
        ["WC.Discard.Tip"] = "Throw away this file's edits and restore it to the last commit. ⚠ This can't be undone.",
        ["WC.CommitMessage.Placeholder"] = "Describe what changed and why",
        ["WC.Amend"] = "Amend last commit",
        ["WC.Amend.Tip"] = "Instead of a new commit, fold these changes into your most recent commit (or edit its message).",
        ["WC.Commit"] = "Commit",
        ["WC.Commit.Tip"] = "Save your staged changes as one commit (a snapshot). A commit is a safe point you can always return to.",
        ["WC.NothingToCommit"] = "Nothing to commit",
        ["WC.Clean"] = "No changes — your working tree is clean!",

        ["Branch.Local"] = "Local branches",
        ["Branch.Remote"] = "Remote branches",
        ["Branch.Checkout"] = "Switch to this branch",
        ["Branch.Checkout.Tip"] = "Switch to this branch; your working files change to match it.",
        ["Branch.New"] = "New branch",
        ["Branch.New.Tip"] = "Create a new branch from where you are. A branch is a parallel space to experiment safely.",
        ["Branch.Delete"] = "Delete branch",
        ["Branch.Delete.Tip"] = "Delete this branch. You'll be warned if it has unmerged commits.",
        ["Branch.Rename"] = "Rename",
        ["Branch.Rename.Tip"] = "Rename this branch.",
        ["Branch.Merge"] = "Merge into current",
        ["Branch.Merge.Tip"] = "Combine the selected branch's changes into your current branch.",
        ["Branch.Rebase"] = "Rebase",
        ["Branch.Rebase.Tip"] = "Re-apply your current branch's commits on top of the selected branch. This rewrites history — use with care.",
        ["Branch.NewName.Prompt"] = "Enter a name for the new branch",
        ["Branch.RenamePrompt"] = "Enter the new name",

        ["Ctx.CheckoutCommit"] = "Checkout this commit (detached)",
        ["Ctx.CreateBranch"] = "Create branch here",
        ["Ctx.CreateTag"] = "Create tag here",
        ["Ctx.ResetSoft"] = "Reset here — soft (keep changes)",
        ["Ctx.ResetMixed"] = "Reset here — mixed (unstage)",
        ["Ctx.ResetHard"] = "Reset here — hard ⚠ (discard changes)",
        ["Ctx.Revert"] = "Revert this commit",
        ["Ctx.CherryPick"] = "Cherry-pick this commit",
        ["Ctx.CopySha"] = "Copy commit hash (SHA)",
        ["Ctx.CopyMessage"] = "Copy commit message",
        ["Ctx.OpenFile"] = "Open file",
        ["Ctx.CopyPath"] = "Copy path",

        ["Gitignore.Button"] = ".gitignore",
        ["Gitignore.Title"] = "Create .gitignore",
        ["Gitignore.Tip"] = "Detects your project's stack and generates a matching .gitignore, so build output, caches, and other noise stay out of version control.",
        ["Gitignore.Detected"] = "Stacks to include (detected ones are pre-selected)",
        ["Gitignore.Write"] = "Create / overwrite",
        ["Gitignore.Append"] = "Append to existing",
        ["Gitignore.Exists"] = "A .gitignore already exists.",

        ["Details.Author"] = "Author",
        ["Details.Date"] = "Date",
        ["Details.Commit"] = "Commit",
        ["Details.Parents"] = "Parents",
        ["Details.Message"] = "Message",
        ["Details.ChangedFiles"] = "Changed files",
        ["Details.SelectCommit"] = "Select a commit on the left to see its details.",

        ["Diff.Binary"] = "Binary file — no preview available.",
        ["Diff.NoChanges"] = "No changes to show.",
        ["Diff.SelectFile"] = "Select a file to see changed lines highlighted.",

        ["Remote.Ahead"] = "▲{0}",
        ["Remote.Behind"] = "▼{0}",
        ["Remote.AheadBehind.Tip"] = "Commits you are ahead of the remote (▲) and behind it (▼).",
        ["Status.NoRepo"] = "Open a repository to begin",
        ["Status.Ready"] = "Ready",
        ["Status.Loading"] = "Loading…",
        ["Status.Working"] = "Working…",

        ["Update.Available"] = "Version {0} is available",
        ["Update.Download"] = "Download & install",
        ["Update.Later"] = "Later",
        ["Update.UpToDate"] = "You're on the latest version",
        ["Update.Checking"] = "Checking for updates…",
        ["Update.CheckFailed"] = "Couldn't check for updates",

        ["Common.OK"] = "OK",
        ["Common.Cancel"] = "Cancel",
        ["Common.Yes"] = "Yes",
        ["Common.No"] = "No",
        ["Common.Close"] = "Close",
        ["Common.Error"] = "Error",
        ["Common.Warning"] = "Warning",
        ["Common.Success"] = "Done",
        ["Common.Delete"] = "Delete",
        ["Confirm.DiscardTitle"] = "Discard changes?",
        ["Confirm.DiscardBody"] = "Edits to '{0}' will be lost and cannot be recovered. Continue?",
        ["Confirm.DeleteBranchTitle"] = "Delete branch?",
        ["Confirm.DeleteBranchBody"] = "The branch '{0}' will be deleted. Continue?",
        ["Error.OpenFailed"] = "Couldn't open the repository",
        ["Error.GitFailed"] = "The Git command failed",
        ["Error.NotARepo"] = "The selected folder is not a Git repository.",

        ["Time.JustNow"] = "just now",
        ["Time.MinutesAgo"] = "{0} min ago",
        ["Time.HoursAgo"] = "{0} h ago",
        ["Time.DaysAgo"] = "{0} d ago",
        ["Time.MonthsAgo"] = "{0} mo ago",
        ["Time.YearsAgo"] = "{0} y ago",
    };
}
