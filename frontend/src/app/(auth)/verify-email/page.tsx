import VerifyEmailView from "@/features/auth/components/VerifyEmailView";
import { Metadata } from "next";
import { Suspense } from "react";

export const metadata: Metadata = {
  title: "Verify Email — SmartHotel Maskeliya",
  description: "Verify your email address to activate your SmartHotel account.",
};

export default function VerifyEmailPage() {
  return (
    <Suspense fallback={<div className="min-h-screen bg-[#0F1B1A]" />}>
      <VerifyEmailView />
    </Suspense>
  );
}
