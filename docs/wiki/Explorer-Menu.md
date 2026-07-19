# Windows Explorer Right-Click Menu

**English** · [한국어](탐색기-우클릭)

**With Windows Explorer right-click integration, you can run Git commands instantly from any folder. It's really convenient!**

---

## Enable the feature

### Step 1: Open Git Tab Settings
1. Launch Git Tab.
2. Click the **⚙ Settings** icon in the top right.

### Step 2: Enable Explorer integration
1. In the Settings window, find **"Integration"** or **"Windows Explorer"** in the left menu.
2. Check the **"Enable Explorer Context Menu"** or **"Enable Explorer Context Menu"** checkbox.
3. Click the "Save" or "Apply" button.

### Step 3: Refresh Windows Explorer (optional)
If Explorer is already open:
- Close and reopen the Explorer window, or
- Press `Ctrl + Shift + Esc` or restart explorer.exe from Task Manager.

> **Tip:** Rebooting guarantees it takes effect.

---

## Using the right-click menu

### Right-click a folder (where Git Tab isn't set up yet)

Any folder right-click will show a **"Git Tab"** or **"Git"** menu item somewhere.

> **Windows 11 users:** You may need to click "Show more options" at the bottom of the menu.

### Menu items (8 total)

When you expand the menu, you'll see these options:

#### 📂 **Open**
- Git Tab launches and loads that folder as a repository.
- If it's already a Git repository, you'll see the commit history.
- If it's not a Git repository yet, you'll see the folder structure, and you can select "Initialize Repository" from the Git Tab menu to create a new one.

#### 🆕 **Clone**
- Enter a remote repository URL from GitHub, GitLab, etc., and clone it to your PC.
- It automatically clones the repository under the folder you selected.
- If authentication is required, you'll be prompted to enter your username + token.

#### 📝 **Commit**
- A separate dialog opens.
- Changed files are automatically shown.
- Enter a commit message and click the "Commit" button.
- Fast, because you don't need to open the main app!

#### ⬇️ **Pull**
- A separate dialog opens.
- Click the "Pull" button to download the latest code from the server.
- If authentication is required, you'll be prompted for your username + token.

#### ⬆️ **Push**
- A separate dialog opens.
- Upload your local commits to GitHub/GitLab.
- If authentication is required, you'll be prompted for your username + token.

#### 🔄 **Fetch**
- A separate dialog opens.
- Check the server for updates (files don't change) and the dialog closes.

#### 📦 **Stash**
- Temporarily save your work in progress.
- A separate dialog opens where you can optionally name the stash.
- Useful when you need to save your current work and switch to another branch.

#### 📜 **Log**
- Git Tab opens in the main window and shows the commit history.
- Similar to "Open", but optimized for viewing history in detail.

---

## Example: Typical workflow

### Scenario: Edit code and upload to GitHub

1. **Edit code**  
   Modify code in your text editor (VS Code, Visual Studio, etc.).

2. **Right-click in Explorer**  
   Right-click the folder where you made changes → "Git Tab" → "Commit".

3. **Enter message & commit**  
   Type a message in the commit dialog:
   ```
   Example: "add login form validation"
   ```
   Click the "Commit" button.

4. **Upload to GitHub**  
   Right-click the same folder again → "Git Tab" → "Push".
   Click the "Push" button.

5. **Done!**  
   When the dialog closes, your code is now on GitHub.

> **Tip:** The first Push requires authentication. After that, it happens automatically.

---

## Right-click menu doesn't show?

### Things to check

**1. Is it really enabled in Settings?**
- Double-check the "Windows Explorer Context Menu" option in Git Tab Settings (⚙).

**2. Did you restart Explorer?**
- Close all Explorer windows and open a new one.
- Or reboot.

**3. Are you on Windows 11?**
- Right-click → At the bottom, click **"Show more options"**.
- The full context menu will appear.

**4. Do you need admin rights?**
- Try running Git Tab as administrator (usually not needed).

**5. Are other Git GUI tools' context menus registered?**
- If TortoiseGit, GitHub Desktop, or other tools have context menus, they might conflict.
- Disable those tools' menus and reconfigure Git Tab.

### Still not showing?

Check the **"Context menu doesn't appear"** section on the [Troubleshooting](Troubleshooting) page.

---

## Commit / Push / Pull / Fetch dialogs

### Commit dialog

```
┌─────────────────────────────────────┐
│  Commit Dialog                      │
├─────────────────────────────────────┤
│ Changed files:                      │
│ ☑ src/main.py (23 changes)         │
│ ☑ README.md (2 changes)            │
│ ☐ .gitignore (unchanged)           │
│                                     │
│ Commit message:                     │
│ ┌─────────────────────────────────┐ │
│ │ Implement login feature         │ │
│ └─────────────────────────────────┘ │
│                                     │
│ [Commit]  [Cancel]                 │
└─────────────────────────────────────┘
```

- Check the boxes for files you want to commit.
- Type your message.
- Click the "Commit" button.

### Push / Pull / Fetch dialogs

```
┌──────────────────────────┐
│ Push Dialog              │
├──────────────────────────┤
│ Branch: main             │
│ Remote: origin           │
│                          │
│ Preparing...             │
│                          │
│ [Push]  [Cancel]         │
└──────────────────────────┘
```

- Your current branch is automatically selected.
- Click the "Push / Pull / Fetch" button.
- If authentication is needed, a username + token dialog appears.

---

## Do I need admin rights?

**No!** You can set up Explorer context menu integration without admin privileges.

It does register in the Windows Registry, but Git Tab handles it automatically. (No admin needed)

---

## Next steps

- **Need GitHub authentication?** Read the [Authentication](Authentication) page.
- **Want to learn more features?** Check the [Features Overview](Features) page.
