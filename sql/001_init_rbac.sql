-- Skema RBAC untuk Rental PS Smart Billing
-- Tabel dibuat otomatis saat aplikasi pertama kali dijalankan (DatabaseInitializer).
-- File ini sebagai referensi manual.

USE rental_ps;
GO

-- Roles, Permissions, RolePermissions, Users, UserRoles
-- Lihat app/Data/DatabaseInitializer.cs untuk definisi lengkap.

-- Akun default (password di-hash saat seed):
--   admin / Admin123!     -> superadmin
--   operator1 / Operator123! -> operator
