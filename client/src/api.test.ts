import { describe, expect, it } from "vitest";
import { money } from "./api";

describe("money", () => {
  it("formats ticket prices in Vietnamese dong without fractional digits", () => {
    expect(money(690000)).toContain("690.000");
    expect(money(1490000)).toContain("1.490.000");
  });

  it("formats zero safely for draft events", () => {
    expect(money(0)).toContain("0");
  });
});
