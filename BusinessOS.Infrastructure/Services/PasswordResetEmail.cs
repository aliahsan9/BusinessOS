using System.Text.Encodings.Web;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Notifications.DTOs;

namespace BusinessOS.Infrastructure.Services;

public static class PasswordResetEmail
{
    public static EmailMessage Compose(string toEmail, string resetUrl, int expiryHours)
    {
        var hours = Math.Clamp(expiryHours, 1, 168);
        var safeUrl = HtmlEncoder.Default.Encode(resetUrl);
        var text =
            $"""
            You requested a password reset for your BusinessOS account.

            Choose a new password using the link below. It expires in {hours} hours:
            {resetUrl}

            If you did not request this, you can ignore this email. Your password will not change.
            BusinessOS support will never ask for your password.
            """;

        var html =
            $"""
            <!DOCTYPE html>
            <html lang="en">
            <body style="margin:0;padding:24px;background:#f4f7f7;font-family:Arial,sans-serif;color:#1f2933;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" style="max-width:560px;background:#ffffff;border-radius:12px;padding:32px;">
                      <tr>
                        <td>
                          <p style="margin:0 0 8px;font-size:13px;letter-spacing:0.08em;text-transform:uppercase;color:#0d6e6e;">BusinessOS</p>
                          <h1 style="margin:0 0 16px;font-size:22px;">Reset your password</h1>
                          <p style="margin:0 0 16px;line-height:1.5;">A password reset was requested for your BusinessOS account. Use the button below to choose a new password. This link expires in {hours} hours and can only be used once.</p>
                          <p style="margin:0 0 24px;">
                            <a href="{safeUrl}" style="display:inline-block;background:#0d6e6e;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:700;">Reset password</a>
                          </p>
                          <p style="margin:0 0 12px;line-height:1.5;font-size:14px;color:#52606d;">If the button does not work, copy this address into your browser:<br>{safeUrl}</p>
                          <p style="margin:0 0 12px;line-height:1.5;font-size:14px;color:#52606d;">If you did not request this, you can ignore this email. Your password will not change.</p>
                          <p style="margin:0;line-height:1.5;font-size:14px;color:#52606d;">BusinessOS support will never ask for your password.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        return new EmailMessage(toEmail, "Reset your BusinessOS password", text, html);
    }
}
