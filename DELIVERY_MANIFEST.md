# NOVA Tickets — Final Delivery Manifest

## Release

This package corresponds to checkpoint `b3da9d39`. The production deployment was updated automatically when the checkpoint was saved. The project uses ASP.NET Core 8 for the backend and React/TypeScript for the frontend.

## Included

The archive contains the complete application source, backend domain/services/controllers, React storefront and admin console, C# Entity Framework migrations, plain SQL migrations `001` through `005`, Dockerfile, integration scripts, Vitest tests, API/architecture/testing/database documentation, and the DH-format technical solution report.

## Verified flows

The integration suite passed against an isolated SQLite database. It covers registration and password policy, login/logout, lockout, session revocation, booking ownership, concurrent seat holding where exactly one request succeeds, expired-hold cleanup, booking/payment idempotency, offline email creation, customer email preview, and an admin retry transition from a genuinely `Failed` outbox row to `Sent`.

## Demo limitations

The email implementation is intentionally an Offline Email Outbox. It renders and persists HTML/text content and exposes customer/admin preview and retry APIs, but it does not send to Gmail or Outlook without a future SMTP/Resend adapter. Payment is an internal demo provider and is not connected to a live payment gateway.

## Exclusions

Build outputs, `node_modules`, `.git`, deployment logs, local environment files, private keys, credentials, and runtime secrets are excluded. Configure the required environment variables in the deployment environment rather than committing them.

## Run locally

Use the commands documented in `README.md` and `DATABASE_USAGE.md`. For the complete verification flow, run `pnpm test`; for the backend-only build, run `dotnet build NovaTickets.sln`.
