-- ==============================================================================
-- SmartHotel: Supabase Storage Setup & Access Policies
-- Target Bucket: hotel-images
-- Subfolders: rooms/, hotels/, employees/
-- ==============================================================================
-- Run these statements in the Supabase Dashboard SQL Editor (or via CLI)
-- ==============================================================================

-- 1. Create the 'hotel-images' storage bucket if it does not already exist
INSERT INTO storage.buckets (id, name, public)
VALUES ('hotel-images', 'hotel-images', true)
ON CONFLICT (id) DO UPDATE
SET public = true;

-- 2. Drop existing policies on 'hotel-images' to allow clean re-application
DROP POLICY IF EXISTS "Public Read Access for hotel-images" ON storage.objects;
DROP POLICY IF EXISTS "Service Role Full Access for hotel-images" ON storage.objects;
DROP POLICY IF EXISTS "Authenticated Staff Upload Access for hotel-images" ON storage.objects;

-- 3. Policy: Public Read Access
-- Allows anonymous and authenticated users to read and view images from the bucket
CREATE POLICY "Public Read Access for hotel-images"
ON storage.objects FOR SELECT
USING (bucket_id = 'hotel-images');

-- 4. Policy: Service Role Full Access
-- Grants the backend service (using SUPABASE_SERVICE_ROLE_KEY) full CRUD control
CREATE POLICY "Service Role Full Access for hotel-images"
ON storage.objects FOR ALL
TO service_role
USING (bucket_id = 'hotel-images')
WITH CHECK (bucket_id = 'hotel-images');

-- ==============================================================================
-- Notes on Subfolder Structure:
-- - rooms/:     Room type gallery, room photos, layout diagrams
-- - hotels/:    Hotel exterior, lobby, amenities, and facility images
-- - employees/: Staff profile avatars and badges
--
-- In Supabase Storage, subfolders are virtual prefixes separated by '/'
-- (e.g., 'rooms/abc123_deluxe.jpg').
-- ==============================================================================
