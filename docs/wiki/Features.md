# Features Overview

**English** · [한국어](기능-둘러보기)

**Discover everything you can do with Git Tab. Each feature includes "When to use it?" to help you find what you need.**

---

## 🆕 Clone Repository

### What does it do?
Enter the URL of a remote repository like GitHub or GitLab, and Git Tab will download the entire repository to your PC. All commits, branches, and files from the remote are copied as-is.

### How to use

**Method 1: From the main window**
1. Click the **"Clone"** button in the Git Tab main window.
2. Enter the repository URL (e.g., `https://github.com/user/repo.git`).
3. Choose a folder to save it in.
4. Click the "Clone" button to start the download.

**Method 2: From Windows Explorer**
1. Right-click the folder where you want to create the repository in Explorer.
2. Select **"Git Tab"** → **"Clone"**.
3. Enter the repository URL.
4. Click the "Clone" button.

### When to use it?
- Download open-source projects from GitHub/GitLab to your computer
- Get a team repository for the first time
- Run someone else's code locally to test it

---

## 🎨 Colorful Commit Graph

### What does it do?
The commit graph in the center of the screen displays each branch in a different color. Next to each commit, you'll see the author's avatar, commit message, and a bar chart showing the number of files changed.

### When to use it?
- Quickly understand at a glance who did what when working with multiple branches
- Track merge history
- Understand your team's progress at a glance

---

## 📝 Stage & Commit Changes

### Basic workflow

1. **View the file list**  
   All modified files appear in the "Changes" section on the right.

2. **Select files**  
   Click the ✓ checkbox next to the files you want to commit to stage them.

3. **Enter a message** (using the commit message helper)  
   Briefly describe what you did in the text box below.  
   Before the message is a **commit type dropdown** (following Conventional Commits):
   ```
   Type: [feat] [fix] [docs] [style] [refactor] [test] [chore] etc.
   Example: feat: add login form validation
   Example: fix: dark theme bug on login page
   Example: docs: update README
   ```
   Select the appropriate type from the dropdown, and it automatically prepends your message.

4. **Commit**  
   Click the "Commit" button.

### Additional options

- **Partial Staging:** Want to commit only part of a file instead of the whole thing?  
  Click the **"±" button** next to the changed file, and you can select individual hunks (changes) to stage. (equivalent to git add -p)
  
- **Discard changes:** Revert a file to its previous state. Right-click the file → select "Discard".

- **Amend:** Fix the message or files of a commit you just made. Enable the "Amend" checkbox and commit again to modify the last commit.

### When to use it?
- Record changes daily
- Commit by feature or task
- Fix a commit message you got wrong

---

## 🌿 Branches & Tags

### Create a branch
1. Click the "Branches" tab on the right.
2. Click the "+ New Branch" button.
3. Enter a branch name:
   ```
   Example: feature/login
   Example: bugfix/signup-error
   Example: release/v1.0.0
   ```
4. Click the "Create" button to make the new branch.

### Switch branches (Checkout)
Right-click the branch you want to switch to in the branch list → select "Checkout". Your current branch will change.

### Rename a branch
Right-click the branch → select "Rename". Enter the new name.

### Delete a branch
Right-click the branch → select "Delete". You can't delete your current branch.

### Merge branches
1. First, checkout the target branch where you want to merge.  
   Example: If you want to merge `feature/login` into `main`, switch to `main` first.
2. Right-click the branch to merge (e.g., `feature/login`) → select "Merge".
3. If there are no conflicts, you're done. If conflicts occur, check the [Troubleshooting](Troubleshooting) page.

### Rebase
An advanced feature — an alternative way to merge two branches. Right-click a branch → select "Rebase". Your commit history will be cleaner.

> **Starting out?** You can get by with just merge for now.

### Create a tag
1. Right-click the commit in the commit graph that you want to tag.
2. Select "Create Tag".
3. Enter a tag name:
   ```
   Example: v1.0.0
   Example: v2.1.1-beta
   ```
4. Click the "Create" button.

### When to use it?
- **Branches:** Develop new features, fix bugs, or experiment
- **Merge:** Apply a finished feature to main
- **Tags:** Mark a version release

---

## 🔀 Interactive Rebase (Advanced)

An advanced feature that lets you rearrange commits, squash multiple commits into one, or delete specific commits.

### Basic usage
1. Right-click a branch → select "Interactive Rebase" or "Rebase Interactive".
2. Choose the base commit (usually `main`).
3. Select an action for each commit:
   - **pick:** Use this commit as-is
   - **squash:** Combine with the previous commit
   - **reorder:** Change the order
   - **drop:** Delete this commit

### When to use it?
- Clean up commits before a Pull Request
- "Combine 3 small commits into 1"

