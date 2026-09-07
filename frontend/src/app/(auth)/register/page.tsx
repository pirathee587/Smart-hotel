import { Suspense } from "react";
import { Metadata } from "next";
import AuthView from "@/features/auth/components/AuthView";

export const metadata: Metadata = {
  title: "Create Account — SmartHotel Maskeliya",
  description: "Set up guest access to book luxury stays and reach the concierge.",
};

export default function RegisterPage() {
  return (
    <Suspense fallback={<div className="min-h-screen bg-[#0F1B1A]" />}>
      <AuthView mode="register" />
    </Suspense>
  );
}
