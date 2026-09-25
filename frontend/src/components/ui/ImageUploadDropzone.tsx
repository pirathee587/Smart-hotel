"use client";

import { cn } from "@/lib/utils";
import {
ALLOWED_IMAGE_MIME_TYPES,
imageApi,
ImageCategory,
validateImageFile,
} from "@/services/imageApi";
import {
AlertCircle,
CheckCircle2,
Image as ImageIcon,
Loader2,
UploadCloud,
X,
} from "lucide-react";
import React,{ useCallback,useRef,useState } from "react";

export interface ImageUploadDropzoneProps {
  category: ImageCategory;
  onUploadComplete?: (url: string) => void;
  className?: string;
  label?: string;
  disabled?: boolean;
}

export function ImageUploadDropzone({
  category,
  onUploadComplete,
  className,
  label = "Upload Image",
  disabled = false,
}: ImageUploadDropzoneProps) {
  const [isDragging, setIsDragging] = useState(false);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successUrl, setSuccessUrl] = useState<string | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  const clearSelection = useCallback(() => {
    if (previewUrl && previewUrl.startsWith("blob:")) {
      URL.revokeObjectURL(previewUrl);
    }
    setSelectedFile(null);
    setPreviewUrl(null);
    setUploadProgress(null);
    setErrorMessage(null);
    setSuccessUrl(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  }, [previewUrl]);

  const handleFileSelect = useCallback(
    async (file: File) => {
      setErrorMessage(null);
      setSuccessUrl(null);

      // 1. Client-side validation fast feedback
      try {
        validateImageFile(file);
      } catch (err: unknown) {
        const message =
          err instanceof Error
            ? err.message
            : "Invalid file selected. Please select a valid image.";
        setErrorMessage(message);
        return;
      }

      // 2. Generate local preview URL
      const objectUrl = URL.createObjectURL(file);
      setSelectedFile(file);
      setPreviewUrl(objectUrl);
      setIsUploading(true);
      setUploadProgress(0);

      // 3. Upload to API Gateway -> HotelOps -> Supabase Storage
      try {
        const response = await imageApi.uploadImage(file, category, {
          onUploadProgress: (progressEvent) => {
            if (progressEvent.total) {
              const percent = Math.round(
                (progressEvent.loaded * 100) / progressEvent.total
              );
              setUploadProgress(percent);
            } else {
              setUploadProgress(50);
            }
          },
        });

        setIsUploading(false);
        setUploadProgress(100);
        setSuccessUrl(response.url);
        onUploadComplete?.(response.url);
      } catch (err: unknown) {
        setIsUploading(false);
        setUploadProgress(null);
        const message =
          err instanceof Error
            ? err.message
            : "Failed to upload image. Please try again.";
        setErrorMessage(message);
      }
    },
    [category, onUploadComplete]
  );

  // ── Drag & Drop Event Handlers ──────────────────────────────────────────────
  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    if (!disabled && !isUploading) {
      setIsDragging(true);
    }
  };

  const handleDragLeave = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);

    if (disabled || isUploading) return;

    const files = e.dataTransfer.files;
    if (files && files.length > 0) {
      handleFileSelect(files[0]);
    }
  };

  const handleClickBrowse = () => {
    if (!disabled && !isUploading) {
      fileInputRef.current?.click();
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0) {
      handleFileSelect(files[0]);
    }
  };

  return (
    <div className={cn("w-full space-y-3 text-[#F6F1E6]", className)}>
      {label && (
        <div className="flex items-center justify-between">
          <label className="text-sm font-medium text-[#F6F1E6]/90 flex items-center gap-2">
            <ImageIcon className="w-4 h-4 text-emerald-400" />
            {label}
          </label>
          <span className="text-xs text-[#F6F1E6]/50">
            JPEG, PNG, WebP • Max 5MB
          </span>
        </div>
      )}

      {/* Dropzone Container */}
      <div
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={previewUrl && !errorMessage ? undefined : handleClickBrowse}
        className={cn(
          "relative border-2 border-dashed rounded-2xl p-6 transition-all duration-300 flex flex-col items-center justify-center min-h-[200px] text-center cursor-pointer select-none",
          "bg-[#0F1B1A]/80 border-[#2F5C52]/40 backdrop-blur-md hover:border-emerald-500/60 hover:bg-[#16302C]/60",
          isDragging &&
            "border-emerald-400 bg-emerald-950/40 shadow-lg shadow-emerald-500/10 scale-[0.99]",
          disabled && "opacity-50 cursor-not-allowed",
          previewUrl && "cursor-default"
        )}
      >
        <input
          ref={fileInputRef}
          type="file"
          accept={ALLOWED_IMAGE_MIME_TYPES.join(",")}
          onChange={handleInputChange}
          disabled={disabled || isUploading}
          className="sr-only"
          aria-label="Upload image file"
        />

        {/* State 1: Active Preview & Upload Progress */}
        {previewUrl ? (
          <div className="relative w-full flex flex-col items-center gap-4">
            <div className="relative w-40 h-28 sm:w-52 sm:h-36 rounded-xl overflow-hidden border border-[#2F5C52]/60 shadow-xl bg-black/40">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={previewUrl}
                alt="Selected preview"
                className="w-full h-full object-cover"
              />

              {!isUploading && (
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    clearSelection();
                  }}
                  className="absolute top-2 right-2 p-1.5 rounded-full bg-black/70 hover:bg-rose-600/80 text-white transition-colors cursor-pointer"
                  title="Remove image"
                >
                  <X className="w-4 h-4" />
                </button>
              )}

              {isUploading && (
                <div className="absolute inset-0 bg-black/60 backdrop-blur-xs flex flex-col items-center justify-center p-3 text-white">
                  <Loader2 className="w-6 h-6 animate-spin text-emerald-400 mb-2" />
                  <span className="text-xs font-medium">
                    Uploading... {uploadProgress ?? 0}%
                  </span>
                </div>
              )}
            </div>

            {/* Progress Bar */}
            {isUploading && uploadProgress !== null && (
              <div className="w-full max-w-xs space-y-1.5">
                <div className="w-full bg-[#16302C] h-2 rounded-full overflow-hidden border border-[#2F5C52]/40">
                  <div
                    className="bg-gradient-to-r from-emerald-500 to-teal-400 h-full transition-all duration-200 rounded-full"
                    style={{ width: `${uploadProgress}%` }}
                  />
                </div>
                <div className="flex justify-between text-xs text-[#F6F1E6]/70">
                  <span>{selectedFile?.name}</span>
                  <span>{uploadProgress}%</span>
                </div>
              </div>
            )}

            {/* Uploaded Success Badge */}
            {successUrl && (
              <div className="flex items-center gap-2 text-xs text-emerald-400 bg-emerald-950/60 border border-emerald-500/30 px-3 py-1.5 rounded-full">
                <CheckCircle2 className="w-4 h-4 shrink-0" />
                <span>Uploaded successfully to {category}/</span>
                <button
                  type="button"
                  onClick={handleClickBrowse}
                  className="underline hover:text-emerald-300 ml-1 cursor-pointer"
                >
                  Change
                </button>
              </div>
            )}
          </div>
        ) : (
          /* State 2: Default Idle Dropzone */
          <div className="flex flex-col items-center gap-3">
            <div
              className={cn(
                "w-14 h-14 rounded-2xl flex items-center justify-center transition-transform duration-300",
                "bg-[#16302C] text-emerald-400 border border-[#2F5C52]/60",
                isDragging ? "scale-110 text-emerald-300" : "group-hover:scale-105"
              )}
            >
              <UploadCloud className="w-7 h-7" />
            </div>

            <div className="space-y-1">
              <p className="text-sm font-medium text-[#F6F1E6]">
                <span className="text-emerald-400 underline decoration-emerald-500/40 underline-offset-2">
                  Click to browse
                </span>{" "}
                or drag and drop
              </p>
              <p className="text-xs text-[#F6F1E6]/50">
                Destination: <span className="font-mono text-emerald-300/80">{category}/</span> folder
              </p>
            </div>
          </div>
        )}
      </div>

      {/* Inline Error Message */}
      {errorMessage && (
        <div className="flex items-start gap-2 p-3 rounded-xl bg-rose-950/50 border border-rose-500/30 text-rose-200 text-xs animate-in fade-in slide-in-from-top-1">
          <AlertCircle className="w-4 h-4 shrink-0 mt-0.5 text-rose-400" />
          <div className="flex-1">
            <p className="font-medium">Upload Error</p>
            <p className="text-rose-300/80">{errorMessage}</p>
          </div>
          <button
            type="button"
            onClick={() => setErrorMessage(null)}
            className="text-rose-400 hover:text-rose-200 cursor-pointer p-0.5"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}
    </div>
  );
}
