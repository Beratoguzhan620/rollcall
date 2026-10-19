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
- [x] Yoklama: sınıf + tarih seç → öğrenci listesinde Var/Yok/Geç işaretle, toplu kaydet
- [x] Not girişi: ders/sınav tanımı, öğrenci başına not, ortalama hesabı
- [x] Öğrenci detay: devamsızlık özeti, not tablosu, basit grafik
- [x] Dashboard: bugünkü yoklama durumu, devamsızlığı yüksek öğrenciler

## Aşama 3 — Cila, test, yayın
- [x] Arama/filtre/sayfalama; CSV dışa aktarma (yoklama ve notlar)
- [x] Servis katmanı ayrıştırma + xUnit testleri + GitHub Actions
- [x] Ekran görüntüleri, README (TR + EN)
- [ ] Canlı demo
