**English** · [한국어](문제-해결)

# Troubleshooting

**Helpful tips for when things aren't working as expected. Most issues have simple solutions!**

---

## Git Tab Won't Open

### Symptoms
- You clicked the Git Tab icon but nothing happens.
- Or the window appears briefly and then closes immediately.

### Solutions

#### Method 1: Check for .NET 8 Runtime

Git Tab requires the **.NET 8 Desktop Runtime**. Let's verify it's installed.

**1. Check if it's installed:**
1. Open Windows Search (🔍) and search for **"Programs"** or **"Programs and Features"**.
2. Look through the installed programs list for **".NET Desktop Runtime"**.
3. If you see version **"8.0"**, you're all set.

**2. If it's not installed:**
1. Go to [Microsoft's .NET download page](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Select **".NET 8.0"** → **"Desktop Runtime"**.
3. Download the **"x64"** version (for typical 64-bit Windows).
4. Run the installer and follow the prompts.
5. After installation completes, restart Git Tab.

#### Method 2: Run as Administrator

1. Right-click the Git Tab shortcut (or .exe file).
2. Select **"Run as administrator"**.

#### Method 3: Reinstall

1. Open Windows Control Panel → **"Programs and Features"** (or **"Uninstall a program"**).
2. Find "Git Tab" and click **"Uninstall"**.
3. Follow the steps on the [Getting Started](Getting-Started-EN) page to reinstall.

---

## "Git not found" or "Git executable not found" Error

### Symptoms
```
Git command not found
Git executable not found
```

### Solutions

#### Method 1: Reinstall Git Tab

Git Tab includes Git tools during installation. If something went wrong:

1. Follow the "Reinstall" section under "Git Tab Won't Open" above.

#### Method 2: Check System PATH (Advanced)

If you've installed Git separately on your system:

1. Verify that Git is included in your system's PATH environment variable.
2. Open Windows Search → **"Edit environment variables"** → check the "Path" list for a Git folder (e.g., `C:\Program Files\Git\bin`).
3. If it's missing, add it and restart your computer.

---

## Context Menu Not Showing

### Symptoms
You right-clicked a folder in Windows Explorer, but the **"Git Tab"** menu option isn't visible.

### Solutions

#### Method 1: Enable in Git Tab Settings

1. Click the **⚙ Settings** icon in Git Tab.
2. Look for the **"Integration"** or **"Windows Explorer"** section.
3. Check the box for **"Enable Windows Explorer Context Menu"**.
4. Click **"Save"** or **"Apply"**.

#### Method 2: Restart Windows Explorer

After enabling the setting above:

1. Close all Explorer windows.
2. Reopen Explorer (press the Windows key, type "explorer" and press Enter).
3. Try right-clicking a folder again.

Or use the Task Manager:

1. Press `Ctrl + Shift + Esc` to open Task Manager.
2. Find **"explorer.exe"**, right-click it, and select **"Restart"**.

#### Method 3: Windows 11's "Show more options"

Windows 11 shows a simplified context menu by default:

1. Right-click a folder.
2. At the bottom of the menu, click **"Show more options"**.
3. The full context menu will appear, and you should see **"Git Tab"** here.

#### Method 4: Disable Other Git Tools' Menus

If you have other Git tools installed (TortoiseGit, GitHub Desktop, etc.), their menu entries might conflict:

1. Open settings for the other tool and disable the **"Windows Explorer context menu"** option.
2. Restart Explorer.
3. Re-enable Git Tab's menu in Git Tab settings if needed.

---

## Push Authentication Failed

### Symptoms
```
Authentication failed
Permission denied
```

### Solutions

#### Method 1: Verify Your Personal Access Token

You need a **PAT (Personal Access Token)**, not your GitHub password.

1. Follow the [Authentication](Authentication) page to generate a token.
2. When prompted during Push, enter the token in the authentication dialog.

> ⚠️ **GitHub passwords no longer work!**

#### Method 2: Clear Saved Credentials

You may have old or incorrect credentials stored.

1. Open Windows Search → search for **"Credential Manager"**.
2. Click the **"Windows Credentials"** tab.
3. Find GitHub-related entries (like `git:https://github.com`).
4. Click on it → click **"Remove"**.
5. Try Push again in Git Tab.
6. Enter your token when the authentication dialog appears.

Or from Git Tab settings:

1. **⚙ Settings** → **"Credentials"** section.
2. Click **"Clear Saved Credentials"**.
3. Try again.

#### Method 3: Check Token Permissions

1. Visit GitHub's Personal Access Tokens page (Settings → Developer settings → Personal access tokens).
2. Find the token you're using.
3. Verify that **"repo"** permission is checked.
4. Check that the expiration date hasn't passed.
5. If it has expired, follow the "Token Expired" section below on the [Authentication](Authentication) page.

#### Method 4: Verify Internet Connection

1. Make sure your internet is working.
2. Check that your firewall isn't blocking Git or GitHub access.

---

## Garbled Text or Korean Characters

### Symptoms
- Commit messages or filenames show as ????? or strange characters.
- You can't type Korean characters in the commit message box on the right.

### Solutions

#### Method 1: Change Language Setting

1. Click **⚙ Settings** → **"Language"** dropdown.
2. Select **"한국어"** (Korean) or **"English"** as needed.
3. The app will restart and display in your chosen language.

#### Method 2: Check Windows System Language

1. Open Windows Settings (⚙) → **"Time & Language"** → **"Language & region"**.
2. Verify that **"Windows display language"** is set to **"한국어"** (or your preferred language).
3. If it's different, select the correct language and restart your computer.

#### Method 3: Check Input Method

If you can't type Korean in the commit message box:

1. Click the message input box on the right.
2. Press your language toggle key (`Shift + Space` or `Alt + ~` on Korean keyboards).
3. Verify the input method shows **"한국어"** (Korean).

---

## Where Do I Find Error Logs?

### Question
"Something seems wrong. Where can I find detailed error messages?"

### Answer

Git Tab log files are located at:

```
C:\Users\[YourUsername]\AppData\Roaming\GitTab\logs
```

Or from within Git Tab:

1. Open **⚙ Settings**.
2. Click **"Open Logs Folder"**.

You can open log files in a text editor (Notepad, VS Code, etc.) to see detailed error messages. If you can't solve the problem yourself, attach the log file to a [GitHub Issue](https://github.com/BaeTab/GitTab/issues) and report it to the developers.

---

## Repository Not Recognized

### Symptoms
- You opened a folder but got a message: "This is not a Git repository."
- Clicking **"Open"** from the right-click menu doesn't load the repository.

### Solutions

#### Check 1: Is there a .git folder?

1. Open the folder in Windows Explorer.
2. Go to the top menu → **"View"** → check **"Hidden items"**.
3. Look for a **`.git`** folder.

**If the `.git` folder is missing:**

The folder is not yet a Git repository. Initialize it:

1. Open the folder in Git Tab, then select **"Initialize Repository"** from the menu.
2. Or right-click the folder in Windows Explorer → **"Git Tab"** → **"Initialize"**.

#### Check 2: Verify Folder Permissions

1. Right-click the folder → **"Properties"**.
2. Click the **"Security"** tab and verify you have read permissions.
3. If not, click **"Edit"** to add yourself with appropriate permissions.

#### Check 3: Check the Folder Path

1. Very long paths (260+ characters) or paths with many special characters can cause issues.
2. If possible, move or rename the folder to a simpler path.
   ```
   ✗ Bad:   C:\Users\user\Desktop\2024project\special★name\gitrepo
   ✓ Good:  C:\Projects\my-repo
   ```

---

## Resolving Merge Conflicts

### Symptoms
A message appears: "Conflict detected during Push/Pull or merge."

### Detailed Solution

See the [Features](Features) page under the **"Resolve Conflicts"** section for step-by-step guidance with visuals.

If the conflict is too complex:

1. In Git Tab, select **"Abort Merge"** or **"Cancel Merge"**.
2. You'll return to the original state.
3. Ask a teammate which lines they edited, then work together to resolve.

---

## Performance Issues or High Memory Usage

### Symptoms
- Git Tab responds slowly.
- It takes a long time to open large repositories.

### Solutions

#### Method 1: Clear Cache

1. Click **⚙ Settings** → **"Advanced"** section.
2. Click **"Clear Cache"**.

#### Method 2: Remove Unused Repositories

1. In the recent repositories list, right-click unused repositories → **"Remove"**.

#### Method 3: Maintain the Repository

For very large repositories (thousands of commits):

1. Visit your repository on GitHub → **"Settings"** → **"Danger Zone"** → **"Clean up repository"**.
2. Or ask someone who knows git commands to run `git gc`.

---

## Something Else Is Wrong

### Troubleshooting Steps

1. Check this page for similar symptoms.
2. Read the [FAQ](FAQ) section.
3. Review the [Features](Features) page for the related feature.
4. Search [GitHub Issues](https://github.com/BaeTab/GitTab/issues). (Your issue might already be solved!)
5. If not resolved, click **"New issue"** on [GitHub Issues](https://github.com/BaeTab/GitTab/issues) and include:
   - A description of what you see
   - Steps to reproduce it
   - Your log file (if relevant)

---

## Still Not Working?

1. Follow the "[Where Do I Find Error Logs?](#where-do-i-find-error-logs)" section above to get your log file.
2. Open the log file in a text editor and look for error messages.
3. Report on [GitHub Issues](https://github.com/BaeTab/GitTab/issues) and include:
   - What you tried to do
   - Step-by-step reproduction
   - The error message from the log file
   - Your Windows version (Settings → System → About)
   - Your .NET Runtime version (Run `dotnet --list-runtimes` in PowerShell, or check Programs and Features)

The developers will help as quickly as possible!
