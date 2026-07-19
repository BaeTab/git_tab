**English** · [한국어](Git-기초)

# Git Basics

**The 7 most important concepts for beginners learning Git. Don't worry—it's easier than it sounds!**

---

## Repository

**Repository = Project Folder + Complete Change History**

A folder where files are managed with Git. Every change is recorded, so you can always ask "Who changed this file and what did they change?" at any time.

**In Git Tab:** Open a folder in the main window to load the repository. The repository name appears in the top left.

---

## Commit

**Commit = Snapshot of File Changes**

When you modify a file and want to save that state, you record it with one click—like a save point in a video game. Add a description (commit message) to each commit so you remember why you made those changes later.

> **Example:** "Fix signup form bug", "Add dark theme", "Refactor login logic"

**In Git Tab:** In the "Commit" section on the right, select files and enter a message, then click "Commit". A new circle appears in the commit graph on the left.

---

## Branch

**Branch = Parallel Workspace**

Work on new features without touching the main code (usually `main` or `master`). It's like trying a different story path in a game using a separate save file. When you're ready, you merge it back to the main branch.

> **Example:** Create a `feature/new-feature` branch from `main` → test → merge back to `main`

**In Git Tab:** Create a new branch in the "Branch" tab at the top. You can view the branch list or switch branches (checkout).

---

## Staging Area

**Staging = "Prep Zone" Before Commit**

Modifying a file doesn't automatically commit it. You need to choose which files to commit now—this is called "staging". Useful when you've edited 10 files but only want to commit 5 of them.

**In Git Tab:** The "Changes" list appears on the right. Click the checkbox next to files you want to commit, or right-click and select "Stage".

---

## Remote

**Remote = Repository on Another Computer or Server**

Back up your code or share it with others by pushing your repository to servers like GitHub or GitLab. This is called a "remote". The most common name is `origin`.

**In Git Tab:** Check the "Remote" section in Settings. Set your GitHub repository URL to complete the remote connection.

---

## Push / Pull / Fetch

These three often confuse beginners. Here's a simple breakdown:

### Push
Upload your commits from your computer to a server like GitHub/GitLab. It means "I want to share or back up what I've done."

**In Git Tab:** Click the "Push" button in the top right. If needed, enter your username and token.

### Pull
Download the latest version from the server to your computer. Use Pull when you want new commits from teammates. It's "Fetch + Merge" in one action.

**In Git Tab:** Click the "Pull" button in the top right.

### Fetch
Check the server for new changes without merging yet. It's "just looking at what's new." Safer than Pull.

**In Git Tab:** Find and click the "Fetch" button.

> **Tip:** At first, just remember "Pull is the simplest."

---

## Merge and Conflict

### Merge
Combine two branches. Use it when you say "I finished the new feature—time to add it to the main code!"

**In Git Tab:** In the "Branch" tab on the right, select the branch to merge, right-click → select "Merge".

### Conflict
Happens when two people edit the same part of the same file differently. Git asks "Which one should I keep?"

**Example:**
```
<<<<<<< HEAD
Your change
=======
Someone else's change
>>>>>>> feature
```

**In Git Tab:** When a conflict happens, the file is specially marked. Use the "Resolve Conflict" option in Git Tab's right panel to choose one, or edit it yourself.

> **It's scary at first, but with 1–2 teammates, it almost never happens.**

---

## Tag

**Tag = Label for a Specific Commit**

Use tags when you want to mark "This commit is version `1.0.0`". Later, you can easily find "So version 1.0.0 was here—what did we do up to this point?"

**In Git Tab:** Right-click a commit → "Create Tag". Enter the tag name and you're done.

---

## Stash

**Stash = Temporary Storage**

You're working on files but don't want to commit yet? Stash them temporarily and pull them back later. Handy when you need to urgently fix a bug but can't switch branches because of your current work.

**In Git Tab:** Click "Save Stash" in the right menu. Later, select "Pop Stash" to resume work.

---

## Summary

| Term | Meaning | When to Use |
|---|---|---|
| **Repository** | Project folder + change history | Always, from start to finish |
| **Commit** | Snapshot of changes | After finishing edits |
| **Branch** | Parallel workspace | When developing a new feature |
| **Staging** | Prep before commit | When selecting specific files |
| **Remote** | Repository on a server | When connecting to GitHub, etc. |
| **Push** | Upload to server | After finishing work, to share |
| **Pull** | Download from server | When getting teammate's code |
| **Merge** | Combine branches | After completing a new feature |
| **Conflict** | Merge collision | When both edit the same spot |
| **Tag** | Version label | When releasing a version |
| **Stash** | Temporary storage | When urgently switching tasks |

---

## Next Steps

- **Want to know how to do things in Git Tab?** Read the [Features](Features) page.
- **Need to connect GitHub?** Check the [Authentication](Authentication) page.
