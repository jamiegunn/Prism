import { test, expect } from "@playwright/test";

test.describe("Phase 1 - Playground", () => {
  test("playground shows system prompt editor", async ({ page }) => {
    await page.goto("/playground");
    await expect(page.locator("body")).toContainText(/system/i);
  });

  test("playground shows parameter sidebar", async ({ page }) => {
    await page.goto("/playground");
    await expect(page.locator("body")).toContainText(/temperature/i);
  });

  test("compare mode loads", async ({ page }) => {
    await page.goto("/playground/compare");
    await expect(page.locator("body")).toContainText(/comparison/i);
  });
});

test.describe("Phase 1 - Token Explorer", () => {
  test("token explorer page loads", async ({ page }) => {
    await page.goto("/token-explorer");
    await expect(page.locator("body")).toContainText(/token/i);
  });

  test("token explorer shows prediction tab", async ({ page }) => {
    await page.goto("/token-explorer");
    await expect(page.locator("body")).toContainText(/predict/i);
  });
});

test.describe("Phase 1 - Models", () => {
  test("models page loads", async ({ page }) => {
    await page.goto("/models");
    await expect(page.locator("body")).toContainText(/model management/i);
  });

  test("models page shows register button or empty state", async ({ page }) => {
    await page.goto("/models");
    await expect(page.locator("body")).toContainText(/register|no inference/i);
  });
});

test.describe("Phase 1 - History", () => {
  test("history page loads", async ({ page }) => {
    await page.goto("/history");
    await expect(page.locator("body")).toContainText(/history/i);
  });

  test("history page shows filters", async ({ page }) => {
    await page.goto("/history");
    await expect(page.locator("body")).toContainText(/search|filter/i);
  });
});
