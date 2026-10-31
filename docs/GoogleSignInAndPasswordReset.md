# Google sign-in and password reset

BusinessOS keeps one authentication system. Google sign-in validates a Google ID token and then issues the existing BusinessOS JWT plus the existing refresh cookie. Password reset uses ASP.NET Core Identity's data-protection token provider. Neither feature creates a second user store or a second session format.

## Chosen Google flow

The Angular app uses Google Identity Services (`https://accounts.google.com/gsi/client`). After the user consents, Google returns an ID token. The browser posts that token to `POST /api/v1/auth/google`. The API validates it with `Google.Apis.Auth` (`GoogleJsonWebSignature.ValidateAsync`), which checks the signature against Google's published keys, the issuer, the audience, and the expiry. BusinessOS then issues its own session.

This flow does **not** use a client secret. Do not put a Google client secret in Angular environment files, `appsettings.json`, or source control.

| Value | Where it lives | Secret? |
| --- | --- | --- |
| Google OAuth client ID | Angular `environment.googleClientId` and API `Google:ClientId` | No. It is a public identifier. The two values must match. |
| Google client secret | Not used | Do not create one for this SPA flow, and do not commit one if Google Console shows it. |
| Email provider password / API key | API `Email:Password` or the host's secret store | Yes |
| Password-reset token | Only in the email link, then in memory on the reset page | Yes. It is not stored in `localStorage`. |

## API routes

All routes are under `/api/v1`.

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| POST | `/api/v1/auth/google` | Anonymous | Validate a Google ID token. Returns a BusinessOS session, or `registration_required` with a 10-minute continuation token. |
| POST | `/api/v1/auth/google/complete` | Anonymous | Create a new tenant and owner from that continuation token plus a business name. |
| POST | `/api/v1/auth/forgot-password` | Anonymous | Request a reset email. The public response does not reveal whether the account exists. |
| POST | `/api/v1/auth/reset-password` | Anonymous | Set a new password with an Identity reset token. Does not sign the user in. |
| GET | `/api/v1/account/external-logins` | Bearer JWT | Whether the current user has a password and a linked Google account. |
| POST | `/api/v1/account/external-logins/google` | Bearer JWT | Link Google to the signed-in user. |
| DELETE | `/api/v1/account/external-logins/google` | Bearer JWT | Unlink Google. Refused unless the user already has a local password. |
| POST | `/api/v1/account/password` | Bearer JWT | Add a local password to a Google-only account. |

Existing email/password login, refresh cookies, and tenant checks are unchanged.

## Account rules

- A returning Google subject that is already linked signs in to that user's existing tenant. No second user is created.
- A verified Google email that matches a password account is **not** linked automatically. The user must sign in with the existing password and connect Google from Profile.
- A brand-new Google user does not join an existing tenant. They supply a business name, and BusinessOS runs the same tenant-creation path as email registration, including the Owner role for that new tenant only.
- Google profile claims never grant Admin or any other role.
- `X-Tenant-ID` is ignored for these anonymous auth routes. The new tenant id is generated on the server.
- Google-only accounts do not receive a local password-reset email. The public forgot-password response stays the same.
- A successful password reset updates the Identity password hash, invalidates the reset token, increments `SecurityVersion` (so existing access tokens fail the tenant middleware), and revokes refresh sessions. The user must sign in again.

## Google Cloud Console

