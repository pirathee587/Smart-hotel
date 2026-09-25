"use client";

import { useEffect } from "react";
import GuestSettingsView from "./GuestSettingsView";

interface GuestSettingsSheetProps {
  isOpen: boolean;
  onClose: () => void;
}

export function GuestSettingsSheet({ isOpen, onClose }: GuestSettingsSheetProps) {
  // Close on Escape key press
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape" && isOpen) {
        onClose();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  // Prevent background scroll when open
  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = "hidden";
    } else {
      document.body.style.overflow = "";
    }
    return () => {
      document.body.style.overflow = "";
    };
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[100] flex justify-end">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black/60 backdrop-blur-sm transition-opacity animate-in fade-in duration-300"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Slide-over Drawer Panel (Half-page layout) */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Guest Sanctuary Settings"
        className="relative z-10 w-full sm:w-[580px] md:w-[660px] lg:w-[740px] h-full bg-[#F7F6F2] border-l border-[#0F1B1A]/10 shadow-2xl overflow-y-auto p-6 sm:p-8 animate-in slide-in-from-right duration-300 selection:bg-[#C4622D] selection:text-white"
      >
        <GuestSettingsView onClose={onClose} isDrawer={true} />
      </div>
    </div>
  );
}

export default GuestSettingsSheet;
