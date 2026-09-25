import StaffLoginView from "@/features/auth/components/StaffLoginView";
import { Metadata } from "next";
import { Suspense } from "react";

export const metadata: Metadata = {
  title: "Staff Portal Login — SmartHotel Maskeliya",
  description: "Authorized staff login to manage hotel operations, guest services, and reservations.",
};

export default function StaffLoginPage() {
  return (
    <Suspense fallback={<div className="min-h-screen bg-[#0F1B1A]" />}>
      <StaffLoginView />
    </Suspense>
  );
}
