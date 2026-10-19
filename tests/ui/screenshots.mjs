// Visual smoke test: visits the main screens with the demo data, walks through the class and student
// forms, saves desktop and 360px-wide screenshots and fails when a page is wider than the viewport.
import { chromium } from "playwright";
import { mkdir } from "node:fs/promises";

const baseUrl = process.env.BASE_URL ?? "http://localhost:5080";
const out = new URL("./output/", import.meta.url).pathname;

const viewports = {
  desktop: { viewport: { width: 1440, height: 900 } },
  mobile: { viewport: { width: 360, height: 780 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true },
};

await mkdir(out, { recursive: true });
const browser = await chromium.launch();
const problems = [];

async function shot(page, name) {
  await page.waitForLoadState("networkidle").catch(() => {});
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  if (overflow > 1) problems.push(`${name}: page is ${overflow}px wider than the viewport`);
  await page.screenshot({ path: `${out}${name}.png`, fullPage: true });
  console.log(`saved ${name}.png`);
}

function expect(condition, message) {
  if (!condition) problems.push(message);
}

const submit = (page) => Promise.all([page.waitForNavigation(), page.click("form.form-card button[type=submit]")]);
const confirm = async (page, button) => {
  await page.click(button);
  await page.waitForTimeout(400);
  await Promise.all([page.waitForNavigation(), page.click("[data-confirm-accept]")]);
};

for (const [device, options] of Object.entries(viewports)) {
  const context = await browser.newContext({ ...options, locale: "tr-TR" });
  const page = await context.newPage();

  await page.goto(`${baseUrl}/`);
  await shot(page, `${device}-dashboard`);

  await page.goto(`${baseUrl}/classes`);
  await shot(page, `${device}-classes`);
  expect((await page.locator(".class-card").count()) === 5, `${device}: expected 5 demo classes`);

  await page.locator(".class-card").first().click();
  await shot(page, `${device}-class`);
  expect((await page.locator("tbody tr").count()) === 24, `${device}: a demo class should list 24 students`);

  await page.goto(`${baseUrl}/students`);
  await shot(page, `${device}-students`);

  await page.goto(`${baseUrl}/students?search=1005`);
  await shot(page, `${device}-students-search`);
  expect((await page.locator("tbody tr").count()) === 1, `${device}: searching a school number should find one student`);

  await page.goto(`${baseUrl}/students/1`);
  await shot(page, `${device}-student`);

  await page.goto(`${baseUrl}/students/new`);
  await shot(page, `${device}-student-new`);

  if (device === "desktop") {
    // Empty class form shows client-side errors.
    await page.goto(`${baseUrl}/classes/new`);
    await page.click("form.form-card button[type=submit]");
    await page.waitForTimeout(300);
    await shot(page, `${device}-class-validation`);

    // A duplicate class name is refused regardless of case.
    await page.selectOption("#GradeLevel", "9");
    await page.fill("#Section", "a");
    await submit(page);
    await shot(page, `${device}-class-duplicate`);
    expect((await page.locator(".field-validation-error").allTextContents()).join(" ").includes("zaten var"), "9-A should be reported as existing");

    await page.fill("#Section", "c");
    await page.fill("#AdvisorName", "Deniz Uysal");
    await page.fill("#Room", "b-106");
    await submit(page);
    await shot(page, `${device}-class-created`);
    expect((await page.locator("h1").textContent()).includes("9-C"), "the new class page should open");
    const classUrl = page.url();

    // Add a student to it; names are tidied, the number must be unique.
    await page.click("a:has-text('Öğrenci ekle')");
    await page.fill("#StudentNumber", "1001");
    await page.fill("#FirstName", "ayşe nur");
    await page.fill("#LastName", "YILDIRIM");
    await submit(page);
    await shot(page, `${device}-student-duplicate-number`);
    expect(page.url().endsWith("/students/new"), "a used school number should be refused");

    await page.fill("#StudentNumber", "2001");
    await submit(page);
    await shot(page, `${device}-student-created`);
    expect((await page.locator("h1").textContent()).trim() === "Ayşe Nur Yıldırım", "names should be title-cased the Turkish way");
    const studentUrl = page.url();

    // A class with students cannot be deleted.
    await page.goto(classUrl);
    await confirm(page, "button:has-text('Sil')");
    await shot(page, `${device}-class-delete-blocked`);
    expect((await page.locator(".alert-danger").count()) > 0, "deleting a class with students should be refused");

    // After the student goes, the class can.
    await page.goto(studentUrl);
    await confirm(page, "button:has-text('Sil')");
    await page.goto(classUrl);
    await confirm(page, "button:has-text('Sil')");
    await shot(page, `${device}-class-deleted`);
    expect((await page.locator(".class-card", { hasText: "9-C" }).count()) === 0, "the empty class should be deleted");
  }

  await page.goto(`${baseUrl}/students/99999`);
  await shot(page, `${device}-not-found`);

  await context.close();
}

await browser.close();

if (problems.length > 0) {
  console.error("\nProblems:\n" + problems.map((p) => `- ${p}`).join("\n"));
  process.exit(1);
}
console.log("\nAll screens rendered without horizontal overflow.");
