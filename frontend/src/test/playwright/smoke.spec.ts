import { test, expect } from "@playwright/test";

test.describe("Smoke tests", () => {
  test("app loads and redirects to playground", async ({ page }) => {
    await page.goto("/");
    await expect(page).toHaveURL(/\/playground/);
  });

  test("navigation sidebar is visible", async ({ page }) => {
    await page.goto("/playground");
    await expect(page.locator("nav")).toBeVisible();
  });

  test("playground page renders", async ({ page }) => {
    await page.goto("/playground");
    await expect(page.locator("body")).toContainText(/playground/i);
  });
});
