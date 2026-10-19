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

  await page.goto(`${baseUrl}/attendance`);
  await shot(page, `${device}-attendance`);
  expect((await page.locator("input.btn-check").count()) === 24 * 4, `${device}: the attendance sheet should list a demo class`);

  await page.goto(`${baseUrl}/exams`);
  await shot(page, `${device}-exams`);

  await page.goto(`${baseUrl}/students/5`);
  await shot(page, `${device}-student-records`);

  if (device === "desktop") {
    // Take attendance for the last class on a past school day and mark two students.
    const options = await page.goto(`${baseUrl}/attendance`).then(() => page.locator("select[name=classId] option").all());
    const lastClass = await options[options.length - 1].getAttribute("value");
    await page.goto(`${baseUrl}/attendance?classId=${lastClass}`);
    const absent = page.locator("label.att-absent").first();
    await absent.click();
    await page.locator("label.att-late").nth(1).click();
    await shot(page, `${device}-attendance-marked`);
    expect((await page.locator("[data-tally]").textContent()).includes("1 yok"), "the tally should count the absent student");
    await Promise.all([page.waitForNavigation(), page.click("button:has-text('Yoklamayı kaydet')")]);
    await shot(page, `${device}-attendance-saved`);
    expect((await page.locator(".app-toast").textContent()).includes("1 devamsız"), "saving should report one absence");
    expect((await page.locator("input.btn-check[value=Absent]:checked").count()) === 1, "the saved sheet should keep the absent mark");

    // A future day cannot be taken.
    const nextWeek = new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 10);
    await page.goto(`${baseUrl}/attendance?classId=${lastClass}&date=${nextWeek}`);
    expect(await page.locator("button:has-text('Yoklamayı kaydet')").isDisabled(), "future days should be read-only");

    // New exam and grades.
    await page.goto(`${baseUrl}/exams/new`);
    await page.selectOption("#ClassId", { label: "9-A" });
    await page.selectOption("#CourseId", { label: "Kimya" });
    await page.fill("#Title", "Kısa sınav");
    await submit(page);
    await shot(page, `${device}-exam-sheet-empty`);
    const inputs = page.locator("input.score-input");
    await inputs.nth(0).fill("105");
    await inputs.nth(1).fill("72,5");
    await inputs.nth(2).fill("88.25");
    await Promise.all([page.waitForNavigation(), page.click("button:has-text('Notları kaydet')")]);
    await shot(page, `${device}-exam-sheet-error`);
    expect((await page.locator(".field-validation-error").count()) === 1, "a score above 100 should be flagged on its row");
    expect((await inputs.nth(1).inputValue()) === "72,5", "typed scores should be kept after an error");
    await inputs.nth(0).fill("45");
    await Promise.all([page.waitForNavigation(), page.click("button:has-text('Notları kaydet')")]);
    await shot(page, `${device}-exam-sheet-saved`);
    expect((await inputs.nth(2).inputValue()) === "88,25", "a dot decimal should be saved as 88,25");

    // A student with records cannot be deleted.
    await page.goto(`${baseUrl}/students/5`);
    await confirm(page, "button:has-text('Sil')");
    expect((await page.locator(".alert-danger").textContent()).includes("okuldan ayrıldı"), "deleting a student with records should be refused");
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