1. Open [Google Cloud Console](https://console.cloud.google.com/) and create or select a project.
2. Configure the OAuth consent screen.
   - User type: External, unless this is a private Google Workspace app.
   - App name: BusinessOS.
   - User support email and developer contact email: your real addresses.
   - Scopes: `openid`, `email`, and `profile` only. Do not request extra scopes.
   - While the app is in Testing, add every Google account that should be allowed to sign in as a test user. Unlisted accounts are rejected by Google.
3. Publish the consent screen when you are ready for users outside the test list. Google verification is required only if you later request sensitive or restricted scopes. `openid`, `email`, and `profile` do not require that verification for a normal sign-in button.
4. Create an OAuth client.
   - Application type: **Web application**.
   - This SPA flow does not use a client secret. You can ignore the secret Google may display.
5. Authorized JavaScript origins (no path, no trailing slash):
   - Local: `http://localhost:4200`
   - Production: `https://your-frontend-domain`
6. Authorized redirect URIs: leave this empty for the Google Identity Services ID-token button. Do not invent a callback such as `/signin-google`. The button posts the ID token to the BusinessOS API; Google does not redirect the browser to the API.
7. Copy the client ID (it ends with `.apps.googleusercontent.com`).

Set the same client ID in both places:

```text
Google__ClientId=YOUR_CLIENT_ID.apps.googleusercontent.com
```

Angular, local `src/environments/environment.ts` and production `src/environments/environment.prod.ts`:

```ts
googleClientId: 'YOUR_CLIENT_ID.apps.googleusercontent.com'
```

For local API secrets, prefer user secrets instead of editing `appsettings.json`:

```powershell
cd BusinessOS\BusinessOS.API
dotnet user-secrets set "Google:ClientId" "YOUR_CLIENT_ID.apps.googleusercontent.com"
```

Restart the API after changing configuration. Until `googleClientId` is set, the login and register pages show a disabled Continue with Google button and do not pretend that sign-in succeeded.

## Email delivery

Password reset reuses `IEmailNotificationService`. Delivery uses MailKit SMTP. When `Email:Enabled` is false, or `Email:SmtpHost` is empty, the message is not sent and the reset link is not written to the logs. The API still returns the generic success message so callers cannot tell whether the mailbox exists.

Local development options:

- [Mailpit](https://github.com/axllent/mailpit) or smtp4dev on `localhost:1025` with `UseSsl` false.
- A provider sandbox such as Mailtrap.

Example local settings (placeholders only):

```json
"Email": {
  "Enabled": true,
  "SmtpHost": "localhost",
  "SmtpPort": 1025,
  "UseSsl": false,
  "Username": "",
  "Password": "",
  "FromAddress": "noreply@localhost",
  "FromName": "BusinessOS",
  "TimeoutSeconds": 15
}
```

Production environment variables:

```text
Email__Enabled=true
Email__SmtpHost=smtp.your-provider.com
Email__SmtpPort=587
Email__UseSsl=true
Email__Username=YOUR_SMTP_USERNAME
Email__Password=YOUR_SMTP_PASSWORD
Email__FromAddress=noreply@your-domain.com
Email__FromName=BusinessOS
Email__TimeoutSeconds=15
App__FrontendBaseUrl=https://your-frontend-domain
App__PasswordResetTokenHours=24
```

`App__FrontendBaseUrl` is the only origin used in reset links. The request Host header is ignored. In Production the API refuses to start unless this value is an absolute `https://` URL.

Verify the sender domain with your email provider (SPF, DKIM, and the provider's from-address check) or the messages will be dropped or marked as spam. The reset email states a 24-hour expiry by default, matching `DataProtectionTokenProviderOptions.TokenLifespan`.

## Abuse controls already in place

- The `/api/v1/auth` group is limited to 10 requests per minute per IP.
- Each email address can trigger at most 5 reset emails per hour. Further requests still return the generic success message and do not send mail.
- Invalid Google tokens and invalid reset tokens return generic errors. Password-policy failures are the exception, because the user must correct the new password.
- ID tokens, reset tokens, and email bodies are not written to logs.
- HTTPS redirection and HSTS are enabled outside Development. The auth cookies are unchanged.

## What you must do before this works end to end

1. Create the Google OAuth web client and set the client ID in the API and in the Angular environment.
2. Add `http://localhost:4200` as an authorized JavaScript origin, and your production origin when you deploy.
3. While the consent screen is in Testing, add your Google account as a test user.
4. Point `Email` at a real SMTP server or a local catcher, and set `Email:Enabled` to true.
5. Set `App:FrontendBaseUrl` to the URL you actually open in the browser (`http://localhost:4200` locally, `https://...` in production).
6. Restart the API and rebuild the Angular app after changing the client ID.

No database migration is required. Google links use the existing ASP.NET Core Identity `AspNetUserLogins` table.
