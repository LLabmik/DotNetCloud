# Gmail OAuth Setup Guide

How to enable Gmail connectivity in DotNetCloud via Google OAuth 2.0.

## How It Works

DotNetCloud uses the standard **OAuth 2.0 web server flow** to connect Gmail accounts:

1. User clicks "Connect Gmail" in the Email app
2. Browser redirects to Google's authorization page
3. User signs in to Google and grants permission
4. Google redirects back to your DotNetCloud server
5. DotNetCloud exchanges the authorization code for access/refresh tokens
6. Tokens are encrypted and stored per-user — DotNetCloud can then sync and send email on the user's behalf

No passwords are ever seen or stored by DotNetCloud. Each user authorizes their own Gmail account individually.

---

## Prerequisites

- A Google account
- Access to the [Google Cloud Console](https://console.cloud.google.com/)
- Your DotNetCloud server's public URL (e.g., `https://mydomain.com`)

---

## Step-by-Step Setup

### Step 1: Create a Google Cloud Project

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Click the project dropdown at the top → **New Project**
3. Name it (e.g., "DotNetCloud") and click **Create**
4. Wait for the project to be created, then select it from the dropdown

### Step 2: Enable the Gmail API

1. Go to [APIs & Services → Library](https://console.cloud.google.com/apis/library)
2. Search for "**Gmail API**"
3. Click **Gmail API** → **Enable**

### Step 3: Configure the OAuth Consent Screen

1. Go to [APIs & Services → OAuth consent screen](https://console.cloud.google.com/apis/credentials/consent)
2. Choose **External** user type (unless all your users are in a Google Workspace organization)
3. Click **Create**
4. Fill in:
   - **App name**: "DotNetCloud" (or your instance name)
   - **User support email**: your email address
   - **Developer contact information**: your email address
5. Click **Save and Continue**
6. On the **Scopes** page, click **Add or Remove Scopes** and add:
   - `https://www.googleapis.com/auth/gmail.modify` (read, send, and manage email)
   - `https://www.googleapis.com/auth/gmail.send` (send email only)
7. Click **Save and Continue**
8. On the **Test users** page, add your own email as a test user (required while app is in "Testing" status)
9. Click **Save and Continue**

> **Note:** While in "Testing" mode, only up to 100 test users can connect. To allow all users, you'll need to publish the app later.

### Step 4: Create OAuth 2.0 Credentials

1. Go to [APIs & Services → Credentials](https://console.cloud.google.com/apis/credentials)
2. Click **Create Credentials** → **OAuth client ID**
3. Select **Web application** as the application type
4. Name it (e.g., "DotNetCloud Web")
5. Under **Authorized redirect URIs**, click **Add URI** and enter:
   ```
   https://YOUR_SERVER_URL/api/v1/email/gmail/oauth/complete
   ```
   Replace `YOUR_SERVER_URL` with your DotNetCloud server's hostname (e.g., `mydomain.com:5443`).

   > ⚠️ The redirect URI must **exactly match** what your users' browsers will use to reach your server. If you use both `mydomain.com` and `www.mydomain.com`, add both.

6. Click **Create**
7. Copy the **Client ID** and **Client Secret** shown — you'll need them in the next step

### Step 5: Configure DotNetCloud

Add the credentials to your server's `appsettings.json`:

```json
{
  "Email": {
    "Gmail": {
      "ClientId": "YOUR_CLIENT_ID.apps.googleusercontent.com",
      "ClientSecret": "YOUR_CLIENT_SECRET"
    }
  }
}
```

Or set them as environment variables:
```bash
export Email__Gmail__ClientId="YOUR_CLIENT_ID.apps.googleusercontent.com"
export Email__Gmail__ClientSecret="YOUR_CLIENT_SECRET"
```

**The `RedirectUri` is auto-detected** from the request URL — you don't need to set it unless you want to override it:
```json
{
  "Email": {
    "Gmail": {
      "RedirectUri": "https://custom-callback.example.com/api/v1/email/gmail/oauth/complete"
    }
  }
}
```

### Step 6: Restart DotNetCloud

```bash
sudo systemctl restart dotnetcloud.service
```

The Gmail tab in the Email app will now show the **Connect Gmail** button. Users can click it to authorize their Gmail accounts.

---

## Verification

1. Open DotNetCloud → **Email** app
2. Click **+ Account** → **Gmail (OAuth)** tab
3. Click **Connect Gmail**
4. You should be redirected to Google's sign-in page
5. Sign in with a Google account and grant the requested permissions
6. You'll be redirected back to DotNetCloud with a "Gmail account connected" message

---

## Troubleshooting

### "Gmail OAuth not yet configured"
The server doesn't have valid OAuth credentials. Check that `Email:Gmail:ClientId` is set in `appsettings.json`.

### "redirect_uri_mismatch" from Google
The redirect URI your browser used doesn't match what's configured in Google Cloud Console.
- Check the authorized redirect URIs in [Credentials](https://console.cloud.google.com/apis/credentials)
- Ensure the URL includes the full path: `/api/v1/email/gmail/oauth/complete`
- Ensure the scheme (`http` vs `https`) matches
- If using a non-standard port (e.g., `:5443`), it must be included

### "access_denied" from Google
The user declined the authorization. This is normal — they can try again.

### "invalid_client" from Google
The Client ID or Client Secret is incorrect. Double-check the values from the Google Cloud Console.

### App shows "Unverified App" warning
While your OAuth consent screen is in "Testing" mode, Google shows a warning. To remove it:
1. Go to [OAuth consent screen](https://console.cloud.google.com/apis/credentials/consent)
2. Click **Publish App** under the "Testing" section
3. Note: Google may require app verification if you have many users

---

## Publishing to Production

When you're ready to let all users connect (not just test users):

1. Go to [OAuth consent screen](https://console.cloud.google.com/apis/credentials/consent)
2. Click **Publish App**
3. Google may require [app verification](https://support.google.com/cloud/answer/9110914) if you use sensitive scopes like Gmail
4. The verification process can take several days — plan ahead

---

## Using a Shared Project Client (Future)

In the future, the DotNetCloud project may provide a shared Google Cloud OAuth client
that all installations can use out of the box — similar to how Thunderbird bundles
Mozilla's OAuth credentials. When this is available, no manual Google Cloud setup
will be required. Server admins who prefer to use their own Google Cloud project can
still override the credentials via configuration.

---

## Reference

- [Google OAuth 2.0 for Web Server Applications](https://developers.google.com/identity/protocols/oauth2/web-server)
- [Gmail API Scopes](https://developers.google.com/gmail/api/auth/scopes)
- [Google Cloud Console](https://console.cloud.google.com/)
