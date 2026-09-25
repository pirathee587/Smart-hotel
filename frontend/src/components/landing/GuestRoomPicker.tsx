"use client";

import { ChevronDown,Minus,Plus,Users } from "lucide-react";
import { useEffect,useRef,useState } from "react";

export interface RoomGuestConfig {
  adults: number;
  children: number;
}

interface GuestRoomPickerProps {
  rooms: RoomGuestConfig[];
  onChange: (rooms: RoomGuestConfig[]) => void;
  className?: string;
}

export function GuestRoomPicker({ rooms, onChange, className = "" }: GuestRoomPickerProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [placement, setPlacement] = useState<"top" | "bottom">("top");
  const containerRef = useRef<HTMLDivElement>(null);

  // Close on outside click
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener("mousedown", handleClickOutside);
      document.addEventListener("keydown", handleKeyDown);
    }

    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  // Adjust placement based on viewport space
  useEffect(() => {
    if (!isOpen || !containerRef.current) return;

    const updatePlacement = () => {
      if (!containerRef.current) return;
      const rect = containerRef.current.getBoundingClientRect();
      const spaceBelow = window.innerHeight - rect.bottom;
      const spaceAbove = rect.top;

      // Prefer popping above over the hero background unless space above is insufficient
      if (spaceAbove > 360) {
        setPlacement("top");
      } else if (spaceBelow > 360) {
        setPlacement("bottom");
      } else {
        setPlacement(spaceAbove > spaceBelow ? "top" : "bottom");
      }
    };

    updatePlacement();
    window.addEventListener("scroll", updatePlacement, { passive: true });
    window.addEventListener("resize", updatePlacement);

    return () => {
      window.removeEventListener("scroll", updatePlacement);
      window.removeEventListener("resize", updatePlacement);
    };
  }, [isOpen]);

  const totalAdults = rooms.reduce((sum, r) => sum + r.adults, 0);
  const totalChildren = rooms.reduce((sum, r) => sum + r.children, 0);
  const totalGuests = totalAdults + totalChildren;

  const formatCount = (n: number) => String(n).padStart(2, "0");

  const handleRoomCountChange = (delta: number) => {
    if (delta > 0 && rooms.length < 5) {
      onChange([...rooms, { adults: 2, children: 0 }]);
    } else if (delta < 0 && rooms.length > 1) {
      onChange(rooms.slice(0, -1));
    }
  };

  const handleAdultsChange = (index: number, delta: number) => {
    const updated = [...rooms];
    const newCount = updated[index].adults + delta;
    if (newCount >= 1 && newCount <= 6) {
      updated[index] = { ...updated[index], adults: newCount };
      onChange(updated);
    }
  };

  const handleChildrenChange = (index: number, delta: number) => {
    const updated = [...rooms];
    const newCount = updated[index].children + delta;
    if (newCount >= 0 && newCount <= 4) {
      updated[index] = { ...updated[index], children: newCount };
      onChange(updated);
    }
  };

  const summaryText = `${rooms.length} ${rooms.length === 1 ? "Room" : "Rooms"}, ${totalGuests} ${totalGuests === 1 ? "Guest" : "Guests"}`;
  const subText = `${totalAdults} ${totalAdults === 1 ? "Adult" : "Adults"}${totalChildren > 0 ? `, ${totalChildren} Child${totalChildren > 1 ? "ren" : ""}` : ""}`;

  return (
    <div ref={containerRef} className={`relative ${isOpen ? "z-50" : "z-20"} space-y-1 ${className}`}>
      <label
        id="hero-guests-label"
        onClick={() => setIsOpen(!isOpen)}
        className="text-[11px] text-[#7C9188] uppercase tracking-wider font-medium flex items-center gap-1.5 cursor-pointer select-none"
      >
        <Users className="w-3.5 h-3.5 text-[#C4622D]" />
        Guests
      </label>

      {/* Trigger Button */}
      <button
        type="button"
        id="hero-guests"
        aria-haspopup="dialog"
        aria-expanded={isOpen}
        aria-labelledby="hero-guests-label"
        onClick={() => setIsOpen(!isOpen)}
        className="w-full bg-[#16302C]/80 border border-[#2F5C52]/50 hover:border-[#C4622D]/80 text-sm text-[#F6F1E6] px-3 py-2 text-left focus:outline-none focus:border-[#C4622D] rounded-none cursor-pointer flex items-center justify-between transition-colors"
      >
        <span className="truncate">{summaryText}</span>
        <ChevronDown
          className={`w-3.5 h-3.5 text-[#7C9188] shrink-0 transition-transform duration-200 ${
            isOpen ? "rotate-180 text-[#C4622D]" : ""
          }`}
        />
      </button>

      <p className="text-[10px] text-[#7C9188] truncate">{subText}</p>

      {/* Dropdown Modal/Card matching exact design */}
      {isOpen && (
        <div
          role="dialog"
          aria-label="Guests and Rooms Selector"
          className={`absolute left-0 z-50 w-[min(330px,calc(100vw-2.5rem))] bg-white text-gray-900 rounded-xl shadow-2xl border border-gray-100 p-5 sm:p-6 transition-all duration-200 animate-in fade-in zoom-in-95 max-h-[min(480px,80vh)] overflow-y-auto ${
            placement === "top" ? "bottom-[calc(100%+8px)]" : "top-[calc(100%+8px)]"
          }`}
        >
          {/* Rooms Row */}
          <div className="flex items-center justify-between">
            <span className="text-base font-normal text-gray-900">Rooms</span>
            <div className="flex items-center gap-3">
              <button
                type="button"
                aria-label="Decrease rooms"
                onClick={() => handleRoomCountChange(-1)}
                disabled={rooms.length <= 1}
                className="w-9 h-9 rounded-full border border-gray-300 flex items-center justify-center text-gray-700 bg-white hover:bg-gray-50 active:scale-95 transition-all disabled:opacity-30 disabled:pointer-events-none disabled:border-gray-200 cursor-pointer"
              >
                <Minus className="w-3.5 h-3.5 stroke-[1.75]" />
              </button>
              <span className="w-7 text-center text-sm font-medium text-gray-900 select-none tabular-nums">
                {formatCount(rooms.length)}
              </span>
              <button
                type="button"
                aria-label="Increase rooms"
                onClick={() => handleRoomCountChange(1)}
                disabled={rooms.length >= 5}
                className="w-9 h-9 rounded-full border border-gray-300 flex items-center justify-center text-gray-700 bg-white hover:bg-gray-50 active:scale-95 transition-all disabled:opacity-30 disabled:pointer-events-none disabled:border-gray-200 cursor-pointer"
              >
                <Plus className="w-3.5 h-3.5 stroke-[1.75]" />
              </button>
            </div>
          </div>

          <div className="border-t border-gray-100 my-4" />

          {/* Room Breakdown List (scrollable if multiple rooms) */}
          <div className="max-h-[290px] overflow-y-auto space-y-4 pr-1 -mr-1">
            {rooms.map((room, index) => (
              <div key={index} className={index > 0 ? "pt-4 border-t border-gray-100" : ""}>
                {/* Room Header: ROOM 01 in purple */}
                <h4 className="text-xs font-bold tracking-wider text-[#58327E] uppercase mb-3">
                  ROOM {formatCount(index + 1)}
                </h4>

                {/* Adults Row */}
                <div className="flex items-center justify-between mb-3.5">
                  <div>
                    <div className="text-sm font-normal text-gray-900">Adults</div>
                    <div className="text-xs text-gray-400 font-normal mt-0.5">(12+ years)</div>
                  </div>
                  <div className="flex items-center gap-3">
                    <button
                      type="button"
                      aria-label={`Decrease adults in room ${index + 1}`}
                      onClick={() => handleAdultsChange(index, -1)}
                      disabled={room.adults <= 1}
                      className="w-9 h-9 rounded-full border border-gray-300 flex items-center justify-center text-gray-700 bg-white hover:bg-gray-50 active:scale-95 transition-all disabled:opacity-30 disabled:pointer-events-none disabled:border-gray-200 cursor-pointer"
                    >
                      <Minus className="w-3.5 h-3.5 stroke-[1.75]" />
                    </button>
                    <span className="w-7 text-center text-sm font-medium text-gray-900 select-none tabular-nums">
                      {formatCount(room.adults)}
                    </span>
                    <button
                      type="button"
                      aria-label={`Increase adults in room ${index + 1}`}
                      onClick={() => handleAdultsChange(index, 1)}
                      disabled={room.adults >= 6}
                      className="w-9 h-9 rounded-full border border-gray-300 flex items-center justify-center text-gray-700 bg-white hover:bg-gray-50 active:scale-95 transition-all disabled:opacity-30 disabled:pointer-events-none disabled:border-gray-200 cursor-pointer"
                    >
                      <Plus className="w-3.5 h-3.5 stroke-[1.75]" />
                    </button>
                  </div>
                </div>

                {/* Children Row */}
                <div className="flex items-center justify-between">
                  <div>
                    <div className="text-sm font-normal text-gray-900">Children</div>
                    <div className="text-xs text-gray-400 font-normal mt-0.5">(2-11 years)</div>
                  </div>
                  <div className="flex items-center gap-3">
                    <button
                      type="button"
                      aria-label={`Decrease children in room ${index + 1}`}
                      onClick={() => handleChildrenChange(index, -1)}
                      disabled={room.children <= 0}
                      className="w-9 h-9 rounded-full border border-gray-300 flex items-center justify-center text-gray-700 bg-white hover:bg-gray-50 active:scale-95 transition-all disabled:opacity-30 disabled:pointer-events-none disabled:border-gray-200 cursor-pointer"
                    >
                      <Minus className="w-3.5 h-3.5 stroke-[1.75]" />
                    </button>
                    <span className="w-7 text-center text-sm font-medium text-gray-900 select-none tabular-nums">
                      {formatCount(room.children)}
                    </span>
                    <button
                      type="button"
                      aria-label={`Increase children in room ${index + 1}`}
                      onClick={() => handleChildrenChange(index, 1)}
                      disabled={room.children >= 4}
                      className="w-9 h-9 rounded-full border border-gray-300 flex items-center justify-center text-gray-700 bg-white hover:bg-gray-50 active:scale-95 transition-all disabled:opacity-30 disabled:pointer-events-none disabled:border-gray-200 cursor-pointer"
                    >
                      <Plus className="w-3.5 h-3.5 stroke-[1.75]" />
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>

          {/* Bottom Done button */}
          <div className="pt-4 mt-4 border-t border-gray-100 flex items-center justify-between">
            <span className="text-[11px] text-gray-400">
              {totalGuests} {totalGuests === 1 ? "Guest" : "Guests"} across {rooms.length} {rooms.length === 1 ? "Room" : "Rooms"}
            </span>
            <button
              type="button"
              onClick={() => setIsOpen(false)}
              className="px-4 py-1.5 bg-[#0F1B1A] hover:bg-[#C4622D] text-white text-xs font-medium uppercase tracking-wider rounded transition-colors cursor-pointer"
            >
              Done
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
