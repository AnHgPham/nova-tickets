import { describe, expect, it } from "vitest";

const baseUrl = process.env.NOVA_TEST_BASE_URL;
const schedulerKey = process.env.NOVA_SCHEDULER_KEY;

describe("scheduler cleanup secret", () => {
  it.skipIf(!baseUrl || !schedulerKey)("authenticates the protected cleanup callback with the configured secret", async () => {
    const response = await fetch(`${baseUrl}/api/scheduled/release-expired-holds`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Nova-Cleanup-Key": schedulerKey!,
      },
      body: "{}",
    });
    expect(response.status).toBe(200);
  });
});
