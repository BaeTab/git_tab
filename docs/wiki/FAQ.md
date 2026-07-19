**English** · [한국어](자주-묻는-질문)

# FAQ (Frequently Asked Questions)

**The 10 most common questions from Git Tab users.**

---

## Q1: Do I need to have Git installed separately?

**A:** No! Git Tab includes all the Git tools you need as part of the installation. You don't need to install anything else.

- Just install Git Tab and start using it.
- You don't need to use the command line (CMD, PowerShell, Git Bash).
- You can do everything with your mouse.

---

## Q2: Is Git Tab really free?

**A:** Yes, 100% free and **open source**.

- No subscription fees or licensing costs.
- No ads.
- Full source code is on GitHub.
- Anyone can freely use, share, and modify it.

---

## Q3: Does my code get sent to external servers? Is it safe?

**A:** Completely safe.

- Git Tab runs **only on your computer** (it's a local app).
- Your code is not automatically sent anywhere.
- To upload to GitHub/GitLab, you must **manually click "Push"**.
- Your personal access token is stored only in Windows Credential Manager (offline and secure).

---

## Q4: I made a mistake in a commit. How do I fix it?

**A:** There are several ways to undo or fix commits.

### Method 1: Fix the Last Commit (Amend)

If your last commit was a mistake:

1. Modify the files in the "Changes" section on the right.
2. If you want to change the commit message, enter a new one.
3. Check the **"Amend"** checkbox.
4. Click **"Commit"**.

The last commit will be updated with your changes.

### Method 2: Undo a Commit (Reset)

1. Right-click a commit in the history.
2. Select **"Reset"**.
3. Choose a reset type:
   - **Soft:** Keep files unchanged, only remove the commit
   - **Mixed:** Undo both the commit and changes to files
   - **Hard:** Completely delete all changes (be careful!)

### Method 3: Revert (Create an Undo Commit)

To undo an old commit safely:

1. Right-click the commit → **"Revert"**.
2. A new "undo" commit is created.
3. This method is safe even if you've already pushed to GitHub.

---

## Q5: I accidentally deleted a file! Can I get it back?

**A:** Yes, you can recover it.

### Method 1: Recover from a Previous Commit

1. In the commit history on the left, find the commit where the file still existed.
2. Right-click that commit → **"Checkout"** or **"Restore Files"**.
3. The file is restored.

### Method 2: Recover from the Changes Panel

1. In the "Changes" section on the right, find the deleted file.
2. Right-click it → **"Discard"**.
3. The file is restored to its state from the last commit.

### Important Note

Git saves the entire history of all commits. Even if a file was deleted years ago, you can still recover it from an old commit!

---

## Q6: What is a branch?

**A:** See the [Git Basics](Git-Basics) page for a detailed explanation. In short:

**Branch = A parallel workspace for development**

Think of it like different save files in a video game. You can develop new features in a separate branch without touching the main code.

```
main ────o────o────o  (main code)
         \
          o──o──o  (new feature branch)
               \
                o──o (another feature)
```

---

## Q7: Push isn't working! Why?

**A:** The three most common causes:

### Cause 1: You Haven't Authenticated

- You haven't logged in to GitHub/GitLab yet.
- **Fix:** Follow the [Authentication](Authentication) page to set up your personal access token.

### Cause 2: You Haven't Made Any Commits

- You've edited files but haven't committed them yet.
- **Fix:** Select files in the right panel, enter a commit message, and click **"Commit"**.

### Cause 3: No Internet Connection

- Your internet might be down.
- **Fix:** Check your internet connection and try again.

For more details, see the [Troubleshooting](Troubleshooting) page.

---

## Q8: How do I change the language to English or Korean?

**A:** Super simple.

1. Click the **⚙ Settings** icon in the top right.
2. Find the **"Language"** dropdown.
3. Select **"English"** or **"한국어"** (Korean).
4. The app automatically restarts and switches languages.

---

## Q9: How do I switch between Light and Dark theme?

**A:** Just as easy.

1. Click **⚙ Settings**.
2. Find the **"Theme"** section.
3. Select **"Light"** or **"Dark"**.
4. The app colors change immediately.

> **Tip:** Dark theme is easier on your eyes during late-night coding sessions.

---

## Q10: How do I update Git Tab?

**A:** Git Tab has automatic updates built in.

### Automatic Updates (Recommended)

- In Git Tab's **⚙ Settings**, look for **"Auto Update"**.
- Turn it on, and new versions will download and install automatically.
- When an update is ready, you'll see a restart message. Click **"Restart"** to run the latest version.

### Manual Update

If you've turned off auto-updates:

1. Visit [GitHub Releases](https://github.com/BaeTab/GitTab/releases).
2. Download the latest installer.
3. Run the installer.
4. Your existing settings are preserved.

---

## Still Have Questions?

If you didn't find your answer here:

1. **[Features](Features)** — Detailed explanations of each feature.
2. **[Troubleshooting](Troubleshooting)** — Help with technical problems.
3. **[Git Basics](Git-Basics)** — Learn Git concepts from the start.
4. **GitHub Issues** — Search or create an issue at the [GitHub repository](https://github.com/BaeTab/GitTab/issues).
