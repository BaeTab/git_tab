**English** · [한국어](인증-설정)

# Authentication

**How to log in to GitHub/GitLab for Push and Pull operations. You only set this up once—after that, it's automatic!**

---

## Why Use a Token Instead of Your Password?

### GitHub's Policy Change

In the past, you could log in with your GitHub password. But starting in 2021, **GitHub stopped allowing password authentication.** Instead, GitHub now requires **Personal Access Tokens (PAT)**.

### What Is a Token?

A token is **a long, unique string of characters** that acts like a temporary password. Here's why it's better:

- **More secure** than a password (you can set it to expire, limit its permissions)
- **No password reset needed** if compromised—just delete the token
- **Limited scope**—can be restricted to Git operations only (not your whole account)

---

## Step 1: Create a Token on GitHub

### Log In to GitHub

1. Go to [github.com](https://github.com) in your browser.
2. Click your profile icon (circular image) in the top right.
3. Select **"Settings"**.

### Go to Developer Settings

1. Scroll down the left sidebar menu to find **"Developer settings"**. Click it.
2. In the left menu again, select **"Personal access tokens"** → **"Tokens (classic)"**.

> **Note:** There's also a newer "Fine-grained tokens" option, but "Tokens (classic)" is simpler for beginners.

### Create the Token

1. Click the **"Generate new token"** button in the top right → **"Generate new token (classic)"**.

2. Enter a name for the token:
   ```
   Examples: GitTab, GitTab-on-Windows
   ```

3. Choose an **"Expiration"** date:
   ```
   - No expiration: Never expires (convenient, but less secure)
   - 30 days, 60 days, 90 days, 1 year: Auto-expires (more secure)
   ```
   
   For your first token, **"90 days"** is a good choice. You can create a new one when it expires.

4. Under **"Select scopes"**, check these boxes:
   ```
   ✓ repo (full access to repositories)
   ✓ read:user (read user information)
   ✓ gist (access to Gists — optional)
   ```
   
   **At minimum**, you must check `repo`.

5. Scroll to the bottom and click **"Generate token"**.

### Copy the Token (Important!)

Your token is now displayed. **Once you leave this page, you won't see it again!**

- Click the **📋 copy button** next to the token.
- Or select the entire token and press `Ctrl+C`.

> ⚠️ **Never share this with anyone!** Treat it like a password.

---

## Step 2: Authenticate in Git Tab

### Try Push or Pull

1. In Git Tab, click **"Push"** or **"Pull"**.
2. If you need network access, an **authentication dialog** will appear.

### Enter Your Credentials

In the dialog box, enter:

```
Username: your_github_username
Token:    ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxx (the one you copied above)
```

- **Username:** Your GitHub account name (e.g., `john_doe`)
- **Token:** The long string you copied earlier

Click **"OK"** or **"Sign In"**.

### Auto-Saved (Windows Credential Manager)

Your credentials are securely saved in **Windows Credential Manager**.

From now on:
- Push/Pull automatically uses your credentials.
- You won't need to type them again.

---

## Step 3: Verify It Works

### Test Push

1. Modify a file and commit it.
2. Click the **"Push"** button.
3. After a moment, you should see a success message.
4. Go to `github.com/your_username/repository_name` in your browser and verify the commit appears.

### Test Pull

1. Edit a file directly on GitHub's website.
2. Enter a commit message and click **"Commit"**.
3. Back in Git Tab, click **"Pull"**.
4. The file automatically updates.

---

## Manage Saved Credentials

### View Saved Credentials

1. Open Windows Search (🔍) and search for **"Credential"**.
2. Open the **"Credential Manager"** app.
3. Click the **"Windows Credentials"** tab.
4. You'll see GitHub-related entries listed.

### Delete Saved Credentials

1. Click the entry you want to remove.
2. Click **"Remove"**.
3. The next time you Push/Pull, you'll be prompted to enter credentials again.

### Delete from Git Tab Settings

1. Open Git Tab's **⚙ Settings**.
2. Find the **"Authentication"** section.
3. Click **"Clear Saved Credentials"**. This removes credentials for the currently open repository.
4. The next Push/Pull will prompt you again.

---

## Token Expired

### Symptoms

Push/Pull fails with:
```
error: authentication failed
```

### Solution

1. Create a new token on GitHub (follow [Step 1](#step-1-create-a-token-on-github) again).

2. Delete the old token from Credential Manager (follow [the "Delete" section](#delete-saved-credentials) above).

3. Try Push/Pull in Git Tab again.

4. Enter your new token when prompted.

---

## Using GitLab or Other Services

The process is the same for other Git hosting services.

### GitLab Example

1. Log in to [gitlab.com](https://gitlab.com).

2. Click your profile icon (top right) → **"Edit profile"** → **"Access tokens"**.

3. Click **"Add new token"**.

4. Enter a token name and check these permissions:
   - `api`
   - `read_repository`
   - `write_repository`

5. Click **"Create personal access token"**.

6. Copy the token.

7. In Git Tab, when you Push/Pull:
   - **Username:** Your GitLab username or email
   - **Token:** Your GitLab token

---

## Authentication FAQ

### Q: I lost my token. What do I do?

**A:** No problem! Just create a new one. Log in to GitHub, go to Personal Access Tokens, and generate a fresh token. The old one is deleted automatically and can't be used.

### Q: I use multiple GitHub accounts.

**A:** Git Tab stores credentials **per host** (e.g., `github.com`). To switch accounts:
1. Use **⚙ Settings** → **"Clear Saved Credentials"** to delete the old account.
2. Next Push/Pull, enter the new account's username and token.
3. Or manually edit entries in Windows Credential Manager.

### Q: I use GitHub Enterprise (company GitHub).

**A:** Same process! The only difference is your GitHub URL is your company domain (e.g., `github.company.com`) instead of `github.com`.

### Q: My token leaked! Emergency!

**A:** Go to GitHub Settings → Developer settings → Personal access tokens, find the token, and click delete. That token is now useless and can't be used anywhere.

---

## Next Steps

- **Learn how to Push/Pull:** Check the [Features](Features) page.
- **Troubleshooting:** If you hit any issues, see the [Troubleshooting](Troubleshooting) page.
