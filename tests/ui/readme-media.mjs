// Regenerates the README images (docs/screenshots) from the demo data on a fresh database.
// The walkthrough video is saved as output/media/walkthrough.webm and turned into docs/screenshots/demo.gif.
import { chromium } from "playwright";
import { mkdir, readdir, rename } from "node:fs/promises";

const baseUrl = process.env.BASE_URL ?? "http://localhost:5080";
const out = new URL("./output/media/", import.meta.url).pathname;
const pause = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

await mkdir(out, { recursive: true });
const browser = await chromium.launch();

async function shot(page, name) {
  await page.waitForLoadState("networkidle").catch(() => {});
  await page.screenshot({ path: `${out}${name}.png` });
  console.log(`saved ${name}.png`);
}

{
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 }, locale: "tr-TR" });
  const page = await context.newPage();

  await page.goto(`${baseUrl}/`);
  await shot(page, "dashboard");
  await page.goto(`${baseUrl}/attendance?classId=4`);
  await shot(page, "attendance");
  await page.goto(`${baseUrl}/classes/1`);
  await shot(page, "class");
  await page.goto(`${baseUrl}/students/5`);
  await shot(page, "student");
  await page.goto(`${baseUrl}/exams?classId=1`);
  await shot(page, "exams");
  await page.locator("td a", { hasText: "2. yazılı" }).first().click();
  await shot(page, "exam-sheet");

  await context.close();
}

{
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true, locale: "tr-TR" });
  const page = await context.newPage();
  await page.goto(`${baseUrl}/attendance?classId=4`);
  await shot(page, "mobile-attendance");
  await page.goto(`${baseUrl}/students/5`);
  await shot(page, "mobile-student");
  await context.close();
}

// Walkthrough: dashboard → take attendance for a class still waiting → student record → grade sheet.
{
  const context = await browser.newContext({
    viewport: { width: 1280, height: 800 },
    locale: "tr-TR",
    recordVideo: { dir: out, size: { width: 1280, height: 800 } },
  });
  const page = await context.newPage();

  await page.goto(`${baseUrl}/`);
  await pause(2000);
  await Promise.all([page.waitForNavigation(), page.locator("td a", { hasText: "10-B" }).first().click()]);
  await pause(1200);
  await page.locator("label.att-absent").nth(2).click();
  await pause(500);
  await page.locator("label.att-late").nth(6).click();
  await pause(500);
  await page.locator("label.att-excused").nth(9).click();
  await pause(900);
  await page.mouse.wheel(0, 900);
  await pause(800);
  await Promise.all([page.waitForNavigation(), page.click("button:has-text('Yoklamayı kaydet')")]);
  await pause(2200);

  await Promise.all([page.waitForNavigation(), page.click(".side-link:has-text('Özet')")]);
  await pause(1200);
  await Promise.all([page.waitForNavigation(), page.locator(".absence-list a").first().click()]);
  await pause(2400);

  await Promise.all([page.waitForNavigation(), page.click(".side-link:has-text('Sınavlar')")]);
  await pause(1200);
  await Promise.all([page.waitForNavigation(), page.locator("td a").first().click()]);
  await pause(2200);

  await context.close();
  const video = (await readdir(out)).find((name) => name.endsWith(".webm") && name !== "walkthrough.webm");
  if (video) await rename(`${out}${video}`, `${out}walkthrough.webm`);
  console.log("saved walkthrough.webm");
}

await browser.close();
