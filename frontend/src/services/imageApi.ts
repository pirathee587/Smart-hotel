import api from "@/lib/axios";
import type { RoomTypeImageDto } from "@/types/roomTypes";
import type { AxiosProgressEvent } from "axios";

export type ImageCategory = "rooms" | "hotels" | "employees" | "guests";

export interface ImageUploadResponse {
  fileName: string;
  category: string;
  url: string;
  path?: string;
}

export interface StorageFileDto {
  name: string;
  path: string;
  url: string;
  sizeBytes?: number | null;
  contentType?: string | null;
  createdAt?: string | null;
}

export interface UploadImageOptions {
  signal?: AbortSignal;
  onUploadProgress?: (progressEvent: AxiosProgressEvent) => void;
}

export const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024; // 5MB
export const ALLOWED_IMAGE_MIME_TYPES = [
  "image/jpeg",
  "image/png",
  "image/webp",
];

/**
 * Validates file size and MIME type before performing a network request.
 * Throws an informative Error if validation fails.
 */
export function validateImageFile(file: File): void {
  if (!file) {
    throw new Error("No file provided for upload.");
  }

  if (file.size > MAX_IMAGE_SIZE_BYTES) {
    const sizeMb = (file.size / (1024 * 1024)).toFixed(2);
    throw new Error(
      `File size (${sizeMb} MB) exceeds the maximum allowed limit of 5 MB.`
    );
  }

  if (!ALLOWED_IMAGE_MIME_TYPES.includes(file.type)) {
    throw new Error(
      `Invalid image format "${file.type || "unknown"}". Only JPEG, PNG, and WebP are allowed.`
    );
  }
}

export const imageApi = {
  /**
   * Upload an image to Supabase Storage under the specified category (rooms, hotels, employees).
   * Validates client-side before sending multipart request to API Gateway.
   */
  async uploadImage(
    file: File,
    category: ImageCategory = "rooms",
    options?: UploadImageOptions
  ): Promise<ImageUploadResponse> {
    // Client-side validation fast feedback
    validateImageFile(file);

    const formData = new FormData();
    formData.append("file", file);

    const { data } = await api.post<ImageUploadResponse>(
      `/api/v1/images/upload?category=${encodeURIComponent(category)}`,
      formData,
      {
        headers: {
          "Content-Type": "multipart/form-data",
        },
        signal: options?.signal,
        onUploadProgress: options?.onUploadProgress,
      }
    );

    return data;
  },

  /**
   * Fetch list of images stored under a category folder in Supabase Storage.
   */
  async fetchCategoryImages(
    category: ImageCategory = "rooms",
    signal?: AbortSignal
  ): Promise<StorageFileDto[]> {
    const { data } = await api.get<StorageFileDto[]>(
      `/api/v1/images?category=${encodeURIComponent(category)}`,
      { signal }
    );
    return data;
  },

  /**
   * Fetch all images associated with a specific RoomType.
   */
  async fetchRoomTypeImages(
    roomTypeId: string,
    signal?: AbortSignal
  ): Promise<RoomTypeImageDto[]> {
    const { data } = await api.get<RoomTypeImageDto[]>(
      `/api/v1/room-types/${encodeURIComponent(roomTypeId)}/images`,
      { signal }
    );
    return data;
  },

  /**
   * Delete an image from storage by its public URL or storage path.
   */
  async deleteImage(
    pathOrUrl: string,
    signal?: AbortSignal
  ): Promise<{ message: string; imageUrl?: string }> {
    const { data } = await api.delete<{ message: string; imageUrl?: string }>(
      "/api/v1/images",
      {
        data: {
          imageUrl: pathOrUrl,
          path: pathOrUrl,
        },
        signal,
      }
    );
    return data;
  },
};