> **Warning:** This is an advanced feature. Practice on a test branch before using it in real work.

---

## 📦 Stash

### Save
1. Look for the "Save Stash" button in the right menu.
2. Optionally give the stash a name:
   ```
   Example: "login form work in progress"
   ```
3. Click the "Save" button.

Your working files are temporarily stored, and your working folder becomes clean.

### Restore
1. Check the "Stash List".
2. Right-click the stash you want to restore → select "Apply" or "Pop".

The difference between the two:
- **Apply:** Restore the stash, but keep it in the list.
- **Pop:** Restore the stash and remove it from the list.

### When to use it?
- Fix an urgent bug without touching your work in progress
- Switch branches without committing

---

## 🔍 Blame

See who wrote each line of code and when.

### How to use
1. Right-click a commit in the commit graph.
2. Select "Blame" or "View History".
3. Each line displays the author, commit message, and date.

### When to use it?
- Track down "Where did this bug come from?"
- Ask a teammate "Why did you do it this way?" with evidence

---

## ⚠️ Conflict Resolution

A conflict happens when you merge two branches and the same part of a file was changed differently in each branch.

### Find conflicting files
They appear in red in the "Changes" section on the right.

### Resolve conflicts

**Method 1: Using Git Tab's visual 3-way merge editor**
1. Right-click the conflicting file.
2. Select "Resolve Conflict".
3. A **visual 3-way merge editor** opens:
   - **Left (Base):** Original code
   - **Middle (Result):** Final result (editable)
   - **Right (Theirs / HEAD):** Their changes
   - Below: Quick fill buttons like "Take mine", "Take theirs", "Keep both"
4. Select the parts you need or edit directly to complete the result.
5. When you save, the file is automatically staged.

**Method 2: Manual editing**
1. Open the file in a text editor.
2. Find the conflict markers:
   ```
   <<<<<<< HEAD
   my version
   =======
   other version
   >>>>>>> branchname
   ```
3. Keep only the parts you want and delete the conflict markers.
4. In Git Tab, select "Resolved".

### Abort or continue
- **Abort:** Cancel the merge and go back to the original state. Select "Abort Merge".
- **Continue:** After resolving conflicts, complete the merge. Click "Finish" or "Commit".

### When to use it?
- An unavoidable part of teamwork

---

## 🌐 Remote

### Push (upload to server)
1. Click the **"Push"** button in the top right.
2. If authentication is needed, a username + token dialog appears.
3. Click "OK" or "Push".

### Pull (download from server)
1. Click the **"Pull"** button in the top right.
2. Your teammates' new commits are downloaded.

### Fetch (check for updates without changing files)
1. Click the **"Fetch"** button in the top right.
2. Server information updates, but your files don't change.

### Ahead / Behind indicator
You'll see a number next to your current branch:
- **Ahead 3:** Your local copy has 3 more commits than the server (Push needed)
- **Behind 2:** The server has 2 more commits than your local copy (Pull needed)

### GitHub / GitLab integration

**Create a Pull Request / Merge Request**
- Quickly open the PR/MR page for your current branch.
- Click the **"Create PR"** button in the top right, and your web browser opens the GitHub/GitLab PR creation page.

**Open in web browser**
- View your repository on the web.
- Click **"Open in Web"** in the top right or the **"Repository Home"** button in repository settings, and your browser opens the GitHub/GitLab repository page.

### When to use it?
- Sync code with GitHub/GitLab
- Collaborate with your team
- Review code from teammates via Pull Request

---

## 🔧 .gitignore Generator

Automatically configure files that Git should ignore (e.g., passwords, temporary folders, compiled output).

### How to use
1. Select ".gitignore Generator" or "Generate .gitignore" from the menu.
2. Choose your project type:
   ```
   Example: Python, Node.js, C#, Java, Android, etc.
   ```
3. Git Tab automatically adds the necessary rules.

### When to use it?
- When creating a new repository
- When first setting up a project with Git

---

## 🔎 Search Commits

### How to use
1. Type the words you're looking for in the search bar at the top.
2. Commit messages, authors, filenames, and more are filtered.

### When to use it?
- Find "When was this feature added?"
- Check "Who modified this file?"

---

## 🎨 Light / Dark Theme

1. Click the **⚙ Settings** icon in the top right.
2. In the **Theme** section, select "Light" or "Dark".
3. The app colors change instantly.

---

## 🌍 Switch language (Korean / English)

1. Click the **⚙ Settings** icon.
2. In the **Language** dropdown, select "한국어" or "English".
3. The app restarts and switches languages.

---

## 🖱️ Right-click Context Menu

Right-click on commits, branches, files, and more for quick actions:

