import ForceResetPasswordView from "@/features/auth/components/ForceResetPasswordView";
import { Metadata } from "next";
import { Suspense } from "react";

export const metadata: Metadata = {
  title: "Set Permanent Password — SmartHotel Maskeliya",
  description: "Mandatory first-login password reset for SmartHotel staff and management.",
};

export default function ForceResetPasswordPage() {
  return (
    <Suspense fallback={<div className="min-h-screen bg-[#0F1B1A]" />}>
      <ForceResetPasswordView />
    </Suspense>
  );
}
