"use client";

import {
Accessibility,
ArrowUpDown,
Check,
ChevronDown,
Layers,
SlidersHorizontal,
} from "lucide-react";
import { useEffect,useRef,useState } from "react";

interface RoomFilterBarProps {
  accessibleOnly: boolean;
  onToggleAccessible: () => void;
  viewBy: "rooms" | "suites" | "villas";
  onChangeViewBy: (view: "rooms" | "suites" | "villas") => void;
  sortBy: "lowest_price" | "highest_price" | "sleeps" | "rating";
  onChangeSortBy: (sort: "lowest_price" | "highest_price" | "sleeps" | "rating") => void;
  selectedAmenities: string[];
  onToggleAmenity: (amenity: string) => void;
  allAmenities: string[];
}

export function RoomFilterBar({
  accessibleOnly,
  onToggleAccessible,
  viewBy,
  onChangeViewBy,
  sortBy,
  onChangeSortBy,
  selectedAmenities,
  onToggleAmenity,
  allAmenities,
}: RoomFilterBarProps) {
  const [viewOpen, setViewOpen] = useState(false);
  const [sortOpen, setSortOpen] = useState(false);
  const [filtersOpen, setFiltersOpen] = useState(false);

  const viewRef = useRef<HTMLDivElement>(null);
  const sortRef = useRef<HTMLDivElement>(null);
  const filtersRef = useRef<HTMLDivElement>(null);

  // Close menus when clicked outside
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (viewRef.current && !viewRef.current.contains(e.target as Node)) {
        setViewOpen(false);
      }
      if (sortRef.current && !sortRef.current.contains(e.target as Node)) {
        setSortOpen(false);
      }
      if (filtersRef.current && !filtersRef.current.contains(e.target as Node)) {
        setFiltersOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const VIEW_OPTIONS: { id: "rooms" | "suites" | "villas"; label: string }[] = [
    { id: "rooms", label: "Rooms & Residences" },
    { id: "suites", label: "Slowhouse Suites" },
    { id: "villas", label: "Private Villas" },
  ];

  const SORT_OPTIONS: {
    id: "lowest_price" | "highest_price" | "sleeps" | "rating";
    label: string;
  }[] = [
    { id: "lowest_price", label: "Lowest Price" },
    { id: "highest_price", label: "Highest Price" },
    { id: "sleeps", label: "Guest Capacity" },
    { id: "rating", label: "Highest Rated" },
  ];

  const currentViewLabel =
    VIEW_OPTIONS.find((v) => v.id === viewBy)?.label ?? "Rooms";
  const currentSortLabel =
    SORT_OPTIONS.find((s) => s.id === sortBy)?.label ?? "Lowest Price";

  return (
    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 py-4 px-4 sm:px-6 bg-[#16302C]/80 border border-[#2F5C52]/40 rounded-2xl mb-8 backdrop-blur-sm">
      {/* Left: Accessible checkbox with accessibility icon */}
      <label className="inline-flex items-center gap-2.5 text-xs sm:text-sm font-medium text-[#F6F1E6] cursor-pointer select-none group">
        <div className="relative flex items-center">
          <input
            type="checkbox"
            checked={accessibleOnly}
            onChange={onToggleAccessible}
            className="sr-only"
          />
          <div
            className={`w-5 h-5 rounded-md border flex items-center justify-center transition-all ${
              accessibleOnly
                ? "bg-[#2F5C52] border-[#2F5C52] text-white"
                : "border-[#2F5C52] bg-[#0F1B1A] group-hover:border-[#C4622D]"
            }`}
          >
            {accessibleOnly && <Check className="w-3.5 h-3.5 stroke-[3]" />}
          </div>
        </div>
        <div className="flex items-center gap-1.5 text-[#F6F1E6] group-hover:text-white transition-colors">
          <Accessibility className="w-4 h-4 text-[#2F5C52] group-hover:text-[#C4622D] transition-colors" />
          <span>Accessible</span>
        </div>
      </label>

      {/* Right cluster of dropdowns: View By, Sort By, Filters */}
      <div className="flex flex-wrap items-center gap-2.5 sm:gap-3">
        {/* Dropdown 1: View By */}
        <div className="relative" ref={viewRef}>
          <button
            type="button"
            suppressHydrationWarning
            onClick={() => {
              setViewOpen((prev) => !prev);
              setSortOpen(false);
              setFiltersOpen(false);
            }}
            className="inline-flex items-center gap-2 px-3 sm:px-3.5 py-2 rounded-xl bg-[#0F1B1A] border border-[#2F5C52]/60 hover:border-[#2F5C52] text-xs font-medium text-[#F6F1E6] transition-colors cursor-pointer"
            aria-expanded={viewOpen}
            aria-haspopup="listbox"
          >
            <Layers className="w-3.5 h-3.5 text-[#7C9188]" />
            <span className="text-[#7C9188]">View By:</span>
            <span className="text-[#F6F1E6] font-semibold">{currentViewLabel}</span>
            <ChevronDown
              className={`w-3.5 h-3.5 text-[#7C9188] transition-transform duration-200 ${
                viewOpen ? "rotate-180 text-[#C4622D]" : ""
              }`}
            />
          </button>

          {viewOpen && (
            <div
              role="listbox"
              className="absolute right-0 top-full mt-2 w-52 bg-[#16302C] border border-[#2F5C52] rounded-xl shadow-2xl p-1.5 z-40 animate-in fade-in zoom-in-95 duration-150"
            >
              {VIEW_OPTIONS.map((opt) => (
                <button
                  key={opt.id}
                  type="button"
                  suppressHydrationWarning
                  onClick={() => {
                    onChangeViewBy(opt.id);
                    setViewOpen(false);
                  }}
                  className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs transition-colors cursor-pointer ${
                    viewBy === opt.id
                      ? "bg-[#2F5C52]/30 text-[#C4622D] font-semibold"
                      : "text-[#F6F1E6] hover:bg-[#2F5C52]/20"
                  }`}
                >
                  <span>{opt.label}</span>
                  {viewBy === opt.id && <Check className="w-3.5 h-3.5 text-[#C4622D]" />}
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Dropdown 2: Sort By */}
        <div className="relative" ref={sortRef}>
          <button
            type="button"
            suppressHydrationWarning
            onClick={() => {
              setSortOpen((prev) => !prev);
              setViewOpen(false);
              setFiltersOpen(false);
            }}
            className="inline-flex items-center gap-2 px-3 sm:px-3.5 py-2 rounded-xl bg-[#0F1B1A] border border-[#2F5C52]/60 hover:border-[#2F5C52] text-xs font-medium text-[#F6F1E6] transition-colors cursor-pointer"
            aria-expanded={sortOpen}
            aria-haspopup="listbox"
          >
            <ArrowUpDown className="w-3.5 h-3.5 text-[#7C9188]" />
            <span className="text-[#7C9188]">Sort By:</span>
            <span className="text-[#F6F1E6] font-semibold">{currentSortLabel}</span>
            <ChevronDown
              className={`w-3.5 h-3.5 text-[#7C9188] transition-transform duration-200 ${
                sortOpen ? "rotate-180 text-[#C4622D]" : ""
              }`}
            />
          </button>

          {sortOpen && (
            <div
              role="listbox"
              className="absolute right-0 top-full mt-2 w-48 bg-[#16302C] border border-[#2F5C52] rounded-xl shadow-2xl p-1.5 z-40 animate-in fade-in zoom-in-95 duration-150"
            >
              {SORT_OPTIONS.map((opt) => (
                <button
                  key={opt.id}
                  type="button"
                  suppressHydrationWarning
                  onClick={() => {
                    onChangeSortBy(opt.id);
                    setSortOpen(false);
                  }}
                  className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs transition-colors cursor-pointer ${
                    sortBy === opt.id
                      ? "bg-[#2F5C52]/30 text-[#C4622D] font-semibold"
                      : "text-[#F6F1E6] hover:bg-[#2F5C52]/20"
                  }`}
                >
                  <span>{opt.label}</span>
                  {sortBy === opt.id && <Check className="w-3.5 h-3.5 text-[#C4622D]" />}
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Dropdown 3: Filters (Panel) */}
        <div className="relative" ref={filtersRef}>
          <button
            type="button"
            suppressHydrationWarning
            onClick={() => {
              setFiltersOpen((prev) => !prev);
              setViewOpen(false);
              setSortOpen(false);
            }}
            className="inline-flex items-center gap-2 px-3 sm:px-3.5 py-2 rounded-xl bg-[#0F1B1A] border border-[#2F5C52]/60 hover:border-[#2F5C52] text-xs font-medium text-[#F6F1E6] transition-colors cursor-pointer"
            aria-expanded={filtersOpen}
          >
            <SlidersHorizontal className="w-3.5 h-3.5 text-[#7C9188]" />
            <span>Filters</span>
            {selectedAmenities.length > 0 && (
              <span className="w-4 h-4 rounded-full bg-[#C4622D] text-white text-[10px] flex items-center justify-center font-bold">
                {selectedAmenities.length}
              </span>
            )}
            <ChevronDown
              className={`w-3.5 h-3.5 text-[#7C9188] transition-transform duration-200 ${
                filtersOpen ? "rotate-180 text-[#C4622D]" : ""
              }`}
            />
          </button>

          {filtersOpen && (
            <div className="absolute right-0 top-full mt-2 w-72 bg-[#16302C] border border-[#2F5C52] rounded-2xl shadow-2xl p-4 z-40 animate-in fade-in zoom-in-95 duration-150">
              <div className="flex items-center justify-between pb-2.5 border-b border-[#2F5C52]/40">
                <span className="text-xs font-serif font-semibold text-[#F6F1E6]">
                  Filter by Amenities
                </span>
                {selectedAmenities.length > 0 && (
                  <button
                    type="button"
                    onClick={() => {
                      selectedAmenities.forEach((a) => onToggleAmenity(a));
                    }}
                    className="text-[10px] text-[#7C9188] hover:text-[#C4622D]"
                  >
                    Clear All
                  </button>
                )}
              </div>

              <div className="py-2 space-y-1.5 max-h-56 overflow-y-auto">
                {allAmenities.map((amenity) => {
                  const isChecked = selectedAmenities.includes(amenity);
                  return (
                    <button
                      key={amenity}
                      type="button"
                      onClick={() => onToggleAmenity(amenity)}
                      className="w-full flex items-center justify-between px-2.5 py-1.5 rounded-lg text-xs text-[#F6F1E6] hover:bg-[#2F5C52]/20 text-left transition-colors cursor-pointer"
                    >
                      <span>{amenity}</span>
                      <div
                        className={`w-4 h-4 rounded border flex items-center justify-center ${
                          isChecked
                            ? "bg-[#2F5C52] border-[#2F5C52] text-white"
                            : "border-[#2F5C52]/80 bg-[#0F1B1A]"
                        }`}
                      >
                        {isChecked && <Check className="w-3 h-3 stroke-[3]" />}
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