**Right-click a commit:**
- Blame (View History)
- Create Tag
- Reset

**Right-click a branch:**
- Checkout (Switch)
- Merge
- Rebase
- Rename
- Delete

**Right-click a file:**
- Stage / Unstage
- Discard
- Resolve Conflict

---

## 📋 File History

### What does it do?
See all commits that touched a specific file at a glance. Great for tracking a file's change history.

### How to use
1. Right-click a file in the Changes section.
2. Select **"View File History"**.
3. All commits that modified the file are listed in chronological order.
4. Click a commit to see the diff (changes) for that commit.

### When to use it?
- Track "When did this bug appear?"
- See detailed change history for a specific file
- Check "Who added this feature and when?"

---

## 🔀 Compare

### What does it do?
See the differences between two branches, two tags, or two commits at a glance. View which files changed and what changed in each file.

### How to use
1. Click the **"Compare"** button in the top toolbar.
2. Choose what to compare (From/To):
   - Select branches (e.g., `main` vs `feature/login`)
   - Select tags (e.g., `v1.0.0` vs `v1.1.0`)
   - Select commits
3. Click the "Compare" button:
   - A list of changed files appears.
   - Click a file to see its diff.

### When to use it?
- Preview your changes before a Pull Request
- Check "What changed between v1.0.0 and v1.1.0?"
- Review differences between a team member's branch and main

---

## 🔍 Search Code (Pickaxe)

### What does it do?
Search the entire codebase to find commits where a specific string was **added or deleted**. Supports regex patterns too.

### How to use
1. Click the **"Search"** button in the top toolbar.
2. Enter the string you're looking for:
   ```
   Example: "const MAX_RETRY = "
   Example: "function setupAuth"
   Example: regex: "^import.*module"
   ```
3. Click the "Search" button.
4. Only commits that added or deleted that string are shown.
5. Click a commit to see the diff for that part.

### When to use it?
- Track "When was this function removed?"
- Find which commit introduced buggy code
- Track "Where was this dependency import added?"

---

## ⌨️ Command Palette

### What does it do?
Press **`Ctrl+P`** to search for and quickly run any Git Tab action with your keyboard.

### How to use
1. Press **`Ctrl+P`**.
2. The command palette window opens.
3. Search by typing the feature name:
   ```
   Example: "commit" → Open commit dialog
   Example: "push" → Run Push
   Example: "clone" → Clone repository
   Example: "rebase" → Rebase
   ```
4. Press Enter or click to run.

### When to use it?
- Run actions faster than clicking menus
- Search for a feature when you can't remember its exact name
- Do your entire workflow with the keyboard

---

## 🔄 Reflog (Work History & Undo)

### What does it do?
A list of all recent points (commits) that HEAD has pointed to. If you accidentally delete a branch or reset, you can restore it to an earlier state.

### How to use
1. Click the **"↩"** (Undo/Reflog) button in the top toolbar.
2. Your recent work history appears:
   ```
   Example: "HEAD@{0}: commit message"
   Example: "HEAD@{1}: checkout main"
   Example: "HEAD@{2}: reset previous commit"
   ```
3. Select the point you want to go back to.
4. Click "Restore" or "Checkout", and HEAD moves to that point.

### When to use it?
- Undo a wrong commit right after making it
- Recover a deleted branch
- Recover from a broken rebase

---

## ⏸️ Cancel Operations

### What does it do?
Stop long-running tasks like Fetch, Pull, Push, or Clone in the middle.

### How to use
1. During Fetch, Pull, Push, or Clone, a **"Cancel"** button appears in the progress dialog or status bar.
2. Click it to stop the operation.
3. Be aware that some changes may have been partially applied.

### When to use it?
- Cancel a large clone when the network is slow
- Stop a Pull if you realize you're on the wrong branch
- Cancel an accidental Push

---

## 🐛 Crash Reports (Opt-in)

### What does it do?
When Git Tab crashes, save error information to your **local disk** so you can report it to the development team later. No data is sent outside.

### How to use
1. Click the **⚙ Settings** icon in the top right.
2. Find the **"Diagnostics"** section.
3. Check the **"Enable Crash Report Collection"** checkbox.
4. From now on, error information is automatically saved when the app closes.

### Report location
```
C:\Users\<username>\AppData\Local\GitTab\CrashReports\
```

### When to use it?
- When reporting a bug to the development team, include the crash report for faster fixes.
- Help improve app stability.

---

## Next steps

- **Want to connect GitHub?** Read the [Authentication](Authentication) page.
- **Want to use Git commands directly from Windows Explorer?** Check the [Explorer Menu](Explorer-Menu) page.
- **Having trouble?** See the [Troubleshooting](Troubleshooting) page.
