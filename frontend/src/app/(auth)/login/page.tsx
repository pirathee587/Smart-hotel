import { Suspense } from "react";
import { Metadata } from "next";
import AuthView from "@/features/auth/components/AuthView";

export const metadata: Metadata = {
  title: "Sign in — SmartHotel Maskeliya",
  description: "Sign in to manage your stay, view personalized services, or access the staff dashboard.",
};

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="min-h-screen bg-[#0F1B1A]" />}>
      <AuthView mode="login" />
    </Suspense>
  );
}
