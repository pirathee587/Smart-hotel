"use client";

import { Check,ChevronLeft,ChevronRight,Utensils } from "lucide-react";
import React,{ useRef } from "react";
import { MealPlan } from "../types/bookingSelection";

interface MealPlanCarouselProps {
  mealPlans: MealPlan[];
  selectedPlanId: string | null;
  onSelectPlan: (planId: string) => void;
}

export function MealPlanCarousel({
  mealPlans,
  selectedPlanId,
  onSelectPlan,
}: MealPlanCarouselProps) {
  const scrollContainerRef = useRef<HTMLDivElement>(null);

  const handleScroll = (direction: "left" | "right") => {
    if (!scrollContainerRef.current) return;
    const scrollAmount = 300;
    scrollContainerRef.current.scrollBy({
      left: direction === "left" ? -scrollAmount : scrollAmount,
      behavior: "smooth",
    });
  };

  const handleKeyDown = (e: React.KeyboardEvent, planId: string) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      onSelectPlan(planId);
    }
  };

  return (
    <section className="relative my-8" aria-label="Available Meal Plans">
      {/* Header & Subtext */}
      <div className="flex flex-col sm:flex-row sm:items-baseline justify-between gap-1 mb-4">
        <div>
          <span className="text-[#C4622D] text-[11px] font-semibold tracking-[0.2em] uppercase block">
            Customise Your Stay
          </span>
          <h2 className="font-serif font-bold text-2xl sm:text-3xl text-[#F6F1E6]">
            Select a Room &amp; Meal Plan
          </h2>
        </div>
        {/* Shown once above the row, not per card */}
        <p className="text-xs text-[#7C9188] italic">
          Prices are avg. daily rate excluding taxes and fees
        </p>
      </div>

      {/* Carousel Wrapper with Overlaid Chevron Arrows */}
      <div className="relative group">
        {/* Left Arrow Button */}
        <button
          type="button"
          suppressHydrationWarning
          onClick={() => handleScroll("left")}
          className="absolute left-0 top-1/2 -translate-y-1/2 -translate-x-2 sm:-translate-x-4 z-10 w-9 h-9 sm:w-10 sm:h-10 rounded-full bg-[#16302C] border border-[#2F5C52] text-[#F6F1E6] hover:text-[#C4622D] hover:border-[#C4622D] flex items-center justify-center shadow-xl transition-all opacity-80 group-hover:opacity-100 hover:scale-105 cursor-pointer"
          aria-label="Scroll meal plans left"
        >
          <ChevronLeft className="w-5 h-5" />
        </button>

        {/* Right Arrow Button */}
        <button
          type="button"
          suppressHydrationWarning
          onClick={() => handleScroll("right")}
          className="absolute right-0 top-1/2 -translate-y-1/2 translate-x-2 sm:translate-x-4 z-10 w-9 h-9 sm:w-10 sm:h-10 rounded-full bg-[#16302C] border border-[#2F5C52] text-[#F6F1E6] hover:text-[#C4622D] hover:border-[#C4622D] flex items-center justify-center shadow-xl transition-all opacity-80 group-hover:opacity-100 hover:scale-105 cursor-pointer"
          aria-label="Scroll meal plans right"
        >
          <ChevronRight className="w-5 h-5" />
        </button>

        {/* Scrollable Track */}
        <div
          ref={scrollContainerRef}
          role="radiogroup"
          aria-label="Meal plan options"
          className="flex gap-4 overflow-x-auto pb-3 pt-1 scroll-smooth snap-x snap-mandatory scrollbar-none px-1"
          style={{ scrollbarWidth: "none", msOverflowStyle: "none" }}
        >
          {mealPlans.map((plan) => {
            const isSelected = selectedPlanId === plan.id;
            return (
              <div
                key={plan.id}
                role="radio"
                tabIndex={0}
                aria-checked={isSelected}
                onClick={() => onSelectPlan(plan.id)}
                onKeyDown={(e) => handleKeyDown(e, plan.id)}
                className={`flex-shrink-0 w-64 sm:w-72 p-4 sm:p-5 rounded-2xl cursor-pointer transition-all duration-300 snap-start flex flex-col justify-between border relative select-none outline-none focus-visible:ring-2 focus-visible:ring-[#2F5C52] ${
                  isSelected
                    ? "bg-[#16302C] border-[#2F5C52] ring-2 ring-[#2F5C52] shadow-lg shadow-[#2F5C52]/20"
                    : "bg-[#16302C]/60 hover:bg-[#16302C] border-[#2F5C52]/40 hover:border-[#2F5C52]"
                }`}
              >
                {/* Active checkmark badge */}
                {isSelected && (
                  <div className="absolute top-3.5 right-3.5 flex items-center justify-center w-5 h-5 rounded-full bg-[#2F5C52] text-white">
                    <Check className="w-3 h-3 stroke-[3]" />
                  </div>
                )}

                <div>
                  <div className="flex items-center gap-2 mb-2 text-[#7C9188]">
                    <Utensils className="w-3.5 h-3.5 text-[#2F5C52]" />
                    <span className="text-[10px] font-semibold uppercase tracking-wider">
                      Meal Package
                    </span>
                  </div>

                  {/* Plan name (bold) */}
                  <h3 className="font-serif font-bold text-base sm:text-lg text-[#F6F1E6] leading-snug">
                    {plan.name}
                  </h3>

                  {plan.tagline && (
                    <p className="text-xs text-[#7C9188] mt-1.5 line-clamp-2 leading-relaxed">
                      {plan.tagline}
                    </p>
                  )}
                </div>

                {/* from $X price */}
                <div className="mt-4 pt-3 border-t border-[#2F5C52]/30 flex items-baseline justify-between">
                  <span className="text-[11px] text-[#7C9188]">Starting rate</span>
                  <div className="flex items-baseline gap-1">
                    <span className="text-xs text-[#7C9188]">from</span>
                    <span className="font-serif font-bold text-lg sm:text-xl text-[#F6F1E6]">
                      ${plan.fromPrice}
                    </span>
                    <span className="text-[10px] text-[#7C9188]">/ night</span>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}
