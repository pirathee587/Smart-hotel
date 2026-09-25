import { expect, test } from "@playwright/test";

const roles = [
  ["Owner", "OWNER", "/owner"],
  ["Front Office Receptionist", "FRONT_OFFICE", "/dashboard/bookings"],
  ["Housekeeping Manager", "HOUSEKEEPING_MANAGER", "/dashboard/housekeeping"],
  ["Housekeeper", "HOUSEKEEPER", "/dashboard/tasks"],
  ["Maintenance Manager", "MAINTENANCE_MANAGER", "/dashboard/maintenance"],
  ["Technician", "TECHNICIAN", "/dashboard/tasks"],
  ["Finance Manager", "FINANCE_MANAGER", "/dashboard/finance"],
  ["Chef", "CHEF", "/dashboard/kds"],
  ["Waiter", "WAITER", "/dashboard/orders"],
] as const;

for (const [label, envPrefix, allowedPath] of roles) {
  const email = process.env[`E2E_${envPrefix}_EMAIL`];
  const password = process.env[`E2E_${envPrefix}_PASSWORD`];

  test(`${label} authenticates through the real Identity service and reaches its dashboard`, async ({ page }) => {
    test.skip(!email || !password, `Set E2E_${envPrefix}_EMAIL and E2E_${envPrefix}_PASSWORD for the disposable integration account.`);

    await page.goto("/staff/login");
    await page.getByLabel("Work email address").fill(email!);
    await page.getByLabel("Password").fill(password!);
    await page.getByRole("button", { name: /sign in/i }).click();
    await page.waitForURL((url) => !url.pathname.includes("/staff/login"));

    await page.goto(allowedPath);
    await expect(page).toHaveURL(new RegExp(allowedPath.replaceAll("/", "\\/")));
    await expect(page.locator("body")).not.toContainText(/unauthorized|access denied/i);
  });
}
