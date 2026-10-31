# Billing setup

BusinessOS bills in the currency stored on the plan or regional price. The workspace operating currency is not applied to subscription amounts. Development never uses a live payment key.

## Packages

Public prices are USD and live in `SubscriptionPlans`, revision 2:

| Plan | Monthly | Annual |
|---|---:|---:|
| Free | 0 | 0 |
| Starter | 19 | 182 |
| Professional | 49 | 470 |
| Enterprise | 149 | 1,430 |

Enterprise amounts are starting prices. A system administrator sets a negotiated contract with `POST /api/v1/system/enterprise-contracts`. That call updates entitlements and the locked price. It does not change an existing provider subscription.

Paid subscribers that existed before revision 2 keep their previous price and limits until they change plan. The snapshot is stored on `TenantSubscriptions`.

## Environment variables

Leave these empty in source control. Set them in the host environment or user secrets.

```
Stripe__SecretKey
Stripe__PublishableKey
Stripe__WebhookSecret
Tap__Enabled
Tap__Sandbox
Tap__SecretKey
Tap__WebhookSecret
Tap__RecurringEnabled
PayTabs__Enabled
PayTabs__Sandbox
PayTabs__ServerKey
PayTabs__ProfileId
PayTabs__Region
Billing__WebhookBaseUrl
Billing__AllowedReturnOrigins__0
```

Development must keep `Tap:Sandbox` and `PayTabs:Sandbox` true. A live Stripe or Tap secret is rejected when `ASPNETCORE_ENVIRONMENT` is Development.

`Billing:AllowedReturnOrigins` must include the Angular origin, for example `http://localhost:4200`. Checkout success and cancel URLs outside that list are rejected.

## Stripe

1. Create a test-mode secret key and webhook signing secret.
2. Point the webhook to `https://<api>/api/v1/billing/webhook/stripe` and also keep `/api/billing/webhook/stripe`.
3. Subscribe to `checkout.session.completed`, `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted`, `invoice.paid`, and `invoice.payment_failed`.
4. Opening a success URL does not activate a subscription. Activation happens after the signed webhook, or, in Development with no Stripe key, after `POST /api/v1/billing/checkout/confirm`.

A new subscription uses Stripe Checkout. The amount uses the server price and the currency's minor units. KWD, BHD, and OMR use three decimal places. When the workspace already has a Stripe subscription, a paid plan change updates that subscription with `proration_behavior=always_invoice` and `payment_behavior=error_if_incomplete`. The local plan changes only after Stripe confirms the update. A second Checkout session is not opened for that subscription. Cancel, reactivate, and a move to Free call Stripe before the local row changes. Free takes effect at the end of the paid period.

## Tap Payments

The charge call uses `POST https://api.tap.company/v2/charges` and the hosted `transaction.url`. It runs only when `Tap:Enabled` is true and a secret key is present. The webhook `hashstring` header is verified before a captured charge can update a subscription. KNET and Mada are shown as one-time or customer-initiated methods. They are not treated as automatic card renewals.

Without a merchant account, leave `Tap:Enabled` false. The API then refuses checkout instead of reporting a successful payment.

## PayTabs

Hosted checkout uses the official `payment/request` endpoint for the configured region:

- `sa`: `https://secure.paytabs.sa`
- `ae`: `https://secure.paytabs.com`
- `eg`: `https://secure-egypt.paytabs.com`
- `om`: `https://secure-oman.paytabs.com`
- `jo`: `https://secure-jordan.paytabs.com`
- `iq`: `https://secure-iraq.paytabs.com`

The server callback is `{Billing:WebhookBaseUrl}/api/v1/billing/webhook/paytabs`. PayTabs signs the raw body in the `Signature` header. A subscription is updated only after that signature matches and `payment_result.response_status` is `A`. Automatic renewal stays off unless `PayTabs:RecurringEnabled` is set for a merchant profile that supports agreements.

## Regional prices

Add rows to `RegionalPrices` for a country and ISO currency. The API uses that row for display and for the charge. It does not convert USD with an exchange rate. If Pakistan is selected and no PKR row exists, the page keeps the USD amount and says the charge is in USD.

PKR checkout through JazzCash or EasyPaisa is rejected until a PKR regional price exists.

## AI quota

One request is consumed when Sophia accepts a non-empty message. The same client request id does not consume a second unit. A provider failure after acceptance still consumes the unit. Input tokens, output tokens, and estimated provider cost are stored for administrators and are not returned on the tenant usage API. The counter resets at the subscription period boundary.
