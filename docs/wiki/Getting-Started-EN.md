**English** · [한국어](Getting-Started)

# Getting Started

**Set up Git Tab and open your first repository in just 5 minutes.**

---

## Step 1: Install

### Download
1. Go to the [GitHub Releases page](https://github.com/BaeTab/GitTab/releases).
2. Find and download the latest **`GitTab-Setup.exe`** (or `*-installer.exe`) file.
3. Double-click the downloaded file.

### Installation Process
When the installer starts:

- **Choose Language:** Select your preferred language (default: English; you can change to Korean after launching Git Tab).
- **.NET 8 Runtime Check:** On first install, you may be notified that `.NET 8 Desktop Runtime` is required. Click "Install" or "Download" and it will be downloaded and installed automatically. **(Git is already included, so no separate installation is needed.)**
- **Installation Folder:** Select the default folder (e.g., `C:\Program Files\GitTab`) and continue.
- **Complete:** When the installer finishes, the "Launch Git Tab" checkbox becomes active. Check it and click "Finish" to run the app immediately.

---

## Step 2: First Launch

### Verify the App Opened
Success if the Git Tab window appears!

- You'll see menus at the top and buttons in the center.
- A "Recent Repositories" list appears on the left (empty on first run).

### Switch to Korean (Optional)
If you see English:
1. Click the **⚙ Settings** icon in the top right.
2. In the **Language** dropdown, select **Korean**.
3. The app will automatically restart and display in Korean.

---

## Step 3: Open a Repository

### Method 0: Clone a Remote Repository
If you want to download a repository from GitHub, GitLab, or another source:
1. Click the **"Clone"** button in the Git Tab main window.
2. Enter the repository URL (e.g., `https://github.com/user/repo.git`).
3. Choose where to save it and click "Clone" to download the entire repository.

### Method 1: Open with Button
1. Click the **"Open Folder"** or **"Open Repository"** button in the Git Tab main window.
2. Select an existing Git repository folder (one that contains a `.git` folder).
3. Click **Open** to load the repository.

> **Tip:** If you don't have a Git repository yet, open a regular folder and select "Initialize New Repository" from the Git Tab menu.

### Method 2: Drag & Drop
1. Find a folder in Windows File Explorer.
2. Drag it into the Git Tab window.
3. The repository loads automatically.

### Method 3: Windows File Explorer Context Menu
1. Right-click a folder in File Explorer.
2. Select **"Git Tab"** or **"Open"** from the menu.
3. Git Tab opens with that repository loaded.

> **Note:** If you don't see the context menu, enable "Windows File Explorer Context Menu" in Git Tab settings. (See [Explorer Menu](Explorer-Menu) for details.)

---

## What is a Repository?

**Repository = Project Folder + Git History**

A Git repository is simply a folder that contains a `.git` folder. Inside it, all file changes are recorded. This lets you go back to any earlier state or collaborate with others anytime.

---

## What's Next?

Now that you've opened a repository, here are your next steps:

- **Want to learn what Git is?** Check out the [Git Basics](Git-Basics) page.
- **Want to explore Git Tab's features?** See the [Features](Features) page.
- **Want to push your code to GitHub?** Learn how to set up a personal access token on the [Authentication](Authentication) page.

---

## Trouble?

- **App won't start:** Check the ".NET 8 Runtime" section on the [Troubleshooting](Troubleshooting) page.
- **Repository not recognized:** Make sure the folder contains a `.git` folder.
- **Something else:** Look for answers in [FAQ](FAQ).
