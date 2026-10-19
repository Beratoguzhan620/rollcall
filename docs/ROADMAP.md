# Rollcall — Yol Haritası

Sınıf, öğrenci, yoklama ve not takibi yapan okul yönetim paneli.

- **Önerilen repo adı:** `rollcall`
- **Teknoloji:** ASP.NET Core MVC (.NET 10), EF Core, SQL Server, Bootstrap 5 + özel SCSS, xUnit
- **Kurallar:** `CONTRIBUTING.md` · **Tasarım:** `docs/DESIGN.md`

Her aşama ayrı bir branch ve PR olarak yapılır (`stage/<no>-<kisa-ad>`). Bir aşama ancak tüm maddeleri bitince, build/testler geçince ve ekran görüntüleri eklenince tamamlanmış sayılır. Biten maddeler `[x]` yapılır.

> Kapsam: EduTrack'ten farklılaşması için "Rollcall" — sınıf, öğrenci, **yoklama** ve **not** takibi.

## Aşama 1 — Temel ve tasarım
- [x] Ad/marka "Rollcall"; SB Admin 2 kaldırılır, `docs/DESIGN.md` token'larıyla yeni layout
- [x] EF Core migrations + seed (5 sınıf, 120 öğrenci)
- [x] Sınıflar CRUD'u (ViewModel'lerle, StudentsController'daki gibi) — TODO kapanır

## Aşama 2 — Yoklama ve notlar
- [ ] Yoklama: sınıf + tarih seç → öğrenci listesinde Var/Yok/Geç işaretle, toplu kaydet
- [ ] Not girişi: ders/sınav tanımı, öğrenci başına not, ortalama hesabı
- [ ] Öğrenci detay: devamsızlık özeti, not tablosu, basit grafik
- [ ] Dashboard: bugünkü yoklama durumu, devamsızlığı yüksek öğrenciler

## Aşama 3 — Cila, test, yayın
- [ ] Arama/filtre/sayfalama; CSV dışa aktarma (yoklama ve notlar)
- [ ] Servis katmanı ayrıştırma + xUnit testleri + GitHub Actions
- [ ] Canlı demo, ekran görüntüleri, README (TR + EN)
