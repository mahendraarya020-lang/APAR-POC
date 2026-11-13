# Rescue Checklist PWA

Aplikasi sampel pemeriksaan peralatan rescue berbasis ASP.NET Core 10 MVC, Identity, PostgreSQL 16, dan PWA. Data awal sepenuhnya fiktif.

Penjelasan lengkap implementasi, diagram arsitektur, model data, endpoint, alur offline, dan batas sampel tersedia di [DOKUMENTASI_ARSITEKTUR.md](DOKUMENTASI_ARSITEKTUR.md).

## Jalankan lokal

Prasyarat: .NET SDK 10, Docker Desktop dengan Linux containers, dan PowerShell. Dari folder ini:

```powershell
.\start-local.ps1
```

Skrip membuat `.env` lokal berisi kata sandi acak, menyalakan PostgreSQL 16, menjalankan migrasi saat aplikasi pertama kali hidup, dan menampilkan alamat serta kata sandi demo. Buka `http://localhost:5137`.

Peran demo: `admin`, `pic`, `rescue`, `management`. Semua memakai kata sandi demo yang ditampilkan skrip. Untuk mereset kata sandi demo, hapus akun melalui database lokal atau gunakan volume baru; mengganti `.env` saja tidak mengubah sandi akun yang sudah disimpan. Data database tetap ada setelah web ditutup. Hentikan database dengan `docker compose down` (tanpa `-v` agar data tetap ada).

Jika PowerShell menolak skrip, jalankan `powershell -ExecutionPolicy Bypass -File .\start-local.ps1` dari folder ini.

## Alur uji

1. Masuk sebagai `rescue`, buka Peralatan, lalu mulai inspeksi. Isi semua butir; catatan wajib untuk `Perlu tindakan`.
2. Simpan draf lokal atau kirim. Buka Riwayat untuk melihat detail dan versi checklist.
3. Masuk sebagai `pic`, buka Tindak lanjut dan tambahkan catatan serta status temuan.
4. Masuk sebagai `management`, buka Laporan dan unduh CSV.
5. Masuk sebagai `admin` untuk membuat gedung, peralatan, template versi baru, atau akun.
6. Untuk offline: kunjungi aplikasi saat online sebagai `rescue`, putuskan jaringan melalui DevTools, buka ulang halaman. Halaman offline memakai data tugas yang telah diunduh. Simpan draf, sambungkan jaringan, lalu buka aplikasi untuk sinkronisasi otomatis atau tekan **Sinkronkan sekarang**.

Saat keluar, aplikasi meminta konfirmasi lalu menghapus seluruh draf dan data tugas lokal pada perangkat. Jangan gunakan perangkat bersama untuk draf sensitif tanpa aturan organisasi yang jelas.

## Konfigurasi

- `ConnectionStrings__DefaultConnection`: koneksi PostgreSQL.
- `RESCUE_DEMO_PASSWORD`: hanya untuk seed akun demo di `Development`.
- `.env` hanya untuk lokal dan diabaikan Git. `.env.example` berisi placeholder.
- Migrasi EF Core ada di `src/RescueChecklist.Web/Migrations`. Untuk membuat migrasi baru: `dotnet tool restore`, lalu `dotnet tool run dotnet-ef migrations add NamaMigrasi --project src/RescueChecklist.Web --startup-project src/RescueChecklist.Web`.

## Azure berikutnya

Build aplikasi untuk Azure App Service dan hubungkan ke Azure Database for PostgreSQL. Set connection string melalui konfigurasi rahasia, aktifkan HTTPS, dan terapkan migrasi melalui proses rilis yang ditinjau. Akun demo dan `RESCUE_DEMO_PASSWORD` hanya untuk pengembangan lokal. Tidak ada deployment Azure dalam sampel ini.

Isi checklist, kebijakan akun, akses gedung, dan aturan tindak lanjut harus dicocokkan dengan BRD resmi sebelum dipakai secara operasional.

## Pengujian

Jalankan `dotnet test RescueChecklist.slnx`. Pengujian xUnit mencakup batas akses gedung, validasi temuan, pengiriman idempoten, konflik versi, dan redirect pengguna anonim. Uji browser lokal telah mencakup penyimpanan draf offline dan sinkronisasi ketika kembali online.
