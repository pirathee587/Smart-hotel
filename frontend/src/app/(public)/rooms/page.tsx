"use client";

import { Loader2 } from "lucide-react";
import dynamic from "next/dynamic";

const RoomSelectionPage = dynamic(
  () =>
    import("@/features/bookings/components/RoomSelectionPage").then(
      (mod) => mod.RoomSelectionPage
    ),
  {
    ssr: false,
    loading: () => (
      <div className="min-h-screen bg-[#0F1B1A] flex items-center justify-center text-[#F6F1E6]">
        <div className="flex items-center gap-3">
          <Loader2 className="w-6 h-6 text-[#C4622D] animate-spin" />
          <span className="text-sm font-light text-[#EEE7D6]">
            Preparing Maskeliya room selection...
          </span>
        </div>
      </div>
    ),
  }
);

export default function RoomsRoutePage() {
  return <RoomSelectionPage />;
}
